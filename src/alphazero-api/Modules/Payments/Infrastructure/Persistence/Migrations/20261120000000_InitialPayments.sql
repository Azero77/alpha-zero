-- Initial Payments schema. PostgreSQL SQL is the schema source of truth.
-- Applied by PaymentsSqlMigrationRunner in one transaction; standalone use:
-- psql --single-transaction --file 20261120000000_InitialPayments.sql "$DATABASE_URL"
-- The migration connection must be a superuser or hold CREATEROLE (this migration
-- grants it membership in payments_migration). Runtime connections must not use it.

DO $roles$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'payments_migration') THEN
        CREATE ROLE payments_migration NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'payments_runtime') THEN
        CREATE ROLE payments_runtime NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'payments_pii_erasure') THEN
        CREATE ROLE payments_pii_erasure NOLOGIN;
    END IF;
END
$roles$;

DO $membership$
BEGIN
    EXECUTE format('GRANT payments_migration TO %I', current_user);
END
$membership$;

CREATE SCHEMA IF NOT EXISTS "Payments" AUTHORIZATION payments_migration;
ALTER SCHEMA "Payments" OWNER TO payments_migration;
GRANT USAGE, CREATE ON SCHEMA "Payments" TO payments_migration;
SET LOCAL ROLE payments_migration;

CREATE TABLE IF NOT EXISTS "Payments"."__PaymentsMigrationHistory" (
    "MigrationId" varchar(150) PRIMARY KEY,
    "ProductVersion" varchar(32) NOT NULL
);

-- Reference data is structural only. Provider accounts, routes, and wallet accounts
-- remain deployment configuration. Exponents follow the schema decision ledger.
CREATE TABLE "Payments"."Currencies" (
    "Code" varchar(3) PRIMARY KEY,
    "Exponent" smallint NOT NULL CHECK ("Exponent" IN (0, 2, 3)),
    "Name" varchar(64) NOT NULL,
    CONSTRAINT "CK_Currencies_Code" CHECK ("Code" ~ '^[A-Z]{3}$')
);
INSERT INTO "Payments"."Currencies" ("Code", "Exponent", "Name") VALUES
    ('USD', 2, 'US Dollar'), ('SYP', 2, 'Syrian Pound'), ('EUR', 2, 'Euro'),
    ('GBP', 2, 'Pound Sterling'), ('KWD', 3, 'Kuwaiti Dinar'),
    ('BHD', 3, 'Bahraini Dinar'), ('OMR', 3, 'Omani Rial'),
    ('JPY', 0, 'Yen'), ('ISK', 0, 'Iceland Krona');

CREATE TABLE "Payments"."PaymentProviders" (
    "Name" varchar(32) PRIMARY KEY,
    "DisplayName" varchar(64) NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "CK_PaymentProviders_Name" CHECK ("Name" ~ '^[a-z0-9]+(-[a-z0-9]+)*$')
);
INSERT INTO "Payments"."PaymentProviders" ("Name", "DisplayName", "IsEnabled", "CreatedAt") VALUES
    ('stripe', 'Stripe', false, now()), ('sham-cash', 'ShamCash', false, now());

CREATE TABLE "Payments"."ProviderAccounts" (
    "Id" uuid PRIMARY KEY,
    "ProviderName" varchar(32) NOT NULL REFERENCES "Payments"."PaymentProviders"("Name") ON DELETE RESTRICT,
    "Environment" varchar(8) NOT NULL CHECK ("Environment" IN ('Test', 'Live')),
    "ExternalAccountId" varchar(255) NOT NULL,
    "Status" varchar(16) NOT NULL CHECK ("Status" IN ('Active', 'Degraded', 'Disabled')),
    "CredentialSecretName" varchar(255) NOT NULL,
    "WebhookSecretName" varchar(255) NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz,
    CONSTRAINT "UQ_ProviderAccounts_Identity" UNIQUE ("ProviderName", "Environment", "ExternalAccountId")
);
CREATE INDEX "IX_ProviderAccounts_ProviderName" ON "Payments"."ProviderAccounts" ("ProviderName");

CREATE TABLE "Payments"."ProviderCurrencyRoutes" (
    "Id" uuid PRIMARY KEY,
    "ProviderAccountId" uuid NOT NULL REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "IntegrationCurrency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "SettlementCurrency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "PresentmentMode" varchar(16) NOT NULL CHECK ("PresentmentMode" IN ('Exact', 'Adaptive')),
    "IsEnabled" boolean NOT NULL,
    "ShortHoldDays" smallint NOT NULL CHECK ("ShortHoldDays" >= 0),
    "ReserveRate" numeric(5,4) NOT NULL CHECK ("ReserveRate" BETWEEN 0 AND 1),
    "DisputeHoldDays" smallint NOT NULL CHECK ("DisputeHoldDays" >= 0),
    "RequiresDailyReconciliation" boolean NOT NULL,
    CONSTRAINT "UQ_ProviderCurrencyRoutes_Account_IntegrationCurrency" UNIQUE ("ProviderAccountId", "IntegrationCurrency"),
    CONSTRAINT "CK_ProviderCurrencyRoutes_IntegrationSettlementCurrency" CHECK ("IntegrationCurrency" = "SettlementCurrency")
);
CREATE INDEX "IX_ProviderCurrencyRoutes_ProviderAccountId" ON "Payments"."ProviderCurrencyRoutes" ("ProviderAccountId");
CREATE INDEX "IX_ProviderCurrencyRoutes_IntegrationCurrency" ON "Payments"."ProviderCurrencyRoutes" ("IntegrationCurrency");
CREATE INDEX "IX_ProviderCurrencyRoutes_SettlementCurrency" ON "Payments"."ProviderCurrencyRoutes" ("SettlementCurrency");

CREATE TABLE "Payments"."Products" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "ItemType" varchar(32) NOT NULL DEFAULT 'CoursePlan',
    "ItemId" uuid NOT NULL,
    "ParentContainerId" uuid,
    "TargetResourceArn" varchar(512) NOT NULL,
    "Title" varchar(255) NOT NULL,
    "SourceAvailability" varchar(16) NOT NULL DEFAULT 'Unavailable' CHECK ("SourceAvailability" IN ('Available', 'Unavailable')),
    "SourceVersion" bigint NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz,
    CONSTRAINT "UQ_Products_Tenant_TargetResourceArn" UNIQUE ("TenantId", "TargetResourceArn"),
    CONSTRAINT "UQ_Products_Tenant_Item" UNIQUE ("TenantId", "ItemType", "ItemId"),
    CONSTRAINT "CK_Products_CoursePlanParent" CHECK ("ItemType" <> 'CoursePlan' OR "ParentContainerId" IS NOT NULL)
);
CREATE INDEX "IX_Products_Tenant_ParentContainerId" ON "Payments"."Products" ("TenantId", "ParentContainerId");

CREATE TABLE "Payments"."Prices" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "ProductId" uuid NOT NULL REFERENCES "Payments"."Products"("Id") ON DELETE RESTRICT,
    "Amount" numeric(18,4) NOT NULL CHECK ("Amount" > 0),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "IsActive" boolean NOT NULL,
    "ArchivedAt" timestamptz,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "CK_Prices_ActiveArchived" CHECK ("IsActive" = ("ArchivedAt" IS NULL)),
    CONSTRAINT "CK_Prices_CurrencyLength" CHECK (char_length("Currency") = 3)
);
CREATE UNIQUE INDEX "UQ_Prices_Product_Currency_Active" ON "Payments"."Prices" ("ProductId", "Currency") WHERE "IsActive";
CREATE INDEX "IX_Prices_Tenant_ProductId" ON "Payments"."Prices" ("TenantId", "ProductId");
CREATE INDEX "IX_Prices_ProductId" ON "Payments"."Prices" ("ProductId");
CREATE INDEX "IX_Prices_Currency" ON "Payments"."Prices" ("Currency");

CREATE TABLE "Payments"."AcademyPaymentSettings" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL UNIQUE,
    "OnlineSellingEnabled" boolean NOT NULL DEFAULT false,
    "CommissionShare" numeric(5,4) NOT NULL CHECK ("CommissionShare" BETWEEN 0 AND 1),
    "RefundWindowDays" smallint CHECK ("RefundWindowDays" BETWEEN 0 AND 365),
    "UpdatedByUserId" uuid,
    "UpdatedAt" timestamptz,
    "CreatedAt" timestamptz NOT NULL
);

CREATE TABLE "Payments"."CheckoutSessions" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ProductId" uuid NOT NULL REFERENCES "Payments"."Products"("Id") ON DELETE RESTRICT,
    "PriceId" uuid NOT NULL REFERENCES "Payments"."Prices"("Id") ON DELETE RESTRICT,
    "ProviderCurrencyRouteId" uuid NOT NULL REFERENCES "Payments"."ProviderCurrencyRoutes"("Id") ON DELETE RESTRICT,
    "ExternalCheckoutId" varchar(255),
    "Status" varchar(32) NOT NULL CHECK ("Status" IN ('Pending', 'Completed', 'Expired')),
    "CreatedAt" timestamptz NOT NULL,
    "CompletedAt" timestamptz,
    "ExpiredAt" timestamptz,
    CONSTRAINT "CK_CheckoutSessions_CompletedAt" CHECK ("Status" <> 'Completed' OR "CompletedAt" IS NOT NULL),
    CONSTRAINT "CK_CheckoutSessions_ExpiredAt" CHECK ("Status" <> 'Expired' OR "ExpiredAt" IS NOT NULL),
    CONSTRAINT "CK_CheckoutSessions_PendingTimestamps" CHECK ("Status" <> 'Pending' OR ("CompletedAt" IS NULL AND "ExpiredAt" IS NULL))
);
CREATE UNIQUE INDEX "UQ_CheckoutSessions_User_Product_Pending" ON "Payments"."CheckoutSessions" ("UserId", "ProductId") WHERE "Status" = 'Pending';
CREATE UNIQUE INDEX "UQ_CheckoutSessions_Route_ExternalId" ON "Payments"."CheckoutSessions" ("ProviderCurrencyRouteId", "ExternalCheckoutId") WHERE "ExternalCheckoutId" IS NOT NULL;
CREATE INDEX "IX_CheckoutSessions_Tenant_User_CreatedAt" ON "Payments"."CheckoutSessions" ("TenantId", "UserId", "CreatedAt");
CREATE INDEX "IX_CheckoutSessions_ProductId" ON "Payments"."CheckoutSessions" ("ProductId");
CREATE INDEX "IX_CheckoutSessions_PriceId" ON "Payments"."CheckoutSessions" ("PriceId");
CREATE INDEX "IX_CheckoutSessions_ProviderCurrencyRouteId" ON "Payments"."CheckoutSessions" ("ProviderCurrencyRouteId");

CREATE TABLE "Payments"."ProviderTransactions" (
    "Id" uuid PRIMARY KEY,
    "ProviderAccountId" uuid NOT NULL REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "WebhookEventId" uuid,
    "ExternalTransactionId" varchar(255) NOT NULL,
    "ExternalPaymentId" varchar(255),
    "ExternalObjectId" varchar(255),
    "Type" varchar(24) NOT NULL CHECK ("Type" IN ('Capture', 'Refund', 'Payout', 'DisputeWithdrawal', 'DisputeReinstatement', 'Transfer')),
    "Direction" varchar(8) NOT NULL CHECK ("Direction" IN ('Incoming', 'Outgoing')),
    "Amount" numeric(18,4) NOT NULL CHECK ("Amount" > 0),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "FeeAmount" numeric(18,4) NOT NULL DEFAULT 0 CHECK ("FeeAmount" >= 0),
    "OccurredAt" timestamptz NOT NULL,
    "RecordedAt" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "UQ_ProviderTransactions_Account_ExternalTransactionId" UNIQUE ("ProviderAccountId", "ExternalTransactionId")
);
CREATE INDEX "IX_ProviderTransactions_ProviderAccountId_OccurredAt" ON "Payments"."ProviderTransactions" ("ProviderAccountId", "OccurredAt");
CREATE INDEX "IX_ProviderTransactions_ProviderAccountId_ExternalPaymentId" ON "Payments"."ProviderTransactions" ("ProviderAccountId", "ExternalPaymentId");
CREATE INDEX "IX_ProviderTransactions_ExternalObjectId" ON "Payments"."ProviderTransactions" ("ExternalObjectId") WHERE "ExternalObjectId" IS NOT NULL;
CREATE INDEX "IX_ProviderTransactions_WebhookEventId" ON "Payments"."ProviderTransactions" ("WebhookEventId");
CREATE INDEX "IX_ProviderTransactions_Currency" ON "Payments"."ProviderTransactions" ("Currency");

CREATE TABLE "Payments"."WebhookEvents" (
    "Id" uuid PRIMARY KEY,
    "ProviderName" varchar(32) NOT NULL REFERENCES "Payments"."PaymentProviders"("Name") ON DELETE RESTRICT,
    "ProviderAccountId" uuid REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "ExternalEventId" varchar(255) NOT NULL,
    "EventType" varchar(64) NOT NULL,
    "ExternalObjectId" varchar(255),
    "SignatureVerified" boolean NOT NULL,
    "Environment" varchar(8) NOT NULL CHECK ("Environment" IN ('Test', 'Live')),
    "Livemode" boolean,
    "DeliveryCount" integer NOT NULL DEFAULT 1 CHECK ("DeliveryCount" > 0),
    "ReceivedAt" timestamptz NOT NULL,
    "ProcessedAt" timestamptz,
    "ProcessingOutcome" varchar(32) CHECK ("ProcessingOutcome" IN ('Processed', 'Duplicate', 'Failed', 'Skipped')),
    CONSTRAINT "UQ_WebhookEvents_Provider_ExternalEventId" UNIQUE ("ProviderName", "ExternalEventId")
);
CREATE INDEX "IX_WebhookEvents_Provider_EventType_ReceivedAt" ON "Payments"."WebhookEvents" ("ProviderName", "EventType", "ReceivedAt");
CREATE INDEX "IX_WebhookEvents_ExternalObjectId" ON "Payments"."WebhookEvents" ("ExternalObjectId") WHERE "ExternalObjectId" IS NOT NULL;
CREATE INDEX "IX_WebhookEvents_ProcessingOutcome_Null" ON "Payments"."WebhookEvents" ("ProcessingOutcome") WHERE "ProcessingOutcome" IS NULL;
CREATE INDEX "IX_WebhookEvents_ProviderAccountId" ON "Payments"."WebhookEvents" ("ProviderAccountId");
CREATE INDEX "IX_WebhookEvents_ProviderName" ON "Payments"."WebhookEvents" ("ProviderName");

CREATE TABLE "Payments"."WebhookEventPayloads" (
    "EventId" uuid PRIMARY KEY REFERENCES "Payments"."WebhookEvents"("Id") ON DELETE RESTRICT,
    "Payload" jsonb,
    "ArchiveObjectKey" varchar(512),
    "ContentHash" varchar(64),
    "ArchiveState" varchar(16) NOT NULL DEFAULT 'Hot' CHECK ("ArchiveState" IN ('Hot', 'Uploading', 'Archived', 'Redacted')),
    "ArchivedAt" timestamptz,
    "RedactedAt" timestamptz,
    "LastArchiveAttemptAt" timestamptz,
    CONSTRAINT "CK_WebhookEventPayloads_Lifecycle" CHECK (
        ("ArchiveState" = 'Hot' AND "Payload" IS NOT NULL AND "ArchiveObjectKey" IS NULL AND "ContentHash" IS NULL AND "ArchivedAt" IS NULL AND "RedactedAt" IS NULL)
        OR ("ArchiveState" = 'Uploading' AND "Payload" IS NOT NULL AND "RedactedAt" IS NULL)
        OR ("ArchiveState" = 'Archived' AND "ArchiveObjectKey" IS NOT NULL AND "ContentHash" ~ '^[0-9a-f]{64}$' AND "ArchivedAt" IS NOT NULL AND "Payload" IS NULL AND "RedactedAt" IS NULL)
        OR ("ArchiveState" = 'Redacted' AND "RedactedAt" IS NOT NULL)
    )
);
CREATE INDEX "IX_WebhookEventPayloads_ArchiveState_LastAttempt" ON "Payments"."WebhookEventPayloads" ("ArchiveState", "LastArchiveAttemptAt");

CREATE TABLE "Payments"."LedgerAccounts" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(64) NOT NULL,
    "Role" varchar(32) NOT NULL CHECK ("Role" IN ('ProviderAsset', 'PlatformRevenue', 'ProviderFees', 'Bank', 'Cash', 'UnappliedFunds', 'AcademyPayable')),
    "Type" varchar(16) NOT NULL CHECK ("Type" IN ('Asset', 'Liability', 'Income', 'Expense')),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "TenantId" uuid,
    "ProviderAccountId" uuid REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "CK_LedgerAccounts_RoleType" CHECK (
        ("Role" IN ('ProviderAsset', 'Bank', 'Cash') AND "Type" = 'Asset') OR
        ("Role" = 'PlatformRevenue' AND "Type" = 'Income') OR
        ("Role" = 'ProviderFees' AND "Type" = 'Expense') OR
        ("Role" IN ('UnappliedFunds', 'AcademyPayable') AND "Type" = 'Liability')
    ),
    CONSTRAINT "CK_LedgerAccounts_TenantProvider" CHECK (
        ("Role" = 'AcademyPayable' AND "TenantId" IS NOT NULL AND "ProviderAccountId" IS NOT NULL) OR
        ("Role" IN ('ProviderAsset', 'PlatformRevenue', 'ProviderFees', 'UnappliedFunds') AND "TenantId" IS NULL AND "ProviderAccountId" IS NOT NULL) OR
        ("Role" IN ('Bank', 'Cash') AND "TenantId" IS NULL AND "ProviderAccountId" IS NULL)
    )
);
CREATE UNIQUE INDEX "UQ_LedgerAccounts_Structural" ON "Payments"."LedgerAccounts" ("Role", "ProviderAccountId", "Currency") WHERE "TenantId" IS NULL AND "ProviderAccountId" IS NOT NULL;
CREATE UNIQUE INDEX "UQ_LedgerAccounts_BankCash" ON "Payments"."LedgerAccounts" ("Role", "Currency") WHERE "ProviderAccountId" IS NULL AND "TenantId" IS NULL;
CREATE UNIQUE INDEX "UQ_LedgerAccounts_AcademyPayable" ON "Payments"."LedgerAccounts" ("TenantId", "ProviderAccountId", "Currency") WHERE "Role" = 'AcademyPayable';
CREATE INDEX "IX_LedgerAccounts_Currency" ON "Payments"."LedgerAccounts" ("Currency");
CREATE INDEX "IX_LedgerAccounts_ProviderAccountId" ON "Payments"."LedgerAccounts" ("ProviderAccountId");

CREATE TABLE "Payments"."Purchases" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ProductId" uuid NOT NULL REFERENCES "Payments"."Products"("Id") ON DELETE RESTRICT,
    "PriceId" uuid NOT NULL REFERENCES "Payments"."Prices"("Id") ON DELETE RESTRICT,
    "CheckoutSessionId" uuid NOT NULL UNIQUE REFERENCES "Payments"."CheckoutSessions"("Id") ON DELETE RESTRICT,
    "ProviderAccountId" uuid NOT NULL REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "ProviderTransactionId" uuid NOT NULL UNIQUE REFERENCES "Payments"."ProviderTransactions"("Id") ON DELETE RESTRICT,
    "ItemTitle" varchar(255) NOT NULL,
    "PresentmentAmount" numeric(18,4) NOT NULL CHECK ("PresentmentAmount" > 0),
    "PresentmentCurrency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "SettlementAmount" numeric(18,4) NOT NULL CHECK ("SettlementAmount" > 0),
    "SettlementCurrency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "FxRate" numeric(18,8) CHECK ("FxRate" IS NULL OR "FxRate" > 0),
    "ProviderFee" numeric(18,4) NOT NULL CHECK ("ProviderFee" >= 0),
    "CommissionShare" numeric(5,4) NOT NULL CHECK ("CommissionShare" BETWEEN 0 AND 1),
    "ExternalPaymentId" varchar(255) NOT NULL,
    "Status" varchar(32) NOT NULL CHECK ("Status" IN ('Paid', 'PartiallyRefunded', 'Refunded')),
    "PaidAt" timestamptz NOT NULL,
    "RefundedAt" timestamptz,
    CONSTRAINT "CK_Purchases_RefundedAt" CHECK (("Status" = 'Refunded') = ("RefundedAt" IS NOT NULL)),
    CONSTRAINT "CK_Purchases_FxRate" CHECK (("PresentmentCurrency" = "SettlementCurrency") = ("FxRate" IS NULL)),
    CONSTRAINT "UQ_Purchases_Provider_ExternalPaymentId" UNIQUE ("ProviderAccountId", "ExternalPaymentId")
);
CREATE INDEX "IX_Purchases_Tenant_PaidAt" ON "Payments"."Purchases" ("TenantId", "PaidAt" DESC);
CREATE INDEX "IX_Purchases_Tenant_Status_PaidAt" ON "Payments"."Purchases" ("TenantId", "Status", "PaidAt" DESC);
CREATE INDEX "IX_Purchases_Tenant_Product_PaidAt" ON "Payments"."Purchases" ("TenantId", "ProductId", "PaidAt");
CREATE INDEX "IX_Purchases_Tenant_User_PaidAt" ON "Payments"."Purchases" ("TenantId", "UserId", "PaidAt");
CREATE INDEX "IX_Purchases_ProviderAccount_PaidAt" ON "Payments"."Purchases" ("ProviderAccountId", "PaidAt");
CREATE INDEX "IX_Purchases_ProviderAccountId" ON "Payments"."Purchases" ("ProviderAccountId");
CREATE INDEX "IX_Purchases_ProductId" ON "Payments"."Purchases" ("ProductId");
CREATE INDEX "IX_Purchases_PriceId" ON "Payments"."Purchases" ("PriceId");
CREATE INDEX "IX_Purchases_PresentmentCurrency" ON "Payments"."Purchases" ("PresentmentCurrency");
CREATE INDEX "IX_Purchases_SettlementCurrency" ON "Payments"."Purchases" ("SettlementCurrency");

CREATE TABLE "Payments"."PaymentReviewCases" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid,
    "ProviderAccountId" uuid NOT NULL REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "PayableAccountId" uuid REFERENCES "Payments"."LedgerAccounts"("Id") ON DELETE RESTRICT,
    "CheckoutSessionId" uuid REFERENCES "Payments"."CheckoutSessions"("Id") ON DELETE RESTRICT,
    "PurchaseId" uuid REFERENCES "Payments"."Purchases"("Id") ON DELETE RESTRICT,
    "ProviderTransactionId" uuid REFERENCES "Payments"."ProviderTransactions"("Id") ON DELETE RESTRICT,
    "Amount" numeric(18,4) NOT NULL CHECK ("Amount" > 0),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "SourceAccountId" uuid REFERENCES "Payments"."LedgerAccounts"("Id") ON DELETE RESTRICT,
    "DestinationSnapshot" jsonb,
    "ReasonCode" varchar(32) CHECK ("ReasonCode" IN ('RequestedByCustomer', 'Duplicate', 'Fraudulent', 'Other')),
    "RevokeAccess" boolean,
    "Type" varchar(32) NOT NULL CHECK ("Type" IN ('UnappliedReturn', 'ApplyUnappliedFunds', 'ProviderReversal', 'ManualCorrection', 'Settlement')),
    "Status" varchar(16) NOT NULL CHECK ("Status" IN ('Open', 'Approved', 'Rejected', 'Resolved')),
    "OpenedByUserId" uuid NOT NULL,
    "ApprovedByUserId" uuid,
    "ProviderSupportReference" varchar(255),
    "EvidenceReference" varchar(512) NOT NULL,
    "Resolution" varchar(1024),
    "CreatedAt" timestamptz NOT NULL,
    "ResolvedAt" timestamptz,
    CONSTRAINT "CK_PaymentReviewCases_StatusCoherence" CHECK (
        ("Status" = 'Open' AND "ApprovedByUserId" IS NULL AND "Resolution" IS NULL AND "ResolvedAt" IS NULL) OR
        ("Status" IN ('Approved', 'Rejected') AND "ApprovedByUserId" IS NOT NULL AND "ApprovedByUserId" <> "OpenedByUserId" AND "ResolvedAt" IS NULL) OR
        ("Status" = 'Resolved' AND "ApprovedByUserId" IS NOT NULL AND "ApprovedByUserId" <> "OpenedByUserId" AND "Resolution" IS NOT NULL AND "ResolvedAt" IS NOT NULL)
    ),
    CONSTRAINT "CK_PaymentReviewCases_TypeFields" CHECK (
        (("Type" IN ('UnappliedReturn', 'ApplyUnappliedFunds') AND "ProviderTransactionId" IS NOT NULL) OR "Type" NOT IN ('UnappliedReturn', 'ApplyUnappliedFunds')) AND
        (("Type" = 'ProviderReversal' AND "PurchaseId" IS NOT NULL AND "ReasonCode" IS NOT NULL AND "RevokeAccess" IS NOT NULL) OR ("Type" <> 'ProviderReversal' AND "ReasonCode" IS NULL AND "RevokeAccess" IS NULL)) AND
        (("Type" = 'Settlement' AND "TenantId" IS NOT NULL AND "PayableAccountId" IS NOT NULL AND "SourceAccountId" IS NOT NULL AND "DestinationSnapshot" IS NOT NULL) OR ("Type" <> 'Settlement' AND "PayableAccountId" IS NULL AND "SourceAccountId" IS NULL AND "DestinationSnapshot" IS NULL))
    )
);
CREATE UNIQUE INDEX "UQ_PaymentReviewCases_Open_ProviderTransaction" ON "Payments"."PaymentReviewCases" ("ProviderTransactionId") WHERE "ProviderTransactionId" IS NOT NULL AND "Status" = 'Open';
CREATE UNIQUE INDEX "UQ_PaymentReviewCases_InFlightSettlement" ON "Payments"."PaymentReviewCases" ("PayableAccountId") WHERE "Type" = 'Settlement' AND "Status" IN ('Open', 'Approved');
CREATE INDEX "IX_PaymentReviewCases_ProviderAccountId" ON "Payments"."PaymentReviewCases" ("ProviderAccountId");
CREATE INDEX "IX_PaymentReviewCases_PayableAccountId" ON "Payments"."PaymentReviewCases" ("PayableAccountId");
CREATE INDEX "IX_PaymentReviewCases_CheckoutSessionId" ON "Payments"."PaymentReviewCases" ("CheckoutSessionId");
CREATE INDEX "IX_PaymentReviewCases_PurchaseId" ON "Payments"."PaymentReviewCases" ("PurchaseId");
CREATE INDEX "IX_PaymentReviewCases_ProviderTransactionId" ON "Payments"."PaymentReviewCases" ("ProviderTransactionId");
CREATE INDEX "IX_PaymentReviewCases_Currency" ON "Payments"."PaymentReviewCases" ("Currency");
CREATE INDEX "IX_PaymentReviewCases_SourceAccountId" ON "Payments"."PaymentReviewCases" ("SourceAccountId");

CREATE TABLE "Payments"."LedgerEntries" (
    "Id" uuid PRIMARY KEY,
    "PostedAt" timestamptz NOT NULL DEFAULT now(),
    "EffectiveAt" timestamptz,
    "Type" varchar(32) NOT NULL CHECK ("Type" IN ('Capture', 'RefundReversal', 'Settlement', 'ProviderPayout', 'DisputeLoss', 'DisputeWon', 'UnappliedReceipt', 'ApplyUnappliedFunds', 'ReturnUnappliedFunds', 'ManualAdjustment')),
    "TenantId" uuid,
    "ProviderAccountId" uuid REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "ProviderTransactionId" uuid REFERENCES "Payments"."ProviderTransactions"("Id") ON DELETE RESTRICT,
    "PurchaseId" uuid REFERENCES "Payments"."Purchases"("Id") ON DELETE RESTRICT,
    "RefundId" uuid,
    "SettlementId" uuid,
    "DisputeId" uuid,
    "PaymentReviewCaseId" uuid REFERENCES "Payments"."PaymentReviewCases"("Id") ON DELETE RESTRICT,
    "ExternalObjectId" varchar(255),
    "CreatedByUserId" uuid,
    "Description" varchar(256),
    CONSTRAINT "CK_LedgerEntries_ReferenceSet" CHECK (
        ("Type" = 'Capture' AND "PurchaseId" IS NOT NULL AND "ProviderAccountId" IS NOT NULL AND "ProviderTransactionId" IS NOT NULL AND "RefundId" IS NULL AND "SettlementId" IS NULL AND "DisputeId" IS NULL AND "PaymentReviewCaseId" IS NULL) OR
        ("Type" = 'RefundReversal' AND "PurchaseId" IS NOT NULL AND "RefundId" IS NOT NULL AND "ProviderAccountId" IS NOT NULL AND "ProviderTransactionId" IS NOT NULL AND "SettlementId" IS NULL AND "DisputeId" IS NULL AND "PaymentReviewCaseId" IS NULL) OR
        ("Type" = 'Settlement' AND "SettlementId" IS NOT NULL AND "ProviderAccountId" IS NOT NULL AND "ProviderTransactionId" IS NULL AND "PurchaseId" IS NULL AND "RefundId" IS NULL AND "DisputeId" IS NULL AND "PaymentReviewCaseId" IS NOT NULL) OR
        ("Type" = 'ProviderPayout' AND "ProviderAccountId" IS NOT NULL AND "ProviderTransactionId" IS NOT NULL AND "PurchaseId" IS NULL AND "RefundId" IS NULL AND "SettlementId" IS NULL AND "DisputeId" IS NULL AND "PaymentReviewCaseId" IS NULL) OR
        ("Type" IN ('DisputeLoss', 'DisputeWon') AND "PurchaseId" IS NOT NULL AND "DisputeId" IS NOT NULL AND "ProviderAccountId" IS NOT NULL AND "ProviderTransactionId" IS NOT NULL AND "RefundId" IS NULL AND "SettlementId" IS NULL AND "PaymentReviewCaseId" IS NULL) OR
        ("Type" = 'UnappliedReceipt' AND "ProviderAccountId" IS NOT NULL AND "ProviderTransactionId" IS NOT NULL AND "PurchaseId" IS NULL AND "RefundId" IS NULL AND "SettlementId" IS NULL AND "DisputeId" IS NULL AND "PaymentReviewCaseId" IS NULL) OR
        ("Type" = 'ApplyUnappliedFunds' AND "PurchaseId" IS NOT NULL AND "ProviderAccountId" IS NOT NULL AND "ProviderTransactionId" IS NOT NULL AND "PaymentReviewCaseId" IS NOT NULL AND "RefundId" IS NULL AND "SettlementId" IS NULL AND "DisputeId" IS NULL) OR
        ("Type" = 'ReturnUnappliedFunds' AND "ProviderAccountId" IS NOT NULL AND "ProviderTransactionId" IS NOT NULL AND "PaymentReviewCaseId" IS NOT NULL AND "PurchaseId" IS NULL AND "RefundId" IS NULL AND "SettlementId" IS NULL AND "DisputeId" IS NULL) OR
        ("Type" = 'ManualAdjustment' AND "CreatedByUserId" IS NOT NULL AND "Description" IS NOT NULL AND "PaymentReviewCaseId" IS NOT NULL AND "ProviderTransactionId" IS NULL AND "PurchaseId" IS NULL AND "RefundId" IS NULL AND "SettlementId" IS NULL AND "DisputeId" IS NULL)
    ),
    CONSTRAINT "CK_LedgerEntries_UnappliedDescription" CHECK ("Type" <> 'UnappliedReceipt' OR "Description" IS NOT NULL)
);
CREATE INDEX "IX_LedgerEntries_PurchaseId" ON "Payments"."LedgerEntries" ("PurchaseId");
CREATE INDEX "IX_LedgerEntries_ProviderAccount_Type_PostedAt" ON "Payments"."LedgerEntries" ("ProviderAccountId", "Type", "PostedAt");
CREATE INDEX "IX_LedgerEntries_ProviderTransactionId" ON "Payments"."LedgerEntries" ("ProviderTransactionId");
CREATE INDEX "IX_LedgerEntries_RefundId" ON "Payments"."LedgerEntries" ("RefundId");
CREATE INDEX "IX_LedgerEntries_SettlementId" ON "Payments"."LedgerEntries" ("SettlementId");
CREATE INDEX "IX_LedgerEntries_DisputeId" ON "Payments"."LedgerEntries" ("DisputeId");
CREATE INDEX "IX_LedgerEntries_PaymentReviewCaseId" ON "Payments"."LedgerEntries" ("PaymentReviewCaseId");
CREATE INDEX "IX_LedgerEntries_TenantId" ON "Payments"."LedgerEntries" ("TenantId") WHERE "TenantId" IS NOT NULL;
CREATE UNIQUE INDEX "UQ_LedgerEntries_Purchase_Funding" ON "Payments"."LedgerEntries" ("PurchaseId") WHERE "Type" IN ('Capture', 'ApplyUnappliedFunds');
CREATE UNIQUE INDEX "UQ_LedgerEntries_Refund" ON "Payments"."LedgerEntries" ("RefundId") WHERE "Type" = 'RefundReversal';
CREATE UNIQUE INDEX "UQ_LedgerEntries_Dispute_Type" ON "Payments"."LedgerEntries" ("DisputeId", "Type") WHERE "DisputeId" IS NOT NULL;
CREATE UNIQUE INDEX "UQ_LedgerEntries_Settlement" ON "Payments"."LedgerEntries" ("SettlementId") WHERE "SettlementId" IS NOT NULL;
CREATE UNIQUE INDEX "UQ_LedgerEntries_ProviderTransaction_Type" ON "Payments"."LedgerEntries" ("ProviderTransactionId", "Type") WHERE "ProviderTransactionId" IS NOT NULL;

CREATE TABLE "Payments"."LedgerLines" (
    "Id" uuid PRIMARY KEY,
    "EntryId" uuid NOT NULL REFERENCES "Payments"."LedgerEntries"("Id") ON DELETE RESTRICT,
    "AccountId" uuid NOT NULL REFERENCES "Payments"."LedgerAccounts"("Id") ON DELETE RESTRICT,
    "Direction" varchar(8) NOT NULL CHECK ("Direction" IN ('Debit', 'Credit')),
    "Amount" numeric(18,4) NOT NULL CHECK ("Amount" > 0),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "TenantId" uuid
);
CREATE INDEX "IX_LedgerLines_Account_Direction" ON "Payments"."LedgerLines" ("AccountId", "Direction") INCLUDE ("Amount");
CREATE INDEX "IX_LedgerLines_EntryId" ON "Payments"."LedgerLines" ("EntryId");
CREATE INDEX "IX_LedgerLines_Tenant_Account" ON "Payments"."LedgerLines" ("TenantId", "AccountId") WHERE "TenantId" IS NOT NULL;
CREATE INDEX "IX_LedgerLines_Currency" ON "Payments"."LedgerLines" ("Currency");

CREATE TABLE "Payments"."Refunds" (
    "Id" uuid PRIMARY KEY,
    "PurchaseId" uuid NOT NULL REFERENCES "Payments"."Purchases"("Id") ON DELETE RESTRICT,
    "TenantId" uuid NOT NULL,
    "ProviderAccountId" uuid NOT NULL REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "ProviderTransactionId" uuid REFERENCES "Payments"."ProviderTransactions"("Id") ON DELETE RESTRICT,
    "PaymentReviewCaseId" uuid REFERENCES "Payments"."PaymentReviewCases"("Id") ON DELETE RESTRICT,
    "Source" varchar(24) NOT NULL CHECK ("Source" IN ('Platform', 'Provider', 'ProviderSupport')),
    "RequestedByUserId" uuid,
    "Reason" varchar(512),
    "ReasonCode" varchar(32) NOT NULL DEFAULT 'Other' CHECK ("ReasonCode" IN ('RequestedByCustomer', 'Duplicate', 'Fraudulent', 'Other')),
    "RevokeAccess" boolean NOT NULL DEFAULT false,
    "Amount" numeric(18,4) NOT NULL CHECK ("Amount" > 0),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "ExternalRefundId" varchar(255),
    "ProviderFeeRefunded" numeric(18,4) NOT NULL DEFAULT 0 CHECK ("ProviderFeeRefunded" >= 0),
    "Status" varchar(32) NOT NULL CHECK ("Status" IN ('Pending', 'Succeeded', 'Failed')),
    "CreatedAt" timestamptz NOT NULL,
    "CompletedAt" timestamptz,
    CONSTRAINT "CK_Refunds_TerminalTimestamp" CHECK (("Status" = 'Pending') = ("CompletedAt" IS NULL)),
    CONSTRAINT "CK_Refunds_SucceededMovement" CHECK ("Status" <> 'Succeeded' OR ("ProviderTransactionId" IS NOT NULL AND ("ExternalRefundId" IS NOT NULL OR ("Source" = 'ProviderSupport' AND "PaymentReviewCaseId" IS NOT NULL)))),
    CONSTRAINT "CK_Refunds_ProviderSupportCase" CHECK ("Source" <> 'ProviderSupport' OR "PaymentReviewCaseId" IS NOT NULL)
);
CREATE UNIQUE INDEX "UQ_Refunds_Provider_ExternalRefundId" ON "Payments"."Refunds" ("ProviderAccountId", "ExternalRefundId") WHERE "ExternalRefundId" IS NOT NULL;
CREATE INDEX "IX_Refunds_PurchaseId" ON "Payments"."Refunds" ("PurchaseId");
CREATE INDEX "IX_Refunds_ProviderAccountId" ON "Payments"."Refunds" ("ProviderAccountId");
CREATE INDEX "IX_Refunds_ProviderTransactionId" ON "Payments"."Refunds" ("ProviderTransactionId");
CREATE INDEX "IX_Refunds_PaymentReviewCaseId" ON "Payments"."Refunds" ("PaymentReviewCaseId");
CREATE INDEX "IX_Refunds_Currency" ON "Payments"."Refunds" ("Currency");

CREATE TABLE "Payments"."RefundRequests" (
    "Id" uuid PRIMARY KEY,
    "PurchaseId" uuid NOT NULL REFERENCES "Payments"."Purchases"("Id") ON DELETE RESTRICT,
    "TenantId" uuid NOT NULL,
    "RequestedByUserId" uuid NOT NULL,
    "Reason" varchar(512),
    "Status" varchar(32) NOT NULL CHECK ("Status" IN ('Requested', 'Approved', 'Rejected', 'Cancelled')),
    "ApprovedAmount" numeric(18,4),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "DecidedByUserId" uuid,
    "DecidedAt" timestamptz,
    "DecisionNotes" varchar(1024),
    "RefundId" uuid UNIQUE REFERENCES "Payments"."Refunds"("Id") ON DELETE RESTRICT,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "CK_RefundRequests_Approved" CHECK ("Status" <> 'Approved' OR ("ApprovedAmount" IS NOT NULL AND "DecidedByUserId" IS NOT NULL AND "DecidedAt" IS NOT NULL AND "RefundId" IS NOT NULL)),
    CONSTRAINT "CK_RefundRequests_Requested" CHECK ("Status" <> 'Requested' OR ("ApprovedAmount" IS NULL AND "DecidedByUserId" IS NULL AND "DecidedAt" IS NULL AND "RefundId" IS NULL)),
    CONSTRAINT "CK_RefundRequests_Rejected" CHECK ("Status" <> 'Rejected' OR ("DecidedByUserId" IS NOT NULL AND "DecidedAt" IS NOT NULL AND "ApprovedAmount" IS NULL AND "RefundId" IS NULL)),
    CONSTRAINT "CK_RefundRequests_Cancelled" CHECK ("Status" <> 'Cancelled' OR ("ApprovedAmount" IS NULL AND "RefundId" IS NULL AND "DecidedByUserId" IS NULL AND "DecidedAt" IS NULL)),
    CONSTRAINT "CK_RefundRequests_ApprovedAmount" CHECK ("ApprovedAmount" IS NULL OR "ApprovedAmount" > 0),
    CONSTRAINT "CK_RefundRequests_RefundIdStatus" CHECK (("Status" = 'Approved') = ("RefundId" IS NOT NULL))
);
CREATE UNIQUE INDEX "UQ_RefundRequests_Purchase_Requested" ON "Payments"."RefundRequests" ("PurchaseId") WHERE "Status" = 'Requested';
CREATE INDEX "IX_RefundRequests_Tenant_Status_CreatedAt" ON "Payments"."RefundRequests" ("TenantId", "Status", "CreatedAt");
CREATE INDEX "IX_RefundRequests_PurchaseId" ON "Payments"."RefundRequests" ("PurchaseId");
CREATE INDEX "IX_RefundRequests_Currency" ON "Payments"."RefundRequests" ("Currency");

CREATE TABLE "Payments"."Disputes" (
    "Id" uuid PRIMARY KEY,
    "PurchaseId" uuid NOT NULL REFERENCES "Payments"."Purchases"("Id") ON DELETE RESTRICT,
    "TenantId" uuid NOT NULL,
    "ProviderAccountId" uuid NOT NULL REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "ExternalDisputeId" varchar(255) NOT NULL,
    "Status" varchar(32) NOT NULL CHECK ("Status" IN ('Open', 'Won', 'Lost')),
    "Reason" varchar(64) NOT NULL,
    "Amount" numeric(18,4) NOT NULL CHECK ("Amount" > 0),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "ProviderDisputeFee" numeric(18,4) NOT NULL DEFAULT 0 CHECK ("ProviderDisputeFee" >= 0),
    "OpenedAt" timestamptz NOT NULL,
    "ResolvedAt" timestamptz,
    "ResolvedByUserId" uuid,
    "OutcomeNotes" varchar(1024),
    CONSTRAINT "CK_Disputes_StatusResolvedAt" CHECK (("Status" = 'Open') = ("ResolvedAt" IS NULL)),
    CONSTRAINT "UQ_Disputes_Provider_ExternalDisputeId" UNIQUE ("ProviderAccountId", "ExternalDisputeId")
);
CREATE INDEX "IX_Disputes_Tenant_Status" ON "Payments"."Disputes" ("TenantId", "Status");
CREATE INDEX "IX_Disputes_PurchaseId" ON "Payments"."Disputes" ("PurchaseId");
CREATE INDEX "IX_Disputes_ProviderAccount_Status" ON "Payments"."Disputes" ("ProviderAccountId", "Status");
CREATE INDEX "IX_Disputes_Currency" ON "Payments"."Disputes" ("Currency");

CREATE TABLE "Payments"."Settlements" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "ProviderAccountId" uuid NOT NULL REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "PaymentReviewCaseId" uuid NOT NULL UNIQUE REFERENCES "Payments"."PaymentReviewCases"("Id") ON DELETE RESTRICT,
    "PayableAccountId" uuid NOT NULL REFERENCES "Payments"."LedgerAccounts"("Id") ON DELETE RESTRICT,
    "SourceAccountId" uuid NOT NULL REFERENCES "Payments"."LedgerAccounts"("Id") ON DELETE RESTRICT,
    "ProviderTransactionId" uuid REFERENCES "Payments"."ProviderTransactions"("Id") ON DELETE RESTRICT,
    "Amount" numeric(18,4) NOT NULL CHECK ("Amount" > 0),
    "Currency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "RecordedByUserId" uuid NOT NULL,
    "ApprovedByUserId" uuid NOT NULL,
    "RecordedAt" timestamptz NOT NULL,
    "ExternalReference" varchar(255) NOT NULL,
    "DestinationSnapshot" jsonb NOT NULL,
    "EvidenceReference" varchar(512) NOT NULL,
    "Notes" varchar(512),
    CONSTRAINT "CK_Settlements_MakerChecker" CHECK ("RecordedByUserId" <> "ApprovedByUserId"),
    CONSTRAINT "UQ_Settlements_Source_ExternalReference" UNIQUE ("SourceAccountId", "ExternalReference")
);
CREATE INDEX "IX_Settlements_Tenant_Provider_Currency_RecordedAt" ON "Payments"."Settlements" ("TenantId", "ProviderAccountId", "Currency", "RecordedAt") INCLUDE ("Amount");
CREATE INDEX "IX_Settlements_ProviderAccountId" ON "Payments"."Settlements" ("ProviderAccountId");
CREATE INDEX "IX_Settlements_PayableAccountId" ON "Payments"."Settlements" ("PayableAccountId");
CREATE INDEX "IX_Settlements_SourceAccountId" ON "Payments"."Settlements" ("SourceAccountId");
CREATE INDEX "IX_Settlements_ProviderTransactionId" ON "Payments"."Settlements" ("ProviderTransactionId");
CREATE INDEX "IX_Settlements_Currency" ON "Payments"."Settlements" ("Currency");

CREATE TABLE "Payments"."AcademyProviderRiskOverrides" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "ProviderCurrencyRouteId" uuid NOT NULL REFERENCES "Payments"."ProviderCurrencyRoutes"("Id") ON DELETE RESTRICT,
    "ShortHoldDays" smallint CHECK ("ShortHoldDays" >= 0),
    "ReserveRate" numeric(5,4) CHECK ("ReserveRate" BETWEEN 0 AND 1),
    "DisputeHoldDays" smallint CHECK ("DisputeHoldDays" >= 0),
    "UpdatedByUserId" uuid NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "UQ_AcademyProviderRiskOverrides_Tenant_Route" UNIQUE ("TenantId", "ProviderCurrencyRouteId")
);
CREATE INDEX "IX_AcademyProviderRiskOverrides_RouteId" ON "Payments"."AcademyProviderRiskOverrides" ("ProviderCurrencyRouteId");

CREATE TABLE "Payments"."ProductDailyPurchases" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "ProviderAccountId" uuid NOT NULL REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "ProductId" uuid NOT NULL,
    "Day" date NOT NULL,
    "SettlementCurrency" varchar(3) NOT NULL REFERENCES "Payments"."Currencies"("Code") ON DELETE RESTRICT,
    "PurchaseCount" integer NOT NULL,
    "GrossAmount" numeric(18,4) NOT NULL,
    "CommissionAmount" numeric(18,4) NOT NULL,
    "NetAcademyAmount" numeric(18,4) NOT NULL,
    "RefundCount" integer NOT NULL,
    "RefundedGrossAmount" numeric(18,4) NOT NULL,
    "RefundedCommissionAmount" numeric(18,4) NOT NULL,
    "RefundedNetAcademyAmount" numeric(18,4) NOT NULL,
    "RefreshedAt" timestamptz NOT NULL,
    CONSTRAINT "UQ_ProductDailyPurchases_Grain" UNIQUE ("TenantId", "ProviderAccountId", "ProductId", "Day", "SettlementCurrency"),
    CONSTRAINT "CK_ProductDailyPurchases_Counts" CHECK ("PurchaseCount" >= 0 AND "RefundCount" >= 0)
);
CREATE INDEX "IX_ProductDailyPurchases_Tenant_Provider_Day" ON "Payments"."ProductDailyPurchases" ("TenantId", "ProviderAccountId", "Day");
CREATE INDEX "IX_ProductDailyPurchases_ProviderAccountId" ON "Payments"."ProductDailyPurchases" ("ProviderAccountId");
CREATE INDEX "IX_ProductDailyPurchases_Currency" ON "Payments"."ProductDailyPurchases" ("SettlementCurrency");

CREATE TABLE "Payments"."PaymentsAuditLogs" (
    "Id" uuid PRIMARY KEY,
    "EventId" uuid NOT NULL UNIQUE,
    "TenantId" uuid,
    "Action" varchar(64) NOT NULL,
    "AggregateId" uuid,
    "ActorUserId" uuid,
    "Payload" jsonb,
    "OccurredAt" timestamptz NOT NULL
);
CREATE INDEX "IX_PaymentsAuditLogs_Tenant_Action_OccurredAt" ON "Payments"."PaymentsAuditLogs" ("TenantId", "Action", "OccurredAt");
CREATE INDEX "IX_PaymentsAuditLogs_AggregateId" ON "Payments"."PaymentsAuditLogs" ("AggregateId");

CREATE TABLE "Payments"."ReconciliationRuns" (
    "Id" uuid PRIMARY KEY,
    "ProviderAccountId" uuid REFERENCES "Payments"."ProviderAccounts"("Id") ON DELETE RESTRICT,
    "Kind" varchar(64) NOT NULL CHECK ("Kind" IN ('LedgerInternal', 'LedgerVsProvider', 'ProviderHealth', 'MirrorVsCourses', 'StaleSessions', 'UnappliedReceipts', 'NegativePayables', 'PiiErasure')),
    "StartedAt" timestamptz NOT NULL,
    "FinishedAt" timestamptz,
    "Status" varchar(32) NOT NULL CHECK ("Status" IN ('Running', 'Completed', 'Failed')),
    "Counts" jsonb,
    "TriggeredBy" varchar(64) NOT NULL,
    CONSTRAINT "CK_ReconciliationRuns_StatusFinishedAt" CHECK (("Status" = 'Running') = ("FinishedAt" IS NULL))
);
CREATE INDEX "IX_ReconciliationRuns_ProviderAccountId_Kind_StartedAt" ON "Payments"."ReconciliationRuns" ("ProviderAccountId", "Kind", "StartedAt" DESC);
CREATE INDEX "IX_ReconciliationRuns_ProviderAccountId" ON "Payments"."ReconciliationRuns" ("ProviderAccountId");

CREATE TABLE "Payments"."ReconciliationFindings" (
    "Id" uuid PRIMARY KEY,
    "RunId" uuid NOT NULL REFERENCES "Payments"."ReconciliationRuns"("Id") ON DELETE RESTRICT,
    "Severity" varchar(16) NOT NULL CHECK ("Severity" IN ('Info', 'Warning', 'Critical')),
    "Kind" varchar(64) NOT NULL,
    "AggregateId" uuid,
    "Detail" varchar(1024) NOT NULL,
    "ResolvedAt" timestamptz,
    "ResolvedByUserId" uuid,
    CONSTRAINT "CK_ReconciliationFindings_Resolution" CHECK (("ResolvedAt" IS NULL) = ("ResolvedByUserId" IS NULL))
);
CREATE INDEX "IX_ReconciliationFindings_RunId" ON "Payments"."ReconciliationFindings" ("RunId");
CREATE INDEX "IX_ReconciliationFindings_Severity_Kind" ON "Payments"."ReconciliationFindings" ("Severity", "Kind") WHERE "ResolvedAt" IS NULL;
CREATE INDEX "IX_ReconciliationFindings_AggregateId" ON "Payments"."ReconciliationFindings" ("AggregateId");

-- Reverse references are added after both sides exist.
ALTER TABLE "Payments"."ProviderTransactions" ADD CONSTRAINT "FK_ProviderTransactions_WebhookEvents_WebhookEventId"
    FOREIGN KEY ("WebhookEventId") REFERENCES "Payments"."WebhookEvents"("Id") ON DELETE RESTRICT;
ALTER TABLE "Payments"."LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_Refunds_RefundId"
    FOREIGN KEY ("RefundId") REFERENCES "Payments"."Refunds"("Id") ON DELETE RESTRICT;
ALTER TABLE "Payments"."LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_Settlements_SettlementId"
    FOREIGN KEY ("SettlementId") REFERENCES "Payments"."Settlements"("Id") ON DELETE RESTRICT;
ALTER TABLE "Payments"."LedgerEntries" ADD CONSTRAINT "FK_LedgerEntries_Disputes_DisputeId"
    FOREIGN KEY ("DisputeId") REFERENCES "Payments"."Disputes"("Id") ON DELETE RESTRICT;

CREATE FUNCTION "Payments".deny_mutation() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    RAISE EXCEPTION 'Payments table %.% is append-only', TG_TABLE_SCHEMA, TG_TABLE_NAME USING ERRCODE = '55000';
END;
$$;

CREATE FUNCTION "Payments".guard_currency_immutable() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    RAISE EXCEPTION 'Currencies reference rows are immutable' USING ERRCODE = '55000';
END;
$$;
CREATE TRIGGER "TR_Currencies_Immutable" BEFORE UPDATE OR DELETE ON "Payments"."Currencies" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_currency_immutable();
CREATE TRIGGER "TR_Currencies_NoTruncate" BEFORE TRUNCATE ON "Payments"."Currencies" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".enforce_minor_units(p_amount numeric, p_currency varchar) RETURNS boolean
LANGUAGE sql STABLE STRICT SET search_path = pg_catalog, "Payments" AS $$
    SELECT p_amount * power(10::numeric, c."Exponent") = trunc(p_amount * power(10::numeric, c."Exponent"))
    FROM "Payments"."Currencies" AS c WHERE c."Code" = p_currency
$$;

CREATE FUNCTION "Payments".validate_price_tenant() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "Payments"."Products" p WHERE p."Id" = NEW."ProductId" AND p."TenantId" = NEW."TenantId") THEN
        RAISE EXCEPTION 'Price tenant must match its Product tenant' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Prices_Tenant" BEFORE INSERT OR UPDATE OF "ProductId", "TenantId" ON "Payments"."Prices" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_price_tenant();

CREATE FUNCTION "Payments".check_price_minor_units() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NOT "Payments".enforce_minor_units(NEW."Amount", NEW."Currency") THEN
        RAISE EXCEPTION 'Price amount exceeds currency minor-unit precision' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Prices_MinorUnits" BEFORE INSERT OR UPDATE OF "Amount", "Currency" ON "Payments"."Prices" FOR EACH ROW EXECUTE FUNCTION "Payments".check_price_minor_units();

CREATE FUNCTION "Payments".check_purchase_minor_units() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NOT "Payments".enforce_minor_units(NEW."PresentmentAmount", NEW."PresentmentCurrency") OR
       NOT "Payments".enforce_minor_units(NEW."SettlementAmount", NEW."SettlementCurrency") OR
       NOT "Payments".enforce_minor_units(NEW."ProviderFee", NEW."SettlementCurrency") THEN
        RAISE EXCEPTION 'Purchase money facts exceed currency minor-unit precision' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Purchases_MinorUnits" BEFORE INSERT ON "Payments"."Purchases" FOR EACH ROW EXECUTE FUNCTION "Payments".check_purchase_minor_units();

CREATE FUNCTION "Payments".guard_price_amend() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Prices cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."TenantId", NEW."ProductId", NEW."Amount", NEW."Currency", NEW."CreatedByUserId", NEW."CreatedAt")
       IS DISTINCT FROM (OLD."Id", OLD."TenantId", OLD."ProductId", OLD."Amount", OLD."Currency", OLD."CreatedByUserId", OLD."CreatedAt")
       OR OLD."IsActive" IS DISTINCT FROM true OR NEW."IsActive" IS DISTINCT FROM false
       OR OLD."ArchivedAt" IS NOT NULL OR NEW."ArchivedAt" IS NULL THEN
        RAISE EXCEPTION 'Price permits only one active-to-archived transition' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Prices_Amend" BEFORE UPDATE OR DELETE ON "Payments"."Prices" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_price_amend();
CREATE TRIGGER "TR_Prices_NoTruncate" BEFORE TRUNCATE ON "Payments"."Prices" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".guard_product_mirror() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Product mirror rows are retained' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."TenantId", NEW."ItemType", NEW."ItemId", NEW."CreatedAt") IS DISTINCT FROM
       (OLD."Id", OLD."TenantId", OLD."ItemType", OLD."ItemId", OLD."CreatedAt") THEN
        RAISE EXCEPTION 'Product mirror identity is immutable' USING ERRCODE = '55000';
    END IF;
    IF NEW."SourceVersion" <= OLD."SourceVersion" THEN
        RAISE EXCEPTION 'Product mirror SourceVersion must increase' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Products_VersionedMirror" BEFORE UPDATE OR DELETE ON "Payments"."Products" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_product_mirror();
CREATE TRIGGER "TR_Products_NoTruncate" BEFORE TRUNCATE ON "Payments"."Products" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".validate_checkout_session_reference() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE route_currency text; route_enabled boolean; price_currency text;
BEGIN
    IF NEW."Status" <> 'Pending' THEN RAISE EXCEPTION 'Checkout sessions must be created Pending' USING ERRCODE = '23514'; END IF;
    IF NOT EXISTS (
        SELECT 1 FROM "Payments"."Products" p
        JOIN "Payments"."Prices" pr ON pr."ProductId" = p."Id"
        WHERE p."Id" = NEW."ProductId" AND p."TenantId" = NEW."TenantId"
          AND pr."Id" = NEW."PriceId" AND pr."TenantId" = NEW."TenantId" AND pr."IsActive"
    ) THEN RAISE EXCEPTION 'Checkout session requires an active Price on a Product with the same tenant' USING ERRCODE = '23514'; END IF;
    SELECT r."IntegrationCurrency", r."IsEnabled" INTO route_currency, route_enabled
      FROM "Payments"."ProviderCurrencyRoutes" r WHERE r."Id" = NEW."ProviderCurrencyRouteId";
    IF NOT FOUND OR NOT route_enabled THEN
        RAISE EXCEPTION 'Checkout session requires an enabled ProviderCurrencyRoute' USING ERRCODE = '23514';
    END IF;
    SELECT pr."Currency" INTO price_currency FROM "Payments"."Prices" pr WHERE pr."Id" = NEW."PriceId";
    IF NOT FOUND OR route_currency <> price_currency THEN
        RAISE EXCEPTION 'Checkout session requires an enabled route compatible with its Price currency' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_CheckoutSessions_References" BEFORE INSERT ON "Payments"."CheckoutSessions" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_checkout_session_reference();

CREATE FUNCTION "Payments".guard_checkout_session() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Checkout sessions cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."TenantId", NEW."UserId", NEW."ProductId", NEW."PriceId", NEW."ProviderCurrencyRouteId", NEW."CreatedAt") IS DISTINCT FROM
       (OLD."Id", OLD."TenantId", OLD."UserId", OLD."ProductId", OLD."PriceId", OLD."ProviderCurrencyRouteId", OLD."CreatedAt") THEN
        RAISE EXCEPTION 'Checkout session intent is immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD."ExternalCheckoutId" IS NOT NULL AND NEW."ExternalCheckoutId" IS NULL THEN
        RAISE EXCEPTION 'Provider checkout ID cannot be cleared once set' USING ERRCODE = '55000';
    END IF;
    IF NEW."Status" = OLD."Status" THEN
        IF NEW."CompletedAt" IS DISTINCT FROM OLD."CompletedAt" OR NEW."ExpiredAt" IS DISTINCT FROM OLD."ExpiredAt" OR
           (OLD."ExternalCheckoutId" IS NOT NULL AND NEW."ExternalCheckoutId" IS DISTINCT FROM OLD."ExternalCheckoutId") THEN
            RAISE EXCEPTION 'Checkout session update is not an allowed provider ID stamp' USING ERRCODE = '55000';
        END IF;
        RETURN NEW;
    END IF;
    IF OLD."Status" = 'Pending' AND NEW."Status" IN ('Completed', 'Expired') AND OLD."ExternalCheckoutId" IS NOT NULL AND NEW."ExternalCheckoutId" IS DISTINCT FROM OLD."ExternalCheckoutId" THEN
        RAISE EXCEPTION 'Checkout provider ID cannot change during a status transition' USING ERRCODE = '55000';
    END IF;
    IF NOT ((OLD."Status" = 'Pending' AND NEW."Status" IN ('Completed', 'Expired')) OR (OLD."Status" = 'Expired' AND NEW."Status" = 'Completed')) THEN
        RAISE EXCEPTION 'Invalid CheckoutSession transition' USING ERRCODE = '55000';
    END IF;
    IF NEW."Status" = 'Completed' AND (NEW."CompletedAt" IS NULL OR (OLD."Status" = 'Expired' AND NEW."ExpiredAt" IS DISTINCT FROM OLD."ExpiredAt")) THEN
        RAISE EXCEPTION 'Invalid CheckoutSession completion timestamps' USING ERRCODE = '23514';
    END IF;
    IF NEW."Status" = 'Expired' AND (NEW."ExpiredAt" IS NULL OR NEW."CompletedAt" IS NOT NULL) THEN
        RAISE EXCEPTION 'Invalid CheckoutSession expiry timestamps' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_CheckoutSessions_Transition" BEFORE UPDATE OR DELETE ON "Payments"."CheckoutSessions" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_checkout_session();
CREATE TRIGGER "TR_CheckoutSessions_NoTruncate" BEFORE TRUNCATE ON "Payments"."CheckoutSessions" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".validate_purchase_references() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE session_row record; price_row record; route_row record; movement record;
BEGIN
    IF NEW."Status" <> 'Paid' OR NEW."RefundedAt" IS NOT NULL THEN
        RAISE EXCEPTION 'Purchases must be created Paid without a RefundedAt timestamp' USING ERRCODE = '23514';
    END IF;
    SELECT s."TenantId", s."UserId", s."ProductId", s."PriceId", s."ProviderCurrencyRouteId"
      INTO session_row FROM "Payments"."CheckoutSessions" s WHERE s."Id" = NEW."CheckoutSessionId";
    SELECT p."TenantId", p."ProductId", p."Amount", p."Currency" INTO price_row
      FROM "Payments"."Prices" p WHERE p."Id" = NEW."PriceId";
    SELECT r."ProviderAccountId", r."IntegrationCurrency", r."SettlementCurrency", r."PresentmentMode"
      INTO route_row FROM "Payments"."ProviderCurrencyRoutes" r WHERE r."Id" = session_row."ProviderCurrencyRouteId";
    SELECT t."ProviderAccountId", t."Type", t."Direction", t."Amount", t."Currency", t."FeeAmount", t."ExternalPaymentId"
      INTO movement FROM "Payments"."ProviderTransactions" t WHERE t."Id" = NEW."ProviderTransactionId";
    IF session_row."TenantId" IS DISTINCT FROM NEW."TenantId" OR session_row."UserId" IS DISTINCT FROM NEW."UserId" OR
       session_row."ProductId" IS DISTINCT FROM NEW."ProductId" OR session_row."PriceId" IS DISTINCT FROM NEW."PriceId" OR
       price_row."TenantId" IS DISTINCT FROM NEW."TenantId" OR price_row."ProductId" IS DISTINCT FROM NEW."ProductId" OR
       route_row."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR movement."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR
       movement."Type" <> 'Capture' OR movement."Direction" <> 'Incoming' OR
       price_row."Amount" <> NEW."SettlementAmount" OR price_row."Currency" <> route_row."IntegrationCurrency" OR
       route_row."IntegrationCurrency" <> route_row."SettlementCurrency" OR NEW."SettlementCurrency" <> movement."Currency" OR
       NEW."SettlementAmount" <> movement."Amount" OR NEW."ProviderFee" <> movement."FeeAmount" OR
       NEW."ExternalPaymentId" IS DISTINCT FROM movement."ExternalPaymentId" OR
       (route_row."PresentmentMode" = 'Exact' AND (NEW."PresentmentAmount" <> price_row."Amount" OR NEW."PresentmentCurrency" <> price_row."Currency")) THEN
        RAISE EXCEPTION 'Purchase facts do not match the locked checkout, route, Price, and capture movement' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Purchases_References" BEFORE INSERT ON "Payments"."Purchases" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_purchase_references();

CREATE FUNCTION "Payments".guard_purchase_transition() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Purchases cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."TenantId", NEW."UserId", NEW."ProductId", NEW."PriceId", NEW."CheckoutSessionId", NEW."ProviderAccountId", NEW."ProviderTransactionId", NEW."ItemTitle", NEW."PresentmentAmount", NEW."PresentmentCurrency", NEW."SettlementAmount", NEW."SettlementCurrency", NEW."FxRate", NEW."ProviderFee", NEW."CommissionShare", NEW."ExternalPaymentId", NEW."PaidAt") IS DISTINCT FROM
       (OLD."Id", OLD."TenantId", OLD."UserId", OLD."ProductId", OLD."PriceId", OLD."CheckoutSessionId", OLD."ProviderAccountId", OLD."ProviderTransactionId", OLD."ItemTitle", OLD."PresentmentAmount", OLD."PresentmentCurrency", OLD."SettlementAmount", OLD."SettlementCurrency", OLD."FxRate", OLD."ProviderFee", OLD."CommissionShare", OLD."ExternalPaymentId", OLD."PaidAt") THEN
        RAISE EXCEPTION 'Purchase capture facts are immutable' USING ERRCODE = '55000';
    END IF;
    IF NOT ((OLD."Status" = 'Paid' AND NEW."Status" IN ('PartiallyRefunded', 'Refunded')) OR (OLD."Status" = 'PartiallyRefunded' AND NEW."Status" = 'Refunded')) THEN
        RAISE EXCEPTION 'Invalid Purchase status transition' USING ERRCODE = '55000';
    END IF;
    IF NEW."Status" = 'PartiallyRefunded' AND NEW."RefundedAt" IS NOT NULL THEN RAISE EXCEPTION 'Partial refund cannot stamp RefundedAt' USING ERRCODE = '23514'; END IF;
    IF NEW."Status" = 'Refunded' AND (NEW."RefundedAt" IS NULL OR OLD."RefundedAt" IS NOT NULL) THEN RAISE EXCEPTION 'Full refund requires first RefundedAt stamp' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Purchases_Transition" BEFORE UPDATE OR DELETE ON "Payments"."Purchases" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_purchase_transition();
CREATE TRIGGER "TR_Purchases_NoTruncate" BEFORE TRUNCATE ON "Payments"."Purchases" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".validate_refund_insert() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE purchase record; review record;
BEGIN
    SELECT p."TenantId", p."ProviderAccountId", p."SettlementCurrency" INTO purchase
      FROM "Payments"."Purchases" p WHERE p."Id" = NEW."PurchaseId";
    IF purchase."TenantId" IS DISTINCT FROM NEW."TenantId" OR purchase."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR purchase."SettlementCurrency" IS DISTINCT FROM NEW."Currency" THEN
        RAISE EXCEPTION 'Refund tenant, provider, and currency must match its Purchase' USING ERRCODE = '23514';
    END IF;
    IF NEW."Source" = 'Platform' AND NEW."RequestedByUserId" IS NULL THEN RAISE EXCEPTION 'Platform refunds require a requesting actor' USING ERRCODE = '23514'; END IF;
    IF NEW."Source" = 'Provider' AND NEW."RequestedByUserId" IS NOT NULL THEN RAISE EXCEPTION 'Provider refunds cannot claim a platform requester' USING ERRCODE = '23514'; END IF;
    IF NEW."Source" = 'ProviderSupport' THEN
        SELECT c."Type", c."PurchaseId", c."ProviderAccountId", c."Status" INTO review FROM "Payments"."PaymentReviewCases" c WHERE c."Id" = NEW."PaymentReviewCaseId";
        IF review."Type" <> 'ProviderReversal' OR review."PurchaseId" IS DISTINCT FROM NEW."PurchaseId" OR review."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR review."Status" NOT IN ('Approved', 'Resolved') THEN
            RAISE EXCEPTION 'ProviderSupport refund must match its approved ProviderReversal case' USING ERRCODE = '23514';
        END IF;
    END IF;
    IF NEW."Status" <> 'Pending' OR NEW."CompletedAt" IS NOT NULL THEN
        RAISE EXCEPTION 'Refunds must be created Pending' USING ERRCODE = '23514';
    END IF;
    IF NEW."Source" = 'ProviderSupport' AND NEW."PaymentReviewCaseId" IS NULL THEN
        RAISE EXCEPTION 'ProviderSupport refunds require a PaymentReviewCase' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Refunds_InitialState" BEFORE INSERT ON "Payments"."Refunds" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_refund_insert();

CREATE FUNCTION "Payments".guard_refund_transition() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Refunds cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."PurchaseId", NEW."TenantId", NEW."ProviderAccountId", NEW."PaymentReviewCaseId", NEW."Source", NEW."RequestedByUserId", NEW."Reason", NEW."ReasonCode", NEW."RevokeAccess", NEW."Amount", NEW."Currency", NEW."CreatedAt") IS DISTINCT FROM
       (OLD."Id", OLD."PurchaseId", OLD."TenantId", OLD."ProviderAccountId", OLD."PaymentReviewCaseId", OLD."Source", OLD."RequestedByUserId", OLD."Reason", OLD."ReasonCode", OLD."RevokeAccess", OLD."Amount", OLD."Currency", OLD."CreatedAt") THEN
        RAISE EXCEPTION 'Refund intent is immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD."Status" <> 'Pending' OR NEW."Status" NOT IN ('Succeeded', 'Failed') OR NEW."CompletedAt" IS NULL THEN
        RAISE EXCEPTION 'Invalid Refund transition' USING ERRCODE = '55000';
    END IF;
    IF OLD."ProviderTransactionId" IS NOT NULL AND NEW."ProviderTransactionId" IS DISTINCT FROM OLD."ProviderTransactionId" THEN RAISE EXCEPTION 'Refund movement identity is immutable' USING ERRCODE = '55000'; END IF;
    IF OLD."ExternalRefundId" IS NOT NULL AND NEW."ExternalRefundId" IS DISTINCT FROM OLD."ExternalRefundId" THEN RAISE EXCEPTION 'External refund ID is immutable once set' USING ERRCODE = '55000'; END IF;
    IF OLD."ProviderFeeRefunded" <> 0 AND NEW."ProviderFeeRefunded" IS DISTINCT FROM OLD."ProviderFeeRefunded" THEN RAISE EXCEPTION 'Refund fee is immutable once reported' USING ERRCODE = '55000'; END IF;
    IF NEW."Status" = 'Succeeded' AND (NEW."ProviderTransactionId" IS NULL OR (NEW."ExternalRefundId" IS NULL AND NOT (NEW."Source" = 'ProviderSupport' AND NEW."PaymentReviewCaseId" IS NOT NULL))) THEN
        RAISE EXCEPTION 'Succeeded refund requires provider movement evidence' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Refunds_Transition" BEFORE UPDATE OR DELETE ON "Payments"."Refunds" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_refund_transition();
CREATE TRIGGER "TR_Refunds_NoTruncate" BEFORE TRUNCATE ON "Payments"."Refunds" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".check_refund_minor_units() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NOT "Payments".enforce_minor_units(NEW."Amount", NEW."Currency") THEN RAISE EXCEPTION 'Refund amount exceeds currency minor-unit precision' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Refunds_MinorUnits" BEFORE INSERT ON "Payments"."Refunds" FOR EACH ROW EXECUTE FUNCTION "Payments".check_refund_minor_units();

CREATE FUNCTION "Payments".validate_refund_request_insert() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NEW."Status" <> 'Requested' OR NEW."ApprovedAmount" IS NOT NULL OR NEW."DecidedByUserId" IS NOT NULL OR
       NEW."DecidedAt" IS NOT NULL OR NEW."RefundId" IS NOT NULL THEN
        RAISE EXCEPTION 'RefundRequests must be created Requested without decision fields' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_RefundRequests_InitialState" BEFORE INSERT ON "Payments"."RefundRequests" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_refund_request_insert();

CREATE FUNCTION "Payments".guard_refund_request() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Refund requests cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF OLD."Status" <> 'Requested' OR NEW."Status" NOT IN ('Approved', 'Rejected', 'Cancelled') OR
       (NEW."Id", NEW."PurchaseId", NEW."TenantId", NEW."RequestedByUserId", NEW."Reason", NEW."Currency", NEW."CreatedAt") IS DISTINCT FROM
       (OLD."Id", OLD."PurchaseId", OLD."TenantId", OLD."RequestedByUserId", OLD."Reason", OLD."Currency", OLD."CreatedAt") OR
       (NEW."Status" = 'Approved' AND (NEW."ApprovedAmount" IS NULL OR NEW."DecidedByUserId" IS NULL OR NEW."DecidedAt" IS NULL OR NEW."RefundId" IS NULL)) OR
       (NEW."Status" = 'Rejected' AND (NEW."ApprovedAmount" IS NOT NULL OR NEW."RefundId" IS NOT NULL OR NEW."DecidedByUserId" IS NULL OR NEW."DecidedAt" IS NULL)) OR
       (NEW."Status" = 'Cancelled' AND (NEW."ApprovedAmount" IS NOT NULL OR NEW."RefundId" IS NOT NULL OR NEW."DecidedByUserId" IS NOT NULL OR NEW."DecidedAt" IS NOT NULL)) OR
       (NEW."Status" IN ('Approved', 'Rejected') AND NEW."DecidedByUserId" IS NOT DISTINCT FROM OLD."RequestedByUserId") THEN
        RAISE EXCEPTION 'Invalid RefundRequest transition' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_RefundRequests_Transition" BEFORE UPDATE OR DELETE ON "Payments"."RefundRequests" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_refund_request();
CREATE TRIGGER "TR_RefundRequests_NoTruncate" BEFORE TRUNCATE ON "Payments"."RefundRequests" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();
CREATE FUNCTION "Payments".check_refund_request_minor_units() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NEW."ApprovedAmount" IS NOT NULL AND NOT "Payments".enforce_minor_units(NEW."ApprovedAmount", NEW."Currency") THEN RAISE EXCEPTION 'Approved refund amount exceeds currency minor-unit precision' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_RefundRequests_MinorUnits" BEFORE INSERT OR UPDATE OF "ApprovedAmount", "Currency" ON "Payments"."RefundRequests" FOR EACH ROW EXECUTE FUNCTION "Payments".check_refund_request_minor_units();

CREATE FUNCTION "Payments".validate_dispute_insert() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE purchase record;
BEGIN
    SELECT p."TenantId", p."ProviderAccountId", p."SettlementCurrency", p."SettlementAmount" INTO purchase
      FROM "Payments"."Purchases" p WHERE p."Id" = NEW."PurchaseId";
    IF purchase."TenantId" IS DISTINCT FROM NEW."TenantId" OR purchase."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR
       purchase."SettlementCurrency" IS DISTINCT FROM NEW."Currency" OR NEW."Amount" > purchase."SettlementAmount" THEN
        RAISE EXCEPTION 'Dispute references and amount must agree with its Purchase' USING ERRCODE = '23514';
    END IF;
    IF NOT "Payments".enforce_minor_units(NEW."Amount", NEW."Currency") OR NOT "Payments".enforce_minor_units(NEW."ProviderDisputeFee", NEW."Currency") THEN
        RAISE EXCEPTION 'Dispute money facts exceed currency minor-unit precision' USING ERRCODE = '23514';
    END IF;
    IF NEW."Status" <> 'Open' OR NEW."ResolvedAt" IS NOT NULL OR NEW."ResolvedByUserId" IS NOT NULL THEN
        RAISE EXCEPTION 'Disputes must be created Open' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Disputes_InitialState" BEFORE INSERT ON "Payments"."Disputes" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_dispute_insert();

CREATE FUNCTION "Payments".guard_dispute_transition() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Disputes cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."PurchaseId", NEW."TenantId", NEW."ProviderAccountId", NEW."ExternalDisputeId", NEW."Reason", NEW."Amount", NEW."Currency", NEW."OpenedAt") IS DISTINCT FROM
       (OLD."Id", OLD."PurchaseId", OLD."TenantId", OLD."ProviderAccountId", OLD."ExternalDisputeId", OLD."Reason", OLD."Amount", OLD."Currency", OLD."OpenedAt") OR OLD."Status" <> 'Open' OR NEW."Status" NOT IN ('Won', 'Lost') OR NEW."ResolvedAt" IS NULL THEN
        RAISE EXCEPTION 'Invalid Dispute transition' USING ERRCODE = '55000';
    END IF;
    IF OLD."ProviderDisputeFee" <> 0 AND NEW."ProviderDisputeFee" IS DISTINCT FROM OLD."ProviderDisputeFee" THEN RAISE EXCEPTION 'Dispute fee is set once' USING ERRCODE = '55000'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Disputes_Transition" BEFORE UPDATE OR DELETE ON "Payments"."Disputes" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_dispute_transition();
CREATE TRIGGER "TR_Disputes_NoTruncate" BEFORE TRUNCATE ON "Payments"."Disputes" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".guard_webhook_event_reference() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE account record;
BEGIN
    IF NEW."ProviderAccountId" IS NOT NULL THEN
        SELECT a."ProviderName", a."Environment" INTO account FROM "Payments"."ProviderAccounts" a WHERE a."Id" = NEW."ProviderAccountId";
        IF account."ProviderName" IS DISTINCT FROM NEW."ProviderName" OR account."Environment" IS DISTINCT FROM NEW."Environment" OR
           (NEW."Livemode" IS NOT NULL AND NEW."Livemode" IS DISTINCT FROM (account."Environment" = 'Live')) THEN
            RAISE EXCEPTION 'Webhook provider account, environment, and livemode must agree' USING ERRCODE = '23514';
        END IF;
    ELSIF NEW."Livemode" IS NOT NULL AND NEW."Livemode" IS DISTINCT FROM (NEW."Environment" = 'Live') THEN
        RAISE EXCEPTION 'Webhook environment and livemode must agree' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_WebhookEvents_Environment" BEFORE INSERT OR UPDATE OF "ProviderName", "ProviderAccountId", "Environment", "Livemode" ON "Payments"."WebhookEvents" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_webhook_event_reference();

CREATE FUNCTION "Payments".guard_provider_transaction() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE route_exists boolean; event_row record; account_provider text; account_environment text;
BEGIN
    IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'ProviderTransactions are append-only' USING ERRCODE = '55000'; END IF;
    IF NOT "Payments".enforce_minor_units(NEW."Amount", NEW."Currency") OR NOT "Payments".enforce_minor_units(NEW."FeeAmount", NEW."Currency") THEN RAISE EXCEPTION 'Provider movement exceeds currency minor-unit precision' USING ERRCODE = '23514'; END IF;
    IF (NEW."Type" = 'Capture' AND NEW."Direction" <> 'Incoming') OR
       (NEW."Type" IN ('Refund', 'Payout', 'DisputeWithdrawal') AND NEW."Direction" <> 'Outgoing') OR
       (NEW."Type" = 'DisputeReinstatement' AND NEW."Direction" <> 'Incoming') THEN
        RAISE EXCEPTION 'Provider transaction type and direction do not agree' USING ERRCODE = '23514';
    END IF;
    SELECT EXISTS (SELECT 1 FROM "Payments"."ProviderCurrencyRoutes" r WHERE r."ProviderAccountId" = NEW."ProviderAccountId" AND r."SettlementCurrency" = NEW."Currency") INTO route_exists;
    IF NOT route_exists THEN RAISE EXCEPTION 'Provider transaction currency has no configured route' USING ERRCODE = '23514'; END IF;
    IF NEW."WebhookEventId" IS NOT NULL THEN
        SELECT e."ProviderName", e."ProviderAccountId", e."Environment", e."Livemode", e."SignatureVerified"
          INTO event_row FROM "Payments"."WebhookEvents" e WHERE e."Id" = NEW."WebhookEventId";
        SELECT a."ProviderName", a."Environment" INTO account_provider, account_environment
          FROM "Payments"."ProviderAccounts" a WHERE a."Id" = NEW."ProviderAccountId";
        IF event_row."ProviderName" IS DISTINCT FROM account_provider OR
           (event_row."ProviderAccountId" IS NOT NULL AND event_row."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId") OR
           event_row."Environment" IS DISTINCT FROM account_environment OR event_row."SignatureVerified" IS DISTINCT FROM true OR
           (event_row."Livemode" IS NOT NULL AND event_row."Livemode" IS DISTINCT FROM (account_environment = 'Live')) THEN
            RAISE EXCEPTION 'Provider transaction cannot derive from an unverified or mismatched webhook event' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_ProviderTransactions_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "Payments"."ProviderTransactions" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_provider_transaction();
CREATE TRIGGER "TR_ProviderTransactions_NoTruncate" BEFORE TRUNCATE ON "Payments"."ProviderTransactions" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".guard_provider_identity() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF EXISTS (SELECT 1 FROM "Payments"."ProviderAccounts" WHERE "ProviderName" = OLD."Name") OR EXISTS (SELECT 1 FROM "Payments"."WebhookEvents" WHERE "ProviderName" = OLD."Name") THEN
            RAISE EXCEPTION 'Referenced payment provider identity cannot be deleted' USING ERRCODE = '55000';
        END IF;
        RETURN OLD;
    END IF;
    IF NEW."Name" IS DISTINCT FROM OLD."Name" OR NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt" THEN RAISE EXCEPTION 'Payment provider identity is immutable' USING ERRCODE = '55000'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_PaymentProviders_Identity" BEFORE UPDATE OR DELETE ON "Payments"."PaymentProviders" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_provider_identity();
CREATE TRIGGER "TR_PaymentProviders_NoTruncate" BEFORE TRUNCATE ON "Payments"."PaymentProviders" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".guard_provider_account() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'ProviderAccounts are retained' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."ProviderName", NEW."Environment", NEW."ExternalAccountId", NEW."CreatedAt") IS DISTINCT FROM
       (OLD."Id", OLD."ProviderName", OLD."Environment", OLD."ExternalAccountId", OLD."CreatedAt") THEN RAISE EXCEPTION 'Provider account identity is immutable' USING ERRCODE = '55000'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_ProviderAccounts_Identity" BEFORE UPDATE OR DELETE ON "Payments"."ProviderAccounts" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_provider_account();
CREATE TRIGGER "TR_ProviderAccounts_NoTruncate" BEFORE TRUNCATE ON "Payments"."ProviderAccounts" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".guard_provider_route() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'ProviderCurrencyRoutes are retained' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."ProviderAccountId", NEW."IntegrationCurrency", NEW."SettlementCurrency", NEW."PresentmentMode") IS DISTINCT FROM
       (OLD."Id", OLD."ProviderAccountId", OLD."IntegrationCurrency", OLD."SettlementCurrency", OLD."PresentmentMode") THEN
        RAISE EXCEPTION 'Provider currency route identity is immutable' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_ProviderCurrencyRoutes_Identity" BEFORE UPDATE OR DELETE ON "Payments"."ProviderCurrencyRoutes" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_provider_route();
CREATE TRIGGER "TR_ProviderCurrencyRoutes_NoTruncate" BEFORE TRUNCATE ON "Payments"."ProviderCurrencyRoutes" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".validate_settlement_insert() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE review record; payable record; source record;
BEGIN
    SELECT c."TenantId", c."ProviderAccountId", c."PayableAccountId", c."SourceAccountId", c."Amount", c."Currency", c."OpenedByUserId", c."ApprovedByUserId", c."ProviderTransactionId", c."Type", c."Status"
      INTO review FROM "Payments"."PaymentReviewCases" c WHERE c."Id" = NEW."PaymentReviewCaseId";
    SELECT a."Role", a."TenantId", a."ProviderAccountId", a."Currency" INTO payable
      FROM "Payments"."LedgerAccounts" a WHERE a."Id" = NEW."PayableAccountId";
    SELECT a."Role", a."TenantId", a."ProviderAccountId", a."Currency" INTO source
      FROM "Payments"."LedgerAccounts" a WHERE a."Id" = NEW."SourceAccountId";
    IF review."Type" <> 'Settlement' OR review."Status" <> 'Resolved' OR
       review."TenantId" IS DISTINCT FROM NEW."TenantId" OR review."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR
       review."PayableAccountId" IS DISTINCT FROM NEW."PayableAccountId" OR review."SourceAccountId" IS DISTINCT FROM NEW."SourceAccountId" OR
       review."Amount" <> NEW."Amount" OR review."Currency" <> NEW."Currency" OR
       review."OpenedByUserId" IS DISTINCT FROM NEW."RecordedByUserId" OR review."ApprovedByUserId" IS DISTINCT FROM NEW."ApprovedByUserId" OR
       review."ProviderTransactionId" IS DISTINCT FROM NEW."ProviderTransactionId" OR NEW."RecordedByUserId" = NEW."ApprovedByUserId" OR
       payable."Role" <> 'AcademyPayable' OR payable."TenantId" IS DISTINCT FROM NEW."TenantId" OR
       payable."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR payable."Currency" <> NEW."Currency" OR
       source."Currency" <> NEW."Currency" OR
       NOT ((source."Role" = 'ProviderAsset' AND source."ProviderAccountId" = NEW."ProviderAccountId" AND source."TenantId" IS NULL) OR
            (source."Role" IN ('Bank', 'Cash') AND source."ProviderAccountId" IS NULL AND source."TenantId" IS NULL)) THEN
        RAISE EXCEPTION 'Settlement does not match its resolved review intent and eligible accounts' USING ERRCODE = '23514';
    END IF;
    IF NEW."ProviderTransactionId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "Payments"."ProviderTransactions" t WHERE t."Id" = NEW."ProviderTransactionId" AND
          t."ProviderAccountId" = NEW."ProviderAccountId" AND t."Direction" = 'Outgoing' AND t."Currency" = NEW."Currency" AND t."Amount" = NEW."Amount"
    ) THEN RAISE EXCEPTION 'Settlement provider movement must be the confirmed outgoing transfer' USING ERRCODE = '23514'; END IF;
    IF NEW."ProviderTransactionId" IS DISTINCT FROM review."ProviderTransactionId" THEN
        RAISE EXCEPTION 'Settlement movement must match the approved review confirmation' USING ERRCODE = '23514';
    END IF;
    IF source."Role" = 'ProviderAsset' AND NEW."ProviderTransactionId" IS NULL THEN
        RAISE EXCEPTION 'Provider-wallet settlement requires confirmed ProviderTransaction evidence' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_Settlements_References" BEFORE INSERT ON "Payments"."Settlements" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_settlement_insert();

CREATE FUNCTION "Payments".guard_settlement_immutable() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    RAISE EXCEPTION 'Settlements are append-only confirmations' USING ERRCODE = '55000';
END;
$$;
CREATE TRIGGER "TR_Settlements_Immutable" BEFORE UPDATE OR DELETE ON "Payments"."Settlements" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_settlement_immutable();
CREATE TRIGGER "TR_Settlements_NoTruncate" BEFORE TRUNCATE ON "Payments"."Settlements" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".validate_review_case_insert() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NEW."Status" <> 'Open' OR NEW."ApprovedByUserId" IS NOT NULL OR NEW."ResolvedAt" IS NOT NULL OR NEW."Resolution" IS NOT NULL THEN
        RAISE EXCEPTION 'PaymentReviewCases must be created Open' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_PaymentReviewCases_InitialState" BEFORE INSERT ON "Payments"."PaymentReviewCases" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_review_case_insert();

CREATE FUNCTION "Payments".guard_review_case() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Payment review cases cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."TenantId", NEW."ProviderAccountId", NEW."PayableAccountId", NEW."CheckoutSessionId", NEW."PurchaseId", NEW."Amount", NEW."Currency", NEW."SourceAccountId", NEW."DestinationSnapshot", NEW."ReasonCode", NEW."RevokeAccess", NEW."Type", NEW."OpenedByUserId", NEW."ProviderSupportReference", NEW."EvidenceReference", NEW."CreatedAt") IS DISTINCT FROM
       (OLD."Id", OLD."TenantId", OLD."ProviderAccountId", OLD."PayableAccountId", OLD."CheckoutSessionId", OLD."PurchaseId", OLD."Amount", OLD."Currency", OLD."SourceAccountId", OLD."DestinationSnapshot", OLD."ReasonCode", OLD."RevokeAccess", OLD."Type", OLD."OpenedByUserId", OLD."ProviderSupportReference", OLD."EvidenceReference", OLD."CreatedAt") OR
       (NEW."ProviderTransactionId" IS DISTINCT FROM OLD."ProviderTransactionId" AND NOT
           (OLD."Status" = 'Approved' AND NEW."Status" = 'Resolved' AND
            (OLD."ProviderTransactionId" IS NULL OR OLD."Type" = 'UnappliedReturn'))) THEN
        RAISE EXCEPTION 'Payment review intent and evidence are immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD."Status" = 'Open' AND NEW."Status" IN ('Approved', 'Rejected') AND NEW."ApprovedByUserId" IS NOT NULL AND NEW."ApprovedByUserId" <> OLD."OpenedByUserId" AND NEW."ResolvedAt" IS NULL THEN RETURN NEW; END IF;
    IF OLD."Status" = 'Approved' AND NEW."Status" = 'Resolved' AND NEW."ApprovedByUserId" = OLD."ApprovedByUserId" AND NEW."ResolvedAt" IS NOT NULL AND NEW."Resolution" IS NOT NULL THEN RETURN NEW; END IF;
    RAISE EXCEPTION 'Invalid PaymentReviewCase transition' USING ERRCODE = '55000';
END;
$$;
CREATE TRIGGER "TR_PaymentReviewCases_Transition" BEFORE UPDATE OR DELETE ON "Payments"."PaymentReviewCases" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_review_case();
CREATE TRIGGER "TR_PaymentReviewCases_NoTruncate" BEFORE TRUNCATE ON "Payments"."PaymentReviewCases" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();
CREATE FUNCTION "Payments".check_review_minor_units() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NOT "Payments".enforce_minor_units(NEW."Amount", NEW."Currency") THEN RAISE EXCEPTION 'Review amount exceeds currency minor-unit precision' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_PaymentReviewCases_MinorUnits" BEFORE INSERT ON "Payments"."PaymentReviewCases" FOR EACH ROW EXECUTE FUNCTION "Payments".check_review_minor_units();

CREATE FUNCTION "Payments".guard_risk_override() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE defaults record;
BEGIN
    SELECT "ShortHoldDays", "ReserveRate", "DisputeHoldDays" INTO defaults FROM "Payments"."ProviderCurrencyRoutes" WHERE "Id" = NEW."ProviderCurrencyRouteId";
    IF NOT FOUND THEN RAISE EXCEPTION 'Risk override route does not exist' USING ERRCODE = '23503'; END IF;
    IF (NEW."ShortHoldDays" IS NOT NULL AND NEW."ShortHoldDays" < defaults."ShortHoldDays") OR
       (NEW."ReserveRate" IS NOT NULL AND NEW."ReserveRate" < defaults."ReserveRate") OR
       (NEW."DisputeHoldDays" IS NOT NULL AND NEW."DisputeHoldDays" < defaults."DisputeHoldDays") THEN
        RAISE EXCEPTION 'Academy risk overrides may only strengthen route defaults' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_AcademyProviderRiskOverrides_Stricter" BEFORE INSERT OR UPDATE ON "Payments"."AcademyProviderRiskOverrides" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_risk_override();

CREATE FUNCTION "Payments".guard_ledger_entry_reference() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE tx record; source record; account record; review record; purchase record; refund record; dispute record;
BEGIN
    IF NEW."Type" IN ('Capture', 'RefundReversal', 'DisputeLoss', 'DisputeWon', 'UnappliedReceipt', 'ApplyUnappliedFunds', 'ReturnUnappliedFunds', 'ProviderPayout') THEN
        SELECT t."ProviderAccountId", t."Currency", t."Amount", t."Type", t."Direction", t."OccurredAt"
          INTO tx FROM "Payments"."ProviderTransactions" t WHERE t."Id" = NEW."ProviderTransactionId";
        IF tx."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" THEN RAISE EXCEPTION 'Ledger entry provider transaction must use the same ProviderAccount' USING ERRCODE = '23514'; END IF;
        IF NEW."EffectiveAt" IS DISTINCT FROM tx."OccurredAt" THEN RAISE EXCEPTION 'EffectiveAt must preserve the provider movement time' USING ERRCODE = '23514'; END IF;
        IF (NEW."Type" IN ('Capture', 'UnappliedReceipt', 'ApplyUnappliedFunds') AND (tx."Direction" <> 'Incoming' OR tx."Type" <> 'Capture')) OR
           (NEW."Type" IN ('RefundReversal', 'ReturnUnappliedFunds') AND (tx."Direction" <> 'Outgoing' OR tx."Type" <> 'Refund')) OR
           (NEW."Type" = 'DisputeLoss' AND (tx."Direction" <> 'Outgoing' OR tx."Type" <> 'DisputeWithdrawal')) OR
           (NEW."Type" = 'DisputeWon' AND (tx."Direction" <> 'Incoming' OR tx."Type" <> 'DisputeReinstatement')) OR
           (NEW."Type" = 'ProviderPayout' AND (tx."Direction" <> 'Outgoing' OR tx."Type" <> 'Payout')) THEN
            RAISE EXCEPTION 'Ledger entry type does not match ProviderTransaction direction' USING ERRCODE = '23514';
        END IF;
        IF NEW."Type" IN ('Capture', 'RefundReversal', 'DisputeLoss', 'DisputeWon') THEN
            SELECT p."TenantId", p."ProviderAccountId", p."ProviderTransactionId", p."SettlementCurrency", p."SettlementAmount"
              INTO purchase FROM "Payments"."Purchases" p WHERE p."Id" = NEW."PurchaseId";
            IF purchase."TenantId" IS DISTINCT FROM NEW."TenantId" OR purchase."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR purchase."SettlementCurrency" IS DISTINCT FROM tx."Currency" THEN
                RAISE EXCEPTION 'Ledger movement tenant/provider/currency must match its Purchase' USING ERRCODE = '23514';
            END IF;
            IF NEW."Type" = 'Capture' AND (purchase."ProviderTransactionId" IS DISTINCT FROM NEW."ProviderTransactionId" OR purchase."SettlementAmount" <> tx."Amount") THEN
                RAISE EXCEPTION 'Capture must match the Purchase funding movement' USING ERRCODE = '23514';
            END IF;
            IF NEW."Type" = 'RefundReversal' THEN
                SELECT r."PurchaseId", r."TenantId", r."ProviderAccountId", r."ProviderTransactionId", r."Amount", r."Currency", r."Status"
                  INTO refund FROM "Payments"."Refunds" r WHERE r."Id" = NEW."RefundId";
                IF refund."PurchaseId" IS DISTINCT FROM NEW."PurchaseId" OR refund."TenantId" IS DISTINCT FROM NEW."TenantId" OR
                   refund."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR refund."ProviderTransactionId" IS DISTINCT FROM NEW."ProviderTransactionId" OR
                   refund."Currency" IS DISTINCT FROM tx."Currency" OR refund."Amount" <> tx."Amount" OR refund."Status" <> 'Succeeded' THEN
                    RAISE EXCEPTION 'Refund reversal must match its succeeded Refund movement' USING ERRCODE = '23514';
                END IF;
            END IF;
            IF NEW."Type" IN ('DisputeLoss', 'DisputeWon') THEN
                SELECT d."PurchaseId", d."TenantId", d."ProviderAccountId", d."Amount", d."Currency"
                  INTO dispute FROM "Payments"."Disputes" d WHERE d."Id" = NEW."DisputeId";
                IF dispute."PurchaseId" IS DISTINCT FROM NEW."PurchaseId" OR dispute."TenantId" IS DISTINCT FROM NEW."TenantId" OR
                   dispute."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR dispute."Currency" IS DISTINCT FROM tx."Currency" OR dispute."Amount" <> tx."Amount" THEN
                    RAISE EXCEPTION 'Dispute movement must match its Dispute facts' USING ERRCODE = '23514';
                END IF;
            END IF;
        END IF;
        IF NEW."Type" IN ('ApplyUnappliedFunds', 'ReturnUnappliedFunds') THEN
            SELECT c."ProviderAccountId", c."ProviderTransactionId", c."Amount", c."Currency", c."Type", c."Status"
              INTO review FROM "Payments"."PaymentReviewCases" c WHERE c."Id" = NEW."PaymentReviewCaseId";
            IF review."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR review."Currency" IS DISTINCT FROM tx."Currency" OR review."Status" <> 'Resolved' OR
               (NEW."Type" = 'ApplyUnappliedFunds' AND (review."Type" <> 'ApplyUnappliedFunds' OR review."ProviderTransactionId" IS DISTINCT FROM NEW."ProviderTransactionId" OR tx."Amount" < review."Amount")) OR
               (NEW."Type" = 'ReturnUnappliedFunds' AND (review."Type" <> 'UnappliedReturn' OR review."ProviderTransactionId" IS NULL OR tx."Amount" <> review."Amount")) THEN
                RAISE EXCEPTION 'Unapplied-funds movement must match its resolved review case' USING ERRCODE = '23514';
            END IF;
        END IF;
    END IF;
    IF NEW."Type" = 'Settlement' THEN
        SELECT s."TenantId", s."ProviderAccountId", s."PaymentReviewCaseId", s."PayableAccountId", s."SourceAccountId", s."ProviderTransactionId", s."Amount", s."Currency"
          INTO source FROM "Payments"."Settlements" s WHERE s."Id" = NEW."SettlementId";
        IF source."TenantId" IS DISTINCT FROM NEW."TenantId" OR source."ProviderAccountId" IS DISTINCT FROM NEW."ProviderAccountId" OR source."PaymentReviewCaseId" IS DISTINCT FROM NEW."PaymentReviewCaseId" THEN
            RAISE EXCEPTION 'Settlement entry header must match its immutable Settlement row' USING ERRCODE = '23514';
        END IF;
        SELECT c."Type", c."Status", c."Amount", c."Currency", c."PayableAccountId", c."SourceAccountId"
          INTO review FROM "Payments"."PaymentReviewCases" c WHERE c."Id" = NEW."PaymentReviewCaseId";
        IF review."Type" <> 'Settlement' OR review."Status" <> 'Resolved' OR review."Amount" <> source."Amount" OR review."Currency" <> source."Currency" OR
           review."PayableAccountId" IS DISTINCT FROM source."PayableAccountId" OR review."SourceAccountId" IS DISTINCT FROM source."SourceAccountId" THEN
            RAISE EXCEPTION 'Settlement ledger entry must resolve its approved review intent exactly' USING ERRCODE = '23514';
        END IF;
        IF NEW."EffectiveAt" IS NOT NULL THEN RAISE EXCEPTION 'Settlement EffectiveAt must be NULL' USING ERRCODE = '23514'; END IF;
        IF EXISTS (SELECT 1 FROM "Payments"."LedgerAccounts" a WHERE a."Id" = source."PayableAccountId" AND (a."Role" <> 'AcademyPayable' OR a."TenantId" IS DISTINCT FROM source."TenantId" OR a."ProviderAccountId" IS DISTINCT FROM source."ProviderAccountId" OR a."Currency" <> source."Currency")) THEN
            RAISE EXCEPTION 'Settlement payable account does not match tenant/provider/currency' USING ERRCODE = '23514';
        END IF;
        SELECT a."Role", a."TenantId", a."ProviderAccountId", a."Currency" INTO account FROM "Payments"."LedgerAccounts" a WHERE a."Id" = source."SourceAccountId";
        IF account."Currency" <> source."Currency" OR NOT ((account."Role" = 'ProviderAsset' AND account."ProviderAccountId" = source."ProviderAccountId" AND account."TenantId" IS NULL) OR (account."Role" IN ('Bank', 'Cash') AND account."ProviderAccountId" IS NULL AND account."TenantId" IS NULL)) THEN
            RAISE EXCEPTION 'Settlement source must be a same-currency provider asset or Bank/Cash account' USING ERRCODE = '23514';
        END IF;
        IF source."ProviderTransactionId" IS DISTINCT FROM (SELECT p."ProviderTransactionId" FROM "Payments"."PaymentReviewCases" p WHERE p."Id" = source."PaymentReviewCaseId") THEN
            RAISE EXCEPTION 'Settlement ProviderTransaction must match approved review evidence' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_LedgerEntries_References" BEFORE INSERT ON "Payments"."LedgerEntries" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_ledger_entry_reference();

CREATE FUNCTION "Payments".validate_ledger_line() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE entry record; account record; settlement record;
BEGIN
    SELECT e."Type", e."ProviderAccountId", e."TenantId", e."SettlementId" INTO entry FROM "Payments"."LedgerEntries" e WHERE e."Id" = NEW."EntryId";
    SELECT a."Role", a."TenantId", a."ProviderAccountId", a."Currency" INTO account FROM "Payments"."LedgerAccounts" a WHERE a."Id" = NEW."AccountId";
    IF NEW."TenantId" IS DISTINCT FROM entry."TenantId" OR NEW."Currency" <> account."Currency" OR
       (account."TenantId" IS NOT NULL AND account."TenantId" IS DISTINCT FROM entry."TenantId") OR
       (account."ProviderAccountId" IS NOT NULL AND account."ProviderAccountId" IS DISTINCT FROM entry."ProviderAccountId") OR
       (account."ProviderAccountId" IS NULL AND entry."Type" NOT IN ('Settlement', 'ProviderPayout')) THEN
        RAISE EXCEPTION 'Ledger line tenant/provider/account/currency does not match its entry' USING ERRCODE = '23514';
    END IF;
    IF entry."Type" = 'Settlement' THEN
        SELECT s."PayableAccountId", s."SourceAccountId", s."Amount", s."Currency" INTO settlement FROM "Payments"."Settlements" s WHERE s."Id" = entry."SettlementId";
        IF (NEW."AccountId" = settlement."PayableAccountId" AND (NEW."Direction" <> 'Debit' OR NEW."Amount" <> settlement."Amount")) OR
           (NEW."AccountId" = settlement."SourceAccountId" AND (NEW."Direction" <> 'Credit' OR NEW."Amount" <> settlement."Amount")) OR
           NEW."AccountId" NOT IN (settlement."PayableAccountId", settlement."SourceAccountId") THEN
            RAISE EXCEPTION 'Settlement ledger lines must debit its exact payable and credit its exact source for the approved amount' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_LedgerLines_References" BEFORE INSERT ON "Payments"."LedgerLines" FOR EACH ROW EXECUTE FUNCTION "Payments".validate_ledger_line();

CREATE FUNCTION "Payments".check_entry_balance() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE line_count bigint; debit_total numeric; credit_total numeric; currency_count bigint; entry_provider uuid; entry_tenant uuid; entry_type text; tx_provider uuid; tx_currency text; review_provider uuid;
BEGIN
    SELECT count(*), count(DISTINCT l."Currency"),
           COALESCE(sum(l."Amount") FILTER (WHERE l."Direction" = 'Debit'), 0),
           COALESCE(sum(l."Amount") FILTER (WHERE l."Direction" = 'Credit'), 0)
      INTO line_count, currency_count, debit_total, credit_total
      FROM "Payments"."LedgerLines" l WHERE l."EntryId" = NEW."Id";
    IF line_count < 2 THEN RAISE EXCEPTION 'Ledger entry requires at least two lines' USING ERRCODE = '23514'; END IF;
    IF debit_total <> credit_total THEN RAISE EXCEPTION 'Ledger entry is not balanced' USING ERRCODE = '23514'; END IF;
    IF currency_count <> 1 THEN RAISE EXCEPTION 'Ledger entry lines must share one currency' USING ERRCODE = '23514'; END IF;

    SELECT e."ProviderAccountId", e."TenantId", e."Type" INTO entry_provider, entry_tenant, entry_type
      FROM "Payments"."LedgerEntries" e WHERE e."Id" = NEW."Id";
    IF NEW."ProviderTransactionId" IS NOT NULL THEN
        SELECT t."ProviderAccountId", t."Currency" INTO tx_provider, tx_currency FROM "Payments"."ProviderTransactions" t WHERE t."Id" = NEW."ProviderTransactionId";
        IF tx_provider IS DISTINCT FROM entry_provider THEN RAISE EXCEPTION 'Ledger entry and provider transaction account differ' USING ERRCODE = '23514'; END IF;
    END IF;
    IF entry_type IN ('Settlement', 'ProviderPayout') THEN
        SELECT p."ProviderAccountId" INTO review_provider FROM "Payments"."PaymentReviewCases" p WHERE p."Id" = NEW."PaymentReviewCaseId";
    END IF;

    IF EXISTS (
        SELECT 1 FROM "Payments"."LedgerLines" l
        JOIN "Payments"."LedgerAccounts" a ON a."Id" = l."AccountId"
        LEFT JOIN "Payments"."Currencies" c ON c."Code" = l."Currency"
        WHERE l."EntryId" = NEW."Id" AND (
            l."TenantId" IS DISTINCT FROM entry_tenant OR a."Currency" <> l."Currency" OR
            (a."TenantId" IS NOT NULL AND a."TenantId" IS DISTINCT FROM entry_tenant) OR
            (a."ProviderAccountId" IS NOT NULL AND a."ProviderAccountId" IS DISTINCT FROM COALESCE(entry_provider, review_provider)) OR
            (a."ProviderAccountId" IS NULL AND a."Role" IN ('Bank', 'Cash') AND entry_type NOT IN ('Settlement', 'ProviderPayout')) OR
            c."Code" IS NULL OR l."Amount" * power(10::numeric, c."Exponent") <> trunc(l."Amount" * power(10::numeric, c."Exponent")) OR
            (tx_currency IS NOT NULL AND tx_currency <> l."Currency")
        )
    ) THEN RAISE EXCEPTION 'Ledger line violates tenant/account/provider/currency/minor-unit partition' USING ERRCODE = '23514'; END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER "TR_LedgerEntries_Balance" AFTER INSERT ON "Payments"."LedgerEntries" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Payments".check_entry_balance();

CREATE FUNCTION "Payments".check_review_resolution_has_ledger() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF NEW."Status" = 'Resolved' AND NOT EXISTS (
        SELECT 1 FROM "Payments"."LedgerEntries" e
        LEFT JOIN "Payments"."Refunds" r ON r."Id" = e."RefundId"
        LEFT JOIN "Payments"."Settlements" s ON s."Id" = e."SettlementId"
        WHERE e."PaymentReviewCaseId" = NEW."Id" OR r."PaymentReviewCaseId" = NEW."Id" OR s."PaymentReviewCaseId" = NEW."Id"
    ) THEN RAISE EXCEPTION 'Resolved PaymentReviewCase requires a resulting LedgerEntry' USING ERRCODE = '23514'; END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER "TR_PaymentReviewCases_ResolvedLedger" AFTER INSERT OR UPDATE ON "Payments"."PaymentReviewCases" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Payments".check_review_resolution_has_ledger();

CREATE FUNCTION "Payments".guard_webhook_payload() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
DECLARE redact_allowed boolean := COALESCE(current_setting('payments.allow_pii_redaction', true), 'false') = 'true';
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Webhook payload manifests cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF NEW."EventId" IS DISTINCT FROM OLD."EventId" THEN RAISE EXCEPTION 'Webhook payload EventId is immutable' USING ERRCODE = '55000'; END IF;
    IF OLD."ArchiveObjectKey" IS NOT NULL AND NEW."ArchiveObjectKey" IS DISTINCT FROM OLD."ArchiveObjectKey" AND NOT (redact_allowed AND NEW."ArchiveState" = 'Redacted') THEN RAISE EXCEPTION 'Archive object key is set once' USING ERRCODE = '55000'; END IF;
    IF OLD."ContentHash" IS NOT NULL AND NEW."ContentHash" IS DISTINCT FROM OLD."ContentHash" AND NOT (redact_allowed AND NEW."ArchiveState" = 'Redacted') THEN RAISE EXCEPTION 'Archive content hash is set once' USING ERRCODE = '55000'; END IF;
    IF NEW."ArchiveState" = 'Redacted' AND current_user = 'payments_migration' AND redact_allowed AND NEW."RedactedAt" IS NOT NULL AND
       NEW."Payload" IS NOT DISTINCT FROM "Payments".redact_sensitive_jsonb(OLD."Payload") AND
       NEW."EventId" = OLD."EventId" AND NEW."ArchiveObjectKey" IS NOT DISTINCT FROM OLD."ArchiveObjectKey" AND
       NEW."ContentHash" IS NOT DISTINCT FROM OLD."ContentHash" AND NEW."ArchivedAt" IS NOT DISTINCT FROM OLD."ArchivedAt" AND
       NEW."LastArchiveAttemptAt" IS NOT DISTINCT FROM OLD."LastArchiveAttemptAt" THEN RETURN NEW; END IF;
    IF (OLD."ArchiveState" = 'Hot' AND NEW."ArchiveState" = 'Uploading' AND NEW."Payload" IS NOT DISTINCT FROM OLD."Payload") OR
       (OLD."ArchiveState" = 'Uploading' AND NEW."ArchiveState" = 'Archived' AND NEW."Payload" IS NULL AND NEW."ArchiveObjectKey" IS NOT NULL AND NEW."ContentHash" IS NOT NULL AND NEW."ArchivedAt" IS NOT NULL) OR
       (OLD."ArchiveState" = 'Uploading' AND NEW."ArchiveState" = 'Hot' AND NEW."Payload" IS NOT NULL) OR
       (OLD."ArchiveState" = 'Uploading' AND NEW."ArchiveState" = 'Uploading' AND NEW."Payload" IS NOT DISTINCT FROM OLD."Payload" AND NEW."LastArchiveAttemptAt" IS NOT NULL) THEN RETURN NEW; END IF;
    RAISE EXCEPTION 'Invalid webhook payload archive lifecycle transition' USING ERRCODE = '55000';
END;
$$;
CREATE TRIGGER "TR_WebhookEventPayloads_Lifecycle" BEFORE UPDATE OR DELETE ON "Payments"."WebhookEventPayloads" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_webhook_payload();
CREATE TRIGGER "TR_WebhookEventPayloads_NoTruncate" BEFORE TRUNCATE ON "Payments"."WebhookEventPayloads" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".redact_sensitive_jsonb(p_value jsonb) RETURNS jsonb
LANGUAGE plpgsql IMMUTABLE STRICT SET search_path = pg_catalog, "Payments" AS $$
DECLARE result jsonb; item record; arr jsonb := '[]'::jsonb; key_name text;
BEGIN
    CASE jsonb_typeof(p_value)
      WHEN 'object' THEN
        result := '{}'::jsonb;
        FOR item IN SELECT key, value FROM jsonb_each(p_value) LOOP
            key_name := lower(item.key);
            IF key_name IN ('email', 'name', 'billing_details', 'shipping_details', 'phone', 'last4', 'sender_name', 'sender_address', 'peer_address') THEN CONTINUE; END IF;
            result := result || jsonb_build_object(item.key, "Payments".redact_sensitive_jsonb(item.value));
        END LOOP;
        RETURN result;
      WHEN 'array' THEN
        FOR item IN SELECT value FROM jsonb_array_elements(p_value) LOOP arr := arr || jsonb_build_array("Payments".redact_sensitive_jsonb(item.value)); END LOOP;
        RETURN arr;
      ELSE RETURN p_value;
    END CASE;
END;
$$;

CREATE FUNCTION "Payments".redact_webhook_pii(p_user_id uuid, p_actor_id uuid) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, "Payments" AS $$
DECLARE run_id uuid := gen_random_uuid(); affected integer;
BEGIN
    IF p_user_id IS NULL OR p_actor_id IS NULL THEN RAISE EXCEPTION 'User and actor IDs are required' USING ERRCODE = '22004'; END IF;
    PERFORM set_config('payments.allow_pii_redaction', 'true', true);
    UPDATE "Payments"."WebhookEventPayloads" p
       SET "Payload" = "Payments".redact_sensitive_jsonb(p."Payload"), "ArchiveState" = 'Redacted', "RedactedAt" = now()
      WHERE p."Payload" IS NOT NULL
        AND (p."Payload" @> jsonb_build_object('userId', p_user_id::text)
             OR p."Payload" @> jsonb_build_object('user_id', p_user_id::text)
             OR p."Payload" @> jsonb_build_object('metadata', jsonb_build_object('user_id', p_user_id::text))
             OR p."Payload" @> jsonb_build_object('data', jsonb_build_object('object', jsonb_build_object('metadata', jsonb_build_object('user_id', p_user_id::text)))));
    GET DIAGNOSTICS affected = ROW_COUNT;
    INSERT INTO "Payments"."ReconciliationRuns" ("Id", "ProviderAccountId", "Kind", "StartedAt", "FinishedAt", "Status", "Counts", "TriggeredBy")
    VALUES (run_id, NULL, 'PiiErasure', now(), now(), 'Completed', jsonb_build_object('redactedPayloads', affected), p_actor_id::text);
    INSERT INTO "Payments"."PaymentsAuditLogs" ("Id", "EventId", "Action", "AggregateId", "ActorUserId", "Payload", "OccurredAt")
    VALUES (gen_random_uuid(), gen_random_uuid(), 'WebhookPayload.PiiErased', p_user_id, p_actor_id, jsonb_build_object('runId', run_id, 'redactedPayloads', affected), now());
    RETURN run_id;
END;
$$;

CREATE FUNCTION "Payments".guard_webhook_immutable() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Webhook event identity is retained' USING ERRCODE = '55000'; END IF;
    IF (NEW."Id", NEW."ProviderName", NEW."ProviderAccountId", NEW."ExternalEventId", NEW."EventType", NEW."ExternalObjectId", NEW."SignatureVerified", NEW."Environment", NEW."Livemode", NEW."ReceivedAt") IS DISTINCT FROM
       (OLD."Id", OLD."ProviderName", OLD."ProviderAccountId", OLD."ExternalEventId", OLD."EventType", OLD."ExternalObjectId", OLD."SignatureVerified", OLD."Environment", OLD."Livemode", OLD."ReceivedAt") OR
       NEW."DeliveryCount" < OLD."DeliveryCount" THEN
        RAISE EXCEPTION 'Webhook event identity is immutable and DeliveryCount cannot decrease' USING ERRCODE = '55000';
    END IF;
    IF NEW."ProcessingOutcome" IS DISTINCT FROM OLD."ProcessingOutcome" AND OLD."ProcessingOutcome" IS NOT NULL AND OLD."ProcessingOutcome" <> 'Failed' THEN
        RAISE EXCEPTION 'Successful webhook outcome is immutable; Failed events may be re-driven' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_WebhookEvents_Immutable" BEFORE UPDATE OR DELETE ON "Payments"."WebhookEvents" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_webhook_immutable();
CREATE TRIGGER "TR_WebhookEvents_NoTruncate" BEFORE TRUNCATE ON "Payments"."WebhookEvents" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".guard_reconciliation_run() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Reconciliation run history cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF OLD."Status" <> 'Running' OR NEW."Status" NOT IN ('Completed', 'Failed') OR
       (NEW."Id", NEW."ProviderAccountId", NEW."Kind", NEW."StartedAt", NEW."TriggeredBy") IS DISTINCT FROM
       (OLD."Id", OLD."ProviderAccountId", OLD."Kind", OLD."StartedAt", OLD."TriggeredBy") THEN RAISE EXCEPTION 'Invalid reconciliation run transition' USING ERRCODE = '55000'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_ReconciliationRuns_Transition" BEFORE UPDATE OR DELETE ON "Payments"."ReconciliationRuns" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_reconciliation_run();
CREATE TRIGGER "TR_ReconciliationRuns_NoTruncate" BEFORE TRUNCATE ON "Payments"."ReconciliationRuns" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".guard_reconciliation_finding() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, "Payments" AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Reconciliation findings cannot be deleted' USING ERRCODE = '55000'; END IF;
    IF OLD."ResolvedAt" IS NOT NULL OR NEW."ResolvedAt" IS NULL OR NEW."ResolvedByUserId" IS NULL OR
       (NEW."Id", NEW."RunId", NEW."Severity", NEW."Kind", NEW."AggregateId", NEW."Detail") IS DISTINCT FROM
       (OLD."Id", OLD."RunId", OLD."Severity", OLD."Kind", OLD."AggregateId", OLD."Detail") THEN RAISE EXCEPTION 'Finding permits only one resolution stamp' USING ERRCODE = '55000'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER "TR_ReconciliationFindings_Resolution" BEFORE UPDATE OR DELETE ON "Payments"."ReconciliationFindings" FOR EACH ROW EXECUTE FUNCTION "Payments".guard_reconciliation_finding();
CREATE TRIGGER "TR_ReconciliationFindings_NoTruncate" BEFORE TRUNCATE ON "Payments"."ReconciliationFindings" FOR EACH STATEMENT EXECUTE FUNCTION "Payments".deny_mutation();

CREATE FUNCTION "Payments".post_ledger_entry(p_posting jsonb) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, "Payments" AS $$
DECLARE entry_id uuid; header jsonb; lines jsonb;
BEGIN
    IF jsonb_typeof(p_posting) <> 'object' OR jsonb_typeof(p_posting->'entry') <> 'object' OR jsonb_typeof(p_posting->'lines') <> 'array' THEN
        RAISE EXCEPTION 'Posting must have entry object and lines array' USING ERRCODE = '22023';
    END IF;
    header := p_posting->'entry'; lines := p_posting->'lines';
    IF jsonb_array_length(lines) < 2 THEN RAISE EXCEPTION 'Ledger posting requires at least two lines' USING ERRCODE = '23514'; END IF;
    INSERT INTO "Payments"."LedgerEntries" (
        "Id", "EffectiveAt", "Type", "TenantId", "ProviderAccountId", "ProviderTransactionId", "PurchaseId", "RefundId", "SettlementId", "DisputeId", "PaymentReviewCaseId", "ExternalObjectId", "CreatedByUserId", "Description")
    VALUES (
        (header->>'id')::uuid, NULLIF(header->>'effectiveAt', '')::timestamptz, header->>'type', NULLIF(header->>'tenantId', '')::uuid,
        NULLIF(header->>'providerAccountId', '')::uuid, NULLIF(header->>'providerTransactionId', '')::uuid, NULLIF(header->>'purchaseId', '')::uuid,
        NULLIF(header->>'refundId', '')::uuid, NULLIF(header->>'settlementId', '')::uuid, NULLIF(header->>'disputeId', '')::uuid,
        NULLIF(header->>'paymentReviewCaseId', '')::uuid, header->>'externalObjectId', NULLIF(header->>'createdByUserId', '')::uuid, header->>'description')
    RETURNING "Id" INTO entry_id;
    INSERT INTO "Payments"."LedgerLines" ("Id", "EntryId", "AccountId", "Direction", "Amount", "Currency", "TenantId")
    SELECT (l->>'id')::uuid, entry_id, (l->>'accountId')::uuid, l->>'direction', (l->>'amount')::numeric, l->>'currency', NULLIF(l->>'tenantId', '')::uuid
      FROM jsonb_array_elements(lines) AS x(l);
    RETURN entry_id;
END;
$$;
ALTER FUNCTION "Payments".post_ledger_entry(jsonb) OWNER TO payments_migration;
REVOKE ALL ON FUNCTION "Payments".post_ledger_entry(jsonb) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION "Payments".post_ledger_entry(jsonb) TO payments_runtime;
ALTER FUNCTION "Payments".redact_webhook_pii(uuid, uuid) OWNER TO payments_migration;
REVOKE ALL ON FUNCTION "Payments".redact_webhook_pii(uuid, uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION "Payments".redact_webhook_pii(uuid, uuid) TO payments_pii_erasure;

-- Append-only tables: row-level UPDATE/DELETE and statement-level TRUNCATE guards.
DO $triggers$
DECLARE table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY['LedgerEntries', 'LedgerLines', 'PaymentsAuditLogs'] LOOP
        EXECUTE format('CREATE TRIGGER %I BEFORE UPDATE OR DELETE ON %I.%I FOR EACH ROW EXECUTE FUNCTION %I.deny_mutation()', 'TR_' || table_name || '_Immutable', 'Payments', table_name, 'Payments');
        EXECUTE format('CREATE TRIGGER %I BEFORE TRUNCATE ON %I.%I FOR EACH STATEMENT EXECUTE FUNCTION %I.deny_mutation()', 'TR_' || table_name || '_NoTruncate', 'Payments', table_name, 'Payments');
    END LOOP;
END
$triggers$;

-- Runtime has no ownership/DDL rights. SELECT is explicit per Payments table;
-- writes are constrained by workflow and transition guards. Ledger INSERT is function-only.
GRANT USAGE ON SCHEMA "Payments" TO payments_runtime, payments_pii_erasure;
GRANT SELECT ON ALL TABLES IN SCHEMA "Payments" TO payments_runtime;
GRANT INSERT ON "Payments"."ProviderAccounts", "Payments"."ProviderCurrencyRoutes", "Payments"."LedgerAccounts",
    "Payments"."Products", "Payments"."Prices", "Payments"."AcademyPaymentSettings", "Payments"."CheckoutSessions",
    "Payments"."ProviderTransactions", "Payments"."WebhookEvents", "Payments"."WebhookEventPayloads",
    "Payments"."Purchases", "Payments"."PaymentReviewCases", "Payments"."Refunds", "Payments"."RefundRequests",
    "Payments"."Disputes", "Payments"."Settlements", "Payments"."AcademyProviderRiskOverrides",
    "Payments"."ProductDailyPurchases", "Payments"."PaymentsAuditLogs", "Payments"."ReconciliationRuns",
    "Payments"."ReconciliationFindings"
    TO payments_runtime;
REVOKE INSERT, UPDATE, DELETE ON "Payments"."Currencies", "Payments"."PaymentProviders" FROM payments_runtime;
REVOKE UPDATE, DELETE ON "Payments"."ProviderTransactions", "Payments"."Settlements", "Payments"."PaymentsAuditLogs" FROM payments_runtime;
GRANT UPDATE ON "Payments"."PaymentProviders", "Payments"."ProviderAccounts", "Payments"."ProviderCurrencyRoutes",
    "Payments"."Products", "Payments"."Prices", "Payments"."AcademyPaymentSettings", "Payments"."CheckoutSessions",
    "Payments"."Purchases", "Payments"."Refunds", "Payments"."RefundRequests", "Payments"."Disputes",
    "Payments"."PaymentReviewCases", "Payments"."WebhookEvents", "Payments"."WebhookEventPayloads",
    "Payments"."AcademyProviderRiskOverrides", "Payments"."ReconciliationRuns", "Payments"."ReconciliationFindings"
    TO payments_runtime;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA "Payments" TO payments_runtime;
REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON "Payments"."LedgerEntries", "Payments"."LedgerLines" FROM PUBLIC, payments_runtime;
REVOKE UPDATE, DELETE, TRUNCATE ON "Payments"."PaymentsAuditLogs", "Payments"."ProviderTransactions", "Payments"."Settlements" FROM PUBLIC, payments_runtime;
REVOKE ALL ON TABLE "Payments"."__PaymentsMigrationHistory" FROM PUBLIC, payments_runtime;

INSERT INTO "Payments"."__PaymentsMigrationHistory" ("MigrationId", "ProductVersion")
VALUES ('20261120000000_InitialPayments', 'PostgreSQL-SQL-1');

-- Future tables remain migration-owned; the runtime role receives no default DDL privilege.
ALTER DEFAULT PRIVILEGES FOR ROLE payments_migration IN SCHEMA "Payments" REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES FOR ROLE payments_migration IN SCHEMA "Payments" REVOKE ALL ON SEQUENCES FROM PUBLIC;

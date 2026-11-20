using System;
using System.Collections.Generic;
using AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated;

public partial class AppDbContext : DbContext
{
    public const string Schema = "Payments";
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AcademyPaymentSettingDataModel> AcademyPaymentSettings { get; set; }

    public virtual DbSet<AcademyProviderRiskOverrideDataModel> AcademyProviderRiskOverrides { get; set; }

    public virtual DbSet<CheckoutSessionDataModel> CheckoutSessions { get; set; }

    public virtual DbSet<CurrencyDataModel> Currencies { get; set; }

    public virtual DbSet<DisputeDataModel> Disputes { get; set; }

    public virtual DbSet<LedgerAccountDataModel> LedgerAccounts { get; set; }

    public virtual DbSet<LedgerEntryDataModel> LedgerEntries { get; set; }

    public virtual DbSet<LedgerLineDataModel> LedgerLines { get; set; }

    public virtual DbSet<PaymentProviderDataModel> PaymentProviders { get; set; }

    public virtual DbSet<PaymentReviewCaseDataModel> PaymentReviewCases { get; set; }

    public virtual DbSet<PaymentsAuditLogDataModel> PaymentsAuditLogs { get; set; }

    public virtual DbSet<PriceDataModel> Prices { get; set; }

    public virtual DbSet<ProductDataModel> Products { get; set; }

    public virtual DbSet<ProductDailyPurchaseDataModel> ProductDailyPurchases { get; set; }

    public virtual DbSet<ProviderAccountDataModel> ProviderAccounts { get; set; }

    public virtual DbSet<ProviderCurrencyRouteDataModel> ProviderCurrencyRoutes { get; set; }

    public virtual DbSet<ProviderTransactionDataModel> ProviderTransactions { get; set; }

    public virtual DbSet<PurchaseDataModel> Purchases { get; set; }

    public virtual DbSet<ReconciliationFindingDataModel> ReconciliationFindings { get; set; }

    public virtual DbSet<ReconciliationRunDataModel> ReconciliationRuns { get; set; }

    public virtual DbSet<RefundDataModel> Refunds { get; set; }

    public virtual DbSet<RefundRequestDataModel> RefundRequests { get; set; }

    public virtual DbSet<SettlementDataModel> Settlements { get; set; }

    public virtual DbSet<WebhookEventDataModel> WebhookEvents { get; set; }

    public virtual DbSet<WebhookEventPayloadDataModel> WebhookEventPayloads { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {


        modelBuilder.HasDefaultSchema(Schema);
        //modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        //modelBuilder.ApplyAlphaZeroGlobalFilters(this);


        modelBuilder.Entity<AcademyPaymentSettingDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AcademyPaymentSettings_pkey");

            entity.ToTable("AcademyPaymentSettings", "Payments");

            entity.HasIndex(e => e.TenantId, "AcademyPaymentSettings_TenantId_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CommissionShare).HasPrecision(5, 4);
        });

        modelBuilder.Entity<AcademyProviderRiskOverrideDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AcademyProviderRiskOverrides_pkey");

            entity.ToTable("AcademyProviderRiskOverrides", "Payments");

            entity.HasIndex(e => e.ProviderCurrencyRouteId, "IX_AcademyProviderRiskOverrides_RouteId");

            entity.HasIndex(e => new { e.TenantId, e.ProviderCurrencyRouteId }, "UQ_AcademyProviderRiskOverrides_Tenant_Route").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ReserveRate).HasPrecision(5, 4);

            entity.HasOne(d => d.ProviderCurrencyRoute).WithMany(p => p.AcademyProviderRiskOverrides)
                .HasForeignKey(d => d.ProviderCurrencyRouteId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("AcademyProviderRiskOverrides_ProviderCurrencyRouteId_fkey");
        });

        modelBuilder.Entity<CheckoutSessionDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CheckoutSessions_pkey");

            entity.ToTable("CheckoutSessions", "Payments");

            entity.HasIndex(e => e.PriceId, "IX_CheckoutSessions_PriceId");

            entity.HasIndex(e => e.ProductId, "IX_CheckoutSessions_ProductId");

            entity.HasIndex(e => e.ProviderCurrencyRouteId, "IX_CheckoutSessions_ProviderCurrencyRouteId");

            entity.HasIndex(e => new { e.TenantId, e.UserId, e.CreatedAt }, "IX_CheckoutSessions_Tenant_User_CreatedAt");

            entity.HasIndex(e => new { e.ProviderCurrencyRouteId, e.ExternalCheckoutId }, "UQ_CheckoutSessions_Route_ExternalId")
                .IsUnique()
                .HasFilter("(\"ExternalCheckoutId\" IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.ProductId }, "UQ_CheckoutSessions_User_Product_Pending")
                .IsUnique()
                .HasFilter("((\"Status\")::text = 'Pending'::text)");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ExternalCheckoutId).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(32);

            entity.HasOne(d => d.Price).WithMany(p => p.CheckoutSessions)
                .HasForeignKey(d => d.PriceId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("CheckoutSessions_PriceId_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.CheckoutSessions)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("CheckoutSessions_ProductId_fkey");

            entity.HasOne(d => d.ProviderCurrencyRoute).WithMany(p => p.CheckoutSessions)
                .HasForeignKey(d => d.ProviderCurrencyRouteId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("CheckoutSessions_ProviderCurrencyRouteId_fkey");
        });

        modelBuilder.Entity<CurrencyDataModel>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("Currencies_pkey");

            entity.ToTable("Currencies", "Payments");

            entity.Property(e => e.Code).HasMaxLength(3);
            entity.Property(e => e.Name).HasMaxLength(64);
        });

        modelBuilder.Entity<DisputeDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Disputes_pkey");

            entity.ToTable("Disputes", "Payments");

            entity.HasIndex(e => e.Currency, "IX_Disputes_Currency");

            entity.HasIndex(e => new { e.ProviderAccountId, e.Status }, "IX_Disputes_ProviderAccount_Status");

            entity.HasIndex(e => e.PurchaseId, "IX_Disputes_PurchaseId");

            entity.HasIndex(e => new { e.TenantId, e.Status }, "IX_Disputes_Tenant_Status");

            entity.HasIndex(e => new { e.ProviderAccountId, e.ExternalDisputeId }, "UQ_Disputes_Provider_ExternalDisputeId").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.ExternalDisputeId).HasMaxLength(255);
            entity.Property(e => e.OutcomeNotes).HasMaxLength(1024);
            entity.Property(e => e.ProviderDisputeFee).HasPrecision(18, 4);
            entity.Property(e => e.Reason).HasMaxLength(64);
            entity.Property(e => e.Status).HasMaxLength(32);

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Disputes_Currency_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Disputes_ProviderAccountId_fkey");

            entity.HasOne(d => d.Purchase).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Disputes_PurchaseId_fkey");
        });

        modelBuilder.Entity<LedgerAccountDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LedgerAccounts_pkey");

            entity.ToTable("LedgerAccounts", "Payments");

            entity.HasIndex(e => e.Currency, "IX_LedgerAccounts_Currency");

            entity.HasIndex(e => e.ProviderAccountId, "IX_LedgerAccounts_ProviderAccountId");

            entity.HasIndex(e => new { e.TenantId, e.ProviderAccountId, e.Currency }, "UQ_LedgerAccounts_AcademyPayable")
                .IsUnique()
                .HasFilter("((\"Role\")::text = 'AcademyPayable'::text)");

            entity.HasIndex(e => new { e.Role, e.Currency }, "UQ_LedgerAccounts_BankCash")
                .IsUnique()
                .HasFilter("((\"ProviderAccountId\" IS NULL) AND (\"TenantId\" IS NULL))");

            entity.HasIndex(e => new { e.Role, e.ProviderAccountId, e.Currency }, "UQ_LedgerAccounts_Structural")
                .IsUnique()
                .HasFilter("((\"TenantId\" IS NULL) AND (\"ProviderAccountId\" IS NOT NULL))");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Name).HasMaxLength(64);
            entity.Property(e => e.Role).HasMaxLength(32);
            entity.Property(e => e.Type).HasMaxLength(16);

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.LedgerAccounts)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerAccounts_Currency_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.LedgerAccounts)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerAccounts_ProviderAccountId_fkey");
        });

        modelBuilder.Entity<LedgerEntryDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LedgerEntries_pkey");

            entity.ToTable("LedgerEntries", "Payments");

            entity.HasIndex(e => e.DisputeId, "IX_LedgerEntries_DisputeId");

            entity.HasIndex(e => e.PaymentReviewCaseId, "IX_LedgerEntries_PaymentReviewCaseId");

            entity.HasIndex(e => new { e.ProviderAccountId, e.Type, e.PostedAt }, "IX_LedgerEntries_ProviderAccount_Type_PostedAt");

            entity.HasIndex(e => e.ProviderTransactionId, "IX_LedgerEntries_ProviderTransactionId");

            entity.HasIndex(e => e.PurchaseId, "IX_LedgerEntries_PurchaseId");

            entity.HasIndex(e => e.RefundId, "IX_LedgerEntries_RefundId");

            entity.HasIndex(e => e.SettlementId, "IX_LedgerEntries_SettlementId");

            entity.HasIndex(e => e.TenantId, "IX_LedgerEntries_TenantId").HasFilter("(\"TenantId\" IS NOT NULL)");

            entity.HasIndex(e => new { e.DisputeId, e.Type }, "UQ_LedgerEntries_Dispute_Type")
                .IsUnique()
                .HasFilter("(\"DisputeId\" IS NOT NULL)");

            entity.HasIndex(e => new { e.ProviderTransactionId, e.Type }, "UQ_LedgerEntries_ProviderTransaction_Type")
                .IsUnique()
                .HasFilter("(\"ProviderTransactionId\" IS NOT NULL)");

            entity.HasIndex(e => e.PurchaseId, "UQ_LedgerEntries_Purchase_Funding")
                .IsUnique()
                .HasFilter("((\"Type\")::text = ANY ((ARRAY['Capture'::character varying, 'ApplyUnappliedFunds'::character varying])::text[]))");

            entity.HasIndex(e => e.RefundId, "UQ_LedgerEntries_Refund")
                .IsUnique()
                .HasFilter("((\"Type\")::text = 'RefundReversal'::text)");

            entity.HasIndex(e => e.SettlementId, "UQ_LedgerEntries_Settlement")
                .IsUnique()
                .HasFilter("(\"SettlementId\" IS NOT NULL)");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(256);
            entity.Property(e => e.ExternalObjectId).HasMaxLength(255);
            entity.Property(e => e.PostedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Type).HasMaxLength(32);

            entity.HasOne(d => d.Dispute).WithMany(p => p.LedgerEntries)
                .HasForeignKey(d => d.DisputeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.PaymentReviewCase).WithMany(p => p.LedgerEntries)
                .HasForeignKey(d => d.PaymentReviewCaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerEntries_PaymentReviewCaseId_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.LedgerEntries)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerEntries_ProviderAccountId_fkey");

            entity.HasOne(d => d.ProviderTransaction).WithMany(p => p.LedgerEntries)
                .HasForeignKey(d => d.ProviderTransactionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerEntries_ProviderTransactionId_fkey");

            entity.HasOne(d => d.Purchase).WithOne(p => p.LedgerEntry)
                .HasForeignKey<LedgerEntryDataModel>(d => d.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerEntries_PurchaseId_fkey");

            entity.HasOne(d => d.Refund).WithOne(p => p.LedgerEntry)
                .HasForeignKey<LedgerEntryDataModel>(d => d.RefundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Settlement).WithOne(p => p.LedgerEntry)
                .HasForeignKey<LedgerEntryDataModel>(d => d.SettlementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerLineDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LedgerLines_pkey");

            entity.ToTable("LedgerLines", "Payments");

            entity.HasIndex(e => new { e.AccountId, e.Direction }, "IX_LedgerLines_Account_Direction");

            entity.HasIndex(e => e.Currency, "IX_LedgerLines_Currency");

            entity.HasIndex(e => e.EntryId, "IX_LedgerLines_EntryId");

            entity.HasIndex(e => new { e.TenantId, e.AccountId }, "IX_LedgerLines_Tenant_Account").HasFilter("(\"TenantId\" IS NOT NULL)");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Direction).HasMaxLength(8);

            entity.HasOne(d => d.Account).WithMany(p => p.LedgerLines)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerLines_AccountId_fkey");

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.LedgerLines)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerLines_Currency_fkey");

            entity.HasOne(d => d.Entry).WithMany(p => p.LedgerLines)
                .HasForeignKey(d => d.EntryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("LedgerLines_EntryId_fkey");
        });

        modelBuilder.Entity<PaymentProviderDataModel>(entity =>
        {
            entity.HasKey(e => e.Name).HasName("PaymentProviders_pkey");

            entity.ToTable("PaymentProviders", "Payments");

            entity.Property(e => e.Name).HasMaxLength(32);
            entity.Property(e => e.DisplayName).HasMaxLength(64);
        });

        modelBuilder.Entity<PaymentReviewCaseDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PaymentReviewCases_pkey");

            entity.ToTable("PaymentReviewCases", "Payments");

            entity.HasIndex(e => e.CheckoutSessionId, "IX_PaymentReviewCases_CheckoutSessionId");

            entity.HasIndex(e => e.Currency, "IX_PaymentReviewCases_Currency");

            entity.HasIndex(e => e.PayableAccountId, "IX_PaymentReviewCases_PayableAccountId");

            entity.HasIndex(e => e.ProviderAccountId, "IX_PaymentReviewCases_ProviderAccountId");

            entity.HasIndex(e => e.ProviderTransactionId, "IX_PaymentReviewCases_ProviderTransactionId");

            entity.HasIndex(e => e.PurchaseId, "IX_PaymentReviewCases_PurchaseId");

            entity.HasIndex(e => e.SourceAccountId, "IX_PaymentReviewCases_SourceAccountId");

            entity.HasIndex(e => e.PayableAccountId, "UQ_PaymentReviewCases_InFlightSettlement")
                .IsUnique()
                .HasFilter("(((\"Type\")::text = 'Settlement'::text) AND ((\"Status\")::text = ANY ((ARRAY['Open'::character varying, 'Approved'::character varying])::text[])))");

            entity.HasIndex(e => e.ProviderTransactionId, "UQ_PaymentReviewCases_Open_ProviderTransaction")
                .IsUnique()
                .HasFilter("((\"ProviderTransactionId\" IS NOT NULL) AND ((\"Status\")::text = 'Open'::text))");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.DestinationSnapshot).HasColumnType("jsonb");
            entity.Property(e => e.EvidenceReference).HasMaxLength(512);
            entity.Property(e => e.ProviderSupportReference).HasMaxLength(255);
            entity.Property(e => e.ReasonCode).HasMaxLength(32);
            entity.Property(e => e.Resolution).HasMaxLength(1024);
            entity.Property(e => e.Status).HasMaxLength(16);
            entity.Property(e => e.Type).HasMaxLength(32);

            entity.HasOne(d => d.CheckoutSession).WithMany(p => p.PaymentReviewCases)
                .HasForeignKey(d => d.CheckoutSessionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("PaymentReviewCases_CheckoutSessionId_fkey");

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.PaymentReviewCases)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("PaymentReviewCases_Currency_fkey");

            entity.HasOne(d => d.PayableAccount).WithOne(p => p.PaymentReviewCasePayableAccount)
                .HasForeignKey<PaymentReviewCaseDataModel>(d => d.PayableAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("PaymentReviewCases_PayableAccountId_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.PaymentReviewCases)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("PaymentReviewCases_ProviderAccountId_fkey");

            entity.HasOne(d => d.ProviderTransaction).WithOne(p => p.PaymentReviewCase)
                .HasForeignKey<PaymentReviewCaseDataModel>(d => d.ProviderTransactionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("PaymentReviewCases_ProviderTransactionId_fkey");

            entity.HasOne(d => d.Purchase).WithMany(p => p.PaymentReviewCases)
                .HasForeignKey(d => d.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("PaymentReviewCases_PurchaseId_fkey");

            entity.HasOne(d => d.SourceAccount).WithMany(p => p.PaymentReviewCaseSourceAccounts)
                .HasForeignKey(d => d.SourceAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("PaymentReviewCases_SourceAccountId_fkey");
        });

        modelBuilder.Entity<PaymentsAuditLogDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PaymentsAuditLogs_pkey");

            entity.ToTable("PaymentsAuditLogs", "Payments");

            entity.HasIndex(e => e.AggregateId, "IX_PaymentsAuditLogs_AggregateId");

            entity.HasIndex(e => new { e.TenantId, e.Action, e.OccurredAt }, "IX_PaymentsAuditLogs_Tenant_Action_OccurredAt");

            entity.HasIndex(e => e.EventId, "PaymentsAuditLogs_EventId_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Action).HasMaxLength(64);
            entity.Property(e => e.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<PriceDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Prices_pkey");

            entity.ToTable("Prices", "Payments");

            entity.HasIndex(e => e.Currency, "IX_Prices_Currency");

            entity.HasIndex(e => e.ProductId, "IX_Prices_ProductId");

            entity.HasIndex(e => new { e.TenantId, e.ProductId }, "IX_Prices_Tenant_ProductId");

            entity.HasIndex(e => new { e.ProductId, e.Currency }, "UQ_Prices_Product_Currency_Active")
                .IsUnique()
                .HasFilter("\"IsActive\"");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.Prices)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Prices_Currency_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.Prices)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Prices_ProductId_fkey");
        });

        modelBuilder.Entity<ProductDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Products_pkey");

            entity.ToTable("Products", "Payments");

            entity.HasIndex(e => new { e.TenantId, e.ParentContainerId }, "IX_Products_Tenant_ParentContainerId");

            entity.HasIndex(e => new { e.TenantId, e.ItemType, e.ItemId }, "UQ_Products_Tenant_Item").IsUnique();

            entity.HasIndex(e => new { e.TenantId, e.TargetResourceArn }, "UQ_Products_Tenant_TargetResourceArn").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ItemType)
                .HasMaxLength(32)
                .HasDefaultValueSql("'CoursePlan'::character varying");
            entity.Property(e => e.SourceAvailability)
                .HasMaxLength(16)
                .HasDefaultValueSql("'Unavailable'::character varying");
            entity.Property(e => e.TargetResourceArn).HasMaxLength(512);
            entity.Property(e => e.Title).HasMaxLength(255);
        });

        modelBuilder.Entity<ProductDailyPurchaseDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProductDailyPurchases_pkey");

            entity.ToTable("ProductDailyPurchases", "Payments");

            entity.HasIndex(e => e.SettlementCurrency, "IX_ProductDailyPurchases_Currency");

            entity.HasIndex(e => e.ProviderAccountId, "IX_ProductDailyPurchases_ProviderAccountId");

            entity.HasIndex(e => new { e.TenantId, e.ProviderAccountId, e.Day }, "IX_ProductDailyPurchases_Tenant_Provider_Day");

            entity.HasIndex(e => new { e.TenantId, e.ProviderAccountId, e.ProductId, e.Day, e.SettlementCurrency }, "UQ_ProductDailyPurchases_Grain").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CommissionAmount).HasPrecision(18, 4);
            entity.Property(e => e.GrossAmount).HasPrecision(18, 4);
            entity.Property(e => e.NetAcademyAmount).HasPrecision(18, 4);
            entity.Property(e => e.RefundedCommissionAmount).HasPrecision(18, 4);
            entity.Property(e => e.RefundedGrossAmount).HasPrecision(18, 4);
            entity.Property(e => e.RefundedNetAcademyAmount).HasPrecision(18, 4);
            entity.Property(e => e.SettlementCurrency).HasMaxLength(3);

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.ProductDailyPurchases)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ProductDailyPurchases_ProviderAccountId_fkey");

            entity.HasOne(d => d.SettlementCurrencyNavigation).WithMany(p => p.ProductDailyPurchases)
                .HasForeignKey(d => d.SettlementCurrency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ProductDailyPurchases_SettlementCurrency_fkey");
        });

        modelBuilder.Entity<ProviderAccountDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProviderAccounts_pkey");

            entity.ToTable("ProviderAccounts", "Payments");

            entity.HasIndex(e => e.ProviderName, "IX_ProviderAccounts_ProviderName");

            entity.HasIndex(e => new { e.ProviderName, e.Environment, e.ExternalAccountId }, "UQ_ProviderAccounts_Identity").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CredentialSecretName).HasMaxLength(255);
            entity.Property(e => e.Environment).HasMaxLength(8);
            entity.Property(e => e.ExternalAccountId).HasMaxLength(255);
            entity.Property(e => e.ProviderName).HasMaxLength(32);
            entity.Property(e => e.Status).HasMaxLength(16);
            entity.Property(e => e.WebhookSecretName).HasMaxLength(255);

            entity.HasOne(d => d.ProviderNameNavigation).WithMany(p => p.ProviderAccounts)
                .HasForeignKey(d => d.ProviderName)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ProviderAccounts_ProviderName_fkey");
        });

        modelBuilder.Entity<ProviderCurrencyRouteDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProviderCurrencyRoutes_pkey");

            entity.ToTable("ProviderCurrencyRoutes", "Payments");

            entity.HasIndex(e => e.IntegrationCurrency, "IX_ProviderCurrencyRoutes_IntegrationCurrency");

            entity.HasIndex(e => e.ProviderAccountId, "IX_ProviderCurrencyRoutes_ProviderAccountId");

            entity.HasIndex(e => e.SettlementCurrency, "IX_ProviderCurrencyRoutes_SettlementCurrency");

            entity.HasIndex(e => new { e.ProviderAccountId, e.IntegrationCurrency }, "UQ_ProviderCurrencyRoutes_Account_IntegrationCurrency").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.IntegrationCurrency).HasMaxLength(3);
            entity.Property(e => e.PresentmentMode).HasMaxLength(16);
            entity.Property(e => e.ReserveRate).HasPrecision(5, 4);
            entity.Property(e => e.SettlementCurrency).HasMaxLength(3);

            entity.HasOne(d => d.IntegrationCurrencyNavigation).WithMany(p => p.ProviderCurrencyRouteIntegrationCurrencyNavigations)
                .HasForeignKey(d => d.IntegrationCurrency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ProviderCurrencyRoutes_IntegrationCurrency_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.ProviderCurrencyRoutes)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ProviderCurrencyRoutes_ProviderAccountId_fkey");

            entity.HasOne(d => d.SettlementCurrencyNavigation).WithMany(p => p.ProviderCurrencyRouteSettlementCurrencyNavigations)
                .HasForeignKey(d => d.SettlementCurrency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ProviderCurrencyRoutes_SettlementCurrency_fkey");
        });

        modelBuilder.Entity<ProviderTransactionDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProviderTransactions_pkey");

            entity.ToTable("ProviderTransactions", "Payments");

            entity.HasIndex(e => e.Currency, "IX_ProviderTransactions_Currency");

            entity.HasIndex(e => e.ExternalObjectId, "IX_ProviderTransactions_ExternalObjectId").HasFilter("(\"ExternalObjectId\" IS NOT NULL)");

            entity.HasIndex(e => new { e.ProviderAccountId, e.ExternalPaymentId }, "IX_ProviderTransactions_ProviderAccountId_ExternalPaymentId");

            entity.HasIndex(e => new { e.ProviderAccountId, e.OccurredAt }, "IX_ProviderTransactions_ProviderAccountId_OccurredAt");

            entity.HasIndex(e => e.WebhookEventId, "IX_ProviderTransactions_WebhookEventId");

            entity.HasIndex(e => new { e.ProviderAccountId, e.ExternalTransactionId }, "UQ_ProviderTransactions_Account_ExternalTransactionId").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Direction).HasMaxLength(8);
            entity.Property(e => e.ExternalObjectId).HasMaxLength(255);
            entity.Property(e => e.ExternalPaymentId).HasMaxLength(255);
            entity.Property(e => e.ExternalTransactionId).HasMaxLength(255);
            entity.Property(e => e.FeeAmount).HasPrecision(18, 4);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Type).HasMaxLength(24);

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.ProviderTransactions)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ProviderTransactions_Currency_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.ProviderTransactions)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ProviderTransactions_ProviderAccountId_fkey");

            entity.HasOne(d => d.WebhookEvent).WithMany(p => p.ProviderTransactions)
                .HasForeignKey(d => d.WebhookEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Purchases_pkey");

            entity.ToTable("Purchases", "Payments");

            entity.HasIndex(e => e.PresentmentCurrency, "IX_Purchases_PresentmentCurrency");

            entity.HasIndex(e => e.PriceId, "IX_Purchases_PriceId");

            entity.HasIndex(e => e.ProductId, "IX_Purchases_ProductId");

            entity.HasIndex(e => e.ProviderAccountId, "IX_Purchases_ProviderAccountId");

            entity.HasIndex(e => new { e.ProviderAccountId, e.PaidAt }, "IX_Purchases_ProviderAccount_PaidAt");

            entity.HasIndex(e => e.SettlementCurrency, "IX_Purchases_SettlementCurrency");

            entity.HasIndex(e => new { e.TenantId, e.PaidAt }, "IX_Purchases_Tenant_PaidAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.TenantId, e.ProductId, e.PaidAt }, "IX_Purchases_Tenant_Product_PaidAt");

            entity.HasIndex(e => new { e.TenantId, e.Status, e.PaidAt }, "IX_Purchases_Tenant_Status_PaidAt").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.TenantId, e.UserId, e.PaidAt }, "IX_Purchases_Tenant_User_PaidAt");

            entity.HasIndex(e => e.CheckoutSessionId, "Purchases_CheckoutSessionId_key").IsUnique();

            entity.HasIndex(e => e.ProviderTransactionId, "Purchases_ProviderTransactionId_key").IsUnique();

            entity.HasIndex(e => new { e.ProviderAccountId, e.ExternalPaymentId }, "UQ_Purchases_Provider_ExternalPaymentId").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CommissionShare).HasPrecision(5, 4);
            entity.Property(e => e.ExternalPaymentId).HasMaxLength(255);
            entity.Property(e => e.FxRate).HasPrecision(18, 8);
            entity.Property(e => e.ItemTitle).HasMaxLength(255);
            entity.Property(e => e.PresentmentAmount).HasPrecision(18, 4);
            entity.Property(e => e.PresentmentCurrency).HasMaxLength(3);
            entity.Property(e => e.ProviderFee).HasPrecision(18, 4);
            entity.Property(e => e.SettlementAmount).HasPrecision(18, 4);
            entity.Property(e => e.SettlementCurrency).HasMaxLength(3);
            entity.Property(e => e.Status).HasMaxLength(32);

            entity.HasOne(d => d.CheckoutSession).WithOne(p => p.Purchase)
                .HasForeignKey<PurchaseDataModel>(d => d.CheckoutSessionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Purchases_CheckoutSessionId_fkey");

            entity.HasOne(d => d.PresentmentCurrencyNavigation).WithMany(p => p.PurchasePresentmentCurrencyNavigations)
                .HasForeignKey(d => d.PresentmentCurrency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Purchases_PresentmentCurrency_fkey");

            entity.HasOne(d => d.Price).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.PriceId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Purchases_PriceId_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Purchases_ProductId_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Purchases_ProviderAccountId_fkey");

            entity.HasOne(d => d.ProviderTransaction).WithOne(p => p.Purchase)
                .HasForeignKey<PurchaseDataModel>(d => d.ProviderTransactionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Purchases_ProviderTransactionId_fkey");

            entity.HasOne(d => d.SettlementCurrencyNavigation).WithMany(p => p.PurchaseSettlementCurrencyNavigations)
                .HasForeignKey(d => d.SettlementCurrency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Purchases_SettlementCurrency_fkey");
        });

        modelBuilder.Entity<ReconciliationFindingDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ReconciliationFindings_pkey");

            entity.ToTable("ReconciliationFindings", "Payments");

            entity.HasIndex(e => e.AggregateId, "IX_ReconciliationFindings_AggregateId");

            entity.HasIndex(e => e.RunId, "IX_ReconciliationFindings_RunId");

            entity.HasIndex(e => new { e.Severity, e.Kind }, "IX_ReconciliationFindings_Severity_Kind").HasFilter("(\"ResolvedAt\" IS NULL)");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Detail).HasMaxLength(1024);
            entity.Property(e => e.Kind).HasMaxLength(64);
            entity.Property(e => e.Severity).HasMaxLength(16);

            entity.HasOne(d => d.Run).WithMany(p => p.ReconciliationFindings)
                .HasForeignKey(d => d.RunId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ReconciliationFindings_RunId_fkey");
        });

        modelBuilder.Entity<ReconciliationRunDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ReconciliationRuns_pkey");

            entity.ToTable("ReconciliationRuns", "Payments");

            entity.HasIndex(e => e.ProviderAccountId, "IX_ReconciliationRuns_ProviderAccountId");

            entity.HasIndex(e => new { e.ProviderAccountId, e.Kind, e.StartedAt }, "IX_ReconciliationRuns_ProviderAccountId_Kind_StartedAt").IsDescending(false, false, true);

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Counts).HasColumnType("jsonb");
            entity.Property(e => e.Kind).HasMaxLength(64);
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.TriggeredBy).HasMaxLength(64);

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.ReconciliationRuns)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ReconciliationRuns_ProviderAccountId_fkey");
        });

        modelBuilder.Entity<RefundDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Refunds_pkey");

            entity.ToTable("Refunds", "Payments");

            entity.HasIndex(e => e.Currency, "IX_Refunds_Currency");

            entity.HasIndex(e => e.PaymentReviewCaseId, "IX_Refunds_PaymentReviewCaseId");

            entity.HasIndex(e => e.ProviderAccountId, "IX_Refunds_ProviderAccountId");

            entity.HasIndex(e => e.ProviderTransactionId, "IX_Refunds_ProviderTransactionId");

            entity.HasIndex(e => e.PurchaseId, "IX_Refunds_PurchaseId");

            entity.HasIndex(e => new { e.ProviderAccountId, e.ExternalRefundId }, "UQ_Refunds_Provider_ExternalRefundId")
                .IsUnique()
                .HasFilter("(\"ExternalRefundId\" IS NOT NULL)");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.ExternalRefundId).HasMaxLength(255);
            entity.Property(e => e.ProviderFeeRefunded).HasPrecision(18, 4);
            entity.Property(e => e.Reason).HasMaxLength(512);
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(32)
                .HasDefaultValueSql("'Other'::character varying");
            entity.Property(e => e.Source).HasMaxLength(24);
            entity.Property(e => e.Status).HasMaxLength(32);

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Refunds_Currency_fkey");

            entity.HasOne(d => d.PaymentReviewCase).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.PaymentReviewCaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Refunds_PaymentReviewCaseId_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Refunds_ProviderAccountId_fkey");

            entity.HasOne(d => d.ProviderTransaction).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.ProviderTransactionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Refunds_ProviderTransactionId_fkey");

            entity.HasOne(d => d.Purchase).WithMany(p => p.Refunds)
                .HasForeignKey(d => d.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Refunds_PurchaseId_fkey");
        });

        modelBuilder.Entity<RefundRequestDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("RefundRequests_pkey");

            entity.ToTable("RefundRequests", "Payments");

            entity.HasIndex(e => e.Currency, "IX_RefundRequests_Currency");

            entity.HasIndex(e => e.PurchaseId, "IX_RefundRequests_PurchaseId");

            entity.HasIndex(e => new { e.TenantId, e.Status, e.CreatedAt }, "IX_RefundRequests_Tenant_Status_CreatedAt");

            entity.HasIndex(e => e.RefundId, "RefundRequests_RefundId_key").IsUnique();

            entity.HasIndex(e => e.PurchaseId, "UQ_RefundRequests_Purchase_Requested")
                .IsUnique()
                .HasFilter("((\"Status\")::text = 'Requested'::text)");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ApprovedAmount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.DecisionNotes).HasMaxLength(1024);
            entity.Property(e => e.Reason).HasMaxLength(512);
            entity.Property(e => e.Status).HasMaxLength(32);

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.RefundRequests)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("RefundRequests_Currency_fkey");

            entity.HasOne(d => d.Purchase).WithOne(p => p.RefundRequest)
                .HasForeignKey<RefundRequestDataModel>(d => d.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("RefundRequests_PurchaseId_fkey");

            entity.HasOne(d => d.Refund).WithOne(p => p.RefundRequest)
                .HasForeignKey<RefundRequestDataModel>(d => d.RefundId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("RefundRequests_RefundId_fkey");
        });

        modelBuilder.Entity<SettlementDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Settlements_pkey");

            entity.ToTable("Settlements", "Payments");

            entity.HasIndex(e => e.Currency, "IX_Settlements_Currency");

            entity.HasIndex(e => e.PayableAccountId, "IX_Settlements_PayableAccountId");

            entity.HasIndex(e => e.ProviderAccountId, "IX_Settlements_ProviderAccountId");

            entity.HasIndex(e => e.ProviderTransactionId, "IX_Settlements_ProviderTransactionId");

            entity.HasIndex(e => e.SourceAccountId, "IX_Settlements_SourceAccountId");

            entity.HasIndex(e => new { e.TenantId, e.ProviderAccountId, e.Currency, e.RecordedAt }, "IX_Settlements_Tenant_Provider_Currency_RecordedAt");

            entity.HasIndex(e => e.PaymentReviewCaseId, "Settlements_PaymentReviewCaseId_key").IsUnique();

            entity.HasIndex(e => new { e.SourceAccountId, e.ExternalReference }, "UQ_Settlements_Source_ExternalReference").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.DestinationSnapshot).HasColumnType("jsonb");
            entity.Property(e => e.EvidenceReference).HasMaxLength(512);
            entity.Property(e => e.ExternalReference).HasMaxLength(255);
            entity.Property(e => e.Notes).HasMaxLength(512);

            entity.HasOne(d => d.CurrencyNavigation).WithMany(p => p.Settlements)
                .HasForeignKey(d => d.Currency)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Settlements_Currency_fkey");

            entity.HasOne(d => d.PayableAccount).WithMany(p => p.SettlementPayableAccounts)
                .HasForeignKey(d => d.PayableAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Settlements_PayableAccountId_fkey");

            entity.HasOne(d => d.PaymentReviewCase).WithOne(p => p.Settlement)
                .HasForeignKey<SettlementDataModel>(d => d.PaymentReviewCaseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Settlements_PaymentReviewCaseId_fkey");

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.Settlements)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Settlements_ProviderAccountId_fkey");

            entity.HasOne(d => d.ProviderTransaction).WithMany(p => p.Settlements)
                .HasForeignKey(d => d.ProviderTransactionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Settlements_ProviderTransactionId_fkey");

            entity.HasOne(d => d.SourceAccount).WithMany(p => p.SettlementSourceAccounts)
                .HasForeignKey(d => d.SourceAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Settlements_SourceAccountId_fkey");
        });

        modelBuilder.Entity<WebhookEventDataModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("WebhookEvents_pkey");

            entity.ToTable("WebhookEvents", "Payments");

            entity.HasIndex(e => e.ExternalObjectId, "IX_WebhookEvents_ExternalObjectId").HasFilter("(\"ExternalObjectId\" IS NOT NULL)");

            entity.HasIndex(e => e.ProcessingOutcome, "IX_WebhookEvents_ProcessingOutcome_Null").HasFilter("(\"ProcessingOutcome\" IS NULL)");

            entity.HasIndex(e => e.ProviderAccountId, "IX_WebhookEvents_ProviderAccountId");

            entity.HasIndex(e => e.ProviderName, "IX_WebhookEvents_ProviderName");

            entity.HasIndex(e => new { e.ProviderName, e.EventType, e.ReceivedAt }, "IX_WebhookEvents_Provider_EventType_ReceivedAt");

            entity.HasIndex(e => new { e.ProviderName, e.ExternalEventId }, "UQ_WebhookEvents_Provider_ExternalEventId").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.DeliveryCount).HasDefaultValue(1);
            entity.Property(e => e.Environment).HasMaxLength(8);
            entity.Property(e => e.EventType).HasMaxLength(64);
            entity.Property(e => e.ExternalEventId).HasMaxLength(255);
            entity.Property(e => e.ExternalObjectId).HasMaxLength(255);
            entity.Property(e => e.ProcessingOutcome).HasMaxLength(32);
            entity.Property(e => e.ProviderName).HasMaxLength(32);

            entity.HasOne(d => d.ProviderAccount).WithMany(p => p.WebhookEvents)
                .HasForeignKey(d => d.ProviderAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("WebhookEvents_ProviderAccountId_fkey");

            entity.HasOne(d => d.ProviderNameNavigation).WithMany(p => p.WebhookEvents)
                .HasForeignKey(d => d.ProviderName)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("WebhookEvents_ProviderName_fkey");
        });

        modelBuilder.Entity<WebhookEventPayloadDataModel>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("WebhookEventPayloads_pkey");

            entity.ToTable("WebhookEventPayloads", "Payments");

            entity.HasIndex(e => new { e.ArchiveState, e.LastArchiveAttemptAt }, "IX_WebhookEventPayloads_ArchiveState_LastAttempt");

            entity.Property(e => e.EventId).ValueGeneratedNever();
            entity.Property(e => e.ArchiveObjectKey).HasMaxLength(512);
            entity.Property(e => e.ArchiveState)
                .HasMaxLength(16)
                .HasDefaultValueSql("'Hot'::character varying");
            entity.Property(e => e.ContentHash).HasMaxLength(64);
            entity.Property(e => e.Payload).HasColumnType("jsonb");

            entity.HasOne(d => d.Event).WithOne(p => p.WebhookEventPayload)
                .HasForeignKey<WebhookEventPayloadDataModel>(d => d.EventId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("WebhookEventPayloads_EventId_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

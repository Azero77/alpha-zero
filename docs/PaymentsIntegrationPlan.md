# 🏗️ Payments Integration & Pricing Tiers Plan

> **Implementation Status:** 📋 Architecturally Planned — Not Yet Implemented  
> **Decision:** Online payment implementation deferred until first tenants provide feedback (see [PricingStrategy.md](./PricingStrategy.md) §5 for phase gates)  
> **Business Model:** See [PricingStrategy.md](./PricingStrategy.md) for pricing tiers, revenue share, and monetization model

This document outlines the architectural plan for integrating online payments (**Stripe Checkout**), adding pricing tiers, coupons, and discounts in the `Courses` module, and unifying them with the offline physical `AccessCode` flow in the `Library` module.

---

## 1. Architectural Vision

We are building a multi-tenant, hybrid payment activation system. Student access is granted through one of two decoupled entry points that converge on a unified enrollment saga:
1. **Offline activation:** Purchasing scratch-off access cards locally and redeeming them (managed by `Library`).
2. **Online payment:** Buying course access via card payment (managed by `Payments` + `Stripe`).

Both flows map to the same outcome: granting the student access and assigning the necessary authorization roles.

### Decentralized Flow (Event-Driven)

```
[Path 1: Offline Access Code Redemption]
Student ──> [Library Module] ──> Hashed Code Verification
                                      │
                                      ▼
                      Execute CourseEnrollmentStrategy (ACL)
                                      │
                                      ▼ (Publishes CourseAccessUnlockedIntegrationEvent)
                            [MassTransit Mediator / Saga] 
                                      │
                                      ▼ (Sends EnrollStudentCommand)
                              [Courses Module] (Enrolls student)
                                      │
                                      ▼ (Sends AssignStudentRoleCommand)
                              [Identity Module] (Grants role)


[Path 2: Online Stripe Checkout Payment]
Student ──> [Courses Module] ──> Calculates price after coupons/discounts
                                      │
                                      ▼ (Sends CreateCheckoutSessionCommand)
                            [Payments Module] ──> Stripe API (Session Url)
                                                              │
                                                              ▼
                                                        Student pays on Stripe
                                                              │
                                                              ▼
                                                        Stripe Webhook (WebhookEndpoint)
                                                              │
                                                              ▼ (Publishes CourseAccessUnlockedIntegrationEvent)
                            [MassTransit Mediator / Saga]
                                      │
                                      ▼ (Sends EnrollStudentCommand)
                              [Courses Module] (Enrolls student)
                                      │
                                      ▼ (Sends AssignStudentRoleCommand)
                              [Identity Module] (Grants role)
```

---

## 2. Module Boundaries & Separation of Concerns

To preserve the **Modular Monolith** and **Clean Architecture** patterns, modules must not share database schemas or access other modules' databases. Communication is strictly asynchronous via MassTransit.

| Module | Core Responsibility | Key Aggregates |
|---|---|---|
| **Courses** | Catalog metadata, pricing tiers, discount calculation, coupons, and enrollment status. | `Course`, `CoursePlan`, `CoursePrice`, `Coupon` |
| **Payments** | Stripe checkout session generation, webhook validation, and transaction audit ledger. | `PaymentTransaction`, `IdempotentEvent` |
| **Library** | Offline voucher creation, distribution, status tracking, and validation. | `AccessCode`, `RedemptionAuditLog` |

---

## 3. Data Model Updates

### A. Courses Module
We will update `CoursePlan` and introduce `CoursePrice` and `Coupon` aggregates.

```csharp
// Modules/Courses/Domain/Aggregates/Courses/CoursePrice.cs
public class CoursePrice : Entity
{
    public Guid CoursePlanId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } // e.g. "USD", "EUR"
    public Guid TenantId { get; private set; }

    private CoursePrice() { }
    
    public static CoursePrice Create(Guid coursePlanId, decimal amount, string currency, Guid tenantId)
        => new() { Id = Guid.NewGuid(), CoursePlanId = coursePlanId, Amount = amount, Currency = currency, TenantId = tenantId };
}

// Modules/Courses/Domain/Aggregates/Coupons/Coupon.cs
public class Coupon : AggregateRoot, IDomainTenantOwned
{
    public string Code { get; private set; } // e.g., "DISCOUNT20"
    public decimal DiscountPercentage { get; private set; } // 0.00 to 1.00
    public DateTime ExpiryDate { get; private set; }
    public bool IsActive { get; private set; }
    public Guid TenantId { get; private set; }

    private Coupon() { }

    public static Coupon Create(string code, decimal discountPercentage, DateTime expiryDate, Guid tenantId)
        => new() { Id = Guid.NewGuid(), Code = code, DiscountPercentage = discountPercentage, ExpiryDate = expiryDate, IsActive = true, TenantId = tenantId };

    public void Deactivate() => IsActive = false;
}
```

### B. Payments Module (New!)
The new Payments module will track transactions and enforce Stripe webhook idempotency.

```csharp
// Modules/Payments/Domain/PaymentTransaction.cs
public class PaymentTransaction : AggregateRoot, IDomainTenantOwned
{
    public Guid UserId { get; private set; }
    public Guid CoursePlanId { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public decimal FinalAmount { get; private set; }
    public string? CouponCode { get; private set; }
    public string Currency { get; private set; }
    public string StripeSessionId { get; private set; }
    public string StripePaymentIntentId { get; private set; }
    public PaymentStatus Status { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PaymentTransaction() { }
}

// Modules/Payments/Domain/IdempotentEvent.cs
public class IdempotentEvent : Entity
{
    public string StripeEventId { get; private set; } // Tracks processed webhooks
    public DateTime ProcessedAt { get; private set; }

    private IdempotentEvent() { }
    
    public static IdempotentEvent Create(string stripeEventId)
        => new() { Id = Guid.NewGuid(), StripeEventId = stripeEventId, ProcessedAt = DateTime.UtcNow };
}
```

---

## 4. Integration Workflow (MassTransit Sagas)

### Unified Course Redemption Saga
Both payment webhooks and physical code redemptions will publish the `CourseAccessUnlockedIntegrationEvent`.

1. **Stripe Webhook Handler:**
   - Validates webhook signature.
   - Saves `IdempotentEvent` (prevents duplicate processing).
   - Updates `PaymentTransaction` to `Completed`.
   - Publishes `CourseAccessUnlockedIntegrationEvent` with `AccessCodeId = Guid.Empty`.

2. **Existing Saga Reuse:**
   - The `CourseRedemptionSaga` correlates the message by event ID.
   - Commands are dispatched to `Courses` to enroll the student (`EnrollStudentFromSagaCommand`).
   - Commands are dispatched to `Identity` to assign the role (`AssignStudentRoleFromSagaCommand`).
   - The flow is fully consistent and reused across both online/offline activation.

---

## 5. Learning Resources (Book Recommendations)

To gain a deeper understanding of designing scalable payment systems, billing pipelines, and clean architectures, we recommend the following:

1. **"Implementing Domain-Driven Design"** by *Vaughn Vernon*
   - Specifically Chapter 13 (Integrating Bounded Contexts) which illustrates how independent domains communicate cleanly via integration events and translation strategies.
2. **"Designing Data-Intensive Applications"** by *Martin Kleppmann*
   - Explains database transactions, idempotency logs, and distributed event systems, which are foundational to making sure you don't charge a user twice.
3. **"Domain-Driven Design: Tackling Complexity in the Heart of Software"** by *Eric Evans*
   - Introduces Bounded Contexts and Anti-Corruption Layers (ACL), clarifying why we isolate physical vouchers from virtual courses.
4. **Stripe Engineering Blog** (*stripe.com/blog*)
   - Essential reading for learning Stripe webhook best practices, handling network retries, and configuring checkout session lifecycles.

---

## 6. What Already Exists vs. NOT in Scope

### What already exists:
* **Library ACL:** The `CourseEnrollmentStrategy` already serves as an Anti-Corruption Layer (ACL) translating physical redemptions into generic `CourseAccessUnlockedIntegrationEvent`.
* **State Machine Sagas:** `CourseRedemptionSaga` already coordinates course enrollment and student role assignment.
* **Revocation Workflow:** `CourseRevocationSaga` is already in place to handle student access deactivations. Admins manage the lifetime of revocation requests, keeping refunds and manually triggered revocations aligned.

### NOT in scope (deferred):
* **Automated Refund Webhooks:** Stripe `charge.refunded` will not trigger automated DB revocation. Instead, refunds are logged, and support handles revocation via the existing admin-mediated `CourseRevocationSaga`.
* **Multi-Currency Dynamic Conversion:** Pricing currencies are hardcoded per tenant config. Dynamic forex APIs are deferred.
* **Physical Voucher Sales Inventory:** The accounting and tracking of library stock levels are handled out-of-band for the MVP.

---

## 7. Failure Modes & Edge Cases

| Failure Case | Mitigation Strategy |
|---|---|
| Stripe webhook timeout / retry | Webhook handler uses the `IdempotentEvents` table to record the Stripe Event ID and reject retried webhooks early in the transaction. |
| Duplicate Stripe Session creation | A student clicking "Buy" multiple times will generate different checkout sessions. The system logs these as pending transactions; only the paid session completes. |
| DB transaction rollbacks in Saga | If database writes fail in Courses or Identity, the saga transitions to `Failed` and logs the exception for support intervention. |

---

## 8. Worktree Parallelization Strategy

Work can be split across git worktrees to enable parallel developer lanes:

| Step | Modules touched | Depends on |
|---|---|---|
| **Step 1: Pricing Catalog** | Courses (Domain/Application) | — (Independent) |
| **Step 2: Payments Module & Stripe** | Payments (New Module) | — (Independent) |
| **Step 3: Webhook Integration** | Payments, Courses (Events) | Step 1, Step 2 |

```
Lane A (Worktree 1): Step 1 (Pricing, Tiers, Coupons in Courses module)
Lane B (Worktree 2): Step 2 (Payments module creation, Stripe checkout session initiation API)
Lane C (Integration): Step 3 (Webhook event mapping, connecting checkout success to Saga)
```

---

## 9. Implementation Tasks

- [ ] **T1 (P1, human: ~4h / CC: ~30min)** — `Courses` — Update domain models to support pricing and coupons
  - Surfaced by: Pricing Tiers design requirement
  - Files: `src/Modules/Courses/Domain/Aggregates/Courses/CoursePlan.cs`, `src/Modules/Courses/Domain/Aggregates/Courses/CoursePrice.cs`
  - Verify: Run Unit Tests for `CoursePlan` updating prices
- [ ] **T2 (P1, human: ~8h / CC: ~45min)** — `Payments` — Create the standalone Payments module with Stripe Checkout Session integration
  - Surfaced by: Stripe Checkout design requirement
  - Files: `src/Modules/Payments/` (new module directory)
  - Verify: Verify checkout endpoint returns session URL
- [ ] **T3 (P1, human: ~6h / CC: ~30min)** — `Payments` — Implement Stripe Webhook endpoint and Webhook Idempotency validation
  - Surfaced by: Webhook Idempotency decision D8
  - Files: `src/Modules/Payments/Presentation/Endpoints/WebhookEndpoint.cs`, `src/Modules/Payments/Infrastructure/Idempotency/IdempotentEventConfiguration.cs`
  - Verify: Send duplicate mock Stripe webhooks; assert second request returns `200 OK` without database duplication
- [ ] **T4 (P2, human: ~4h / CC: ~20min)** — `Courses` — Connect checkout webhook completion to existing Saga
  - Surfaced by: Unified Saga activation
  - Files: `src/Modules/Courses/Infrastructure/Sagas/CourseRedemption/CourseRedemptionSaga.cs`
  - Verify: Publish webhook success event; verify enrollment is created

---

## GSTACK REVIEW REPORT

### Checklist & Scope Validation
- **Step 0: Scope Challenge** — Scope accepted as-is with decentralized event-driven design.
- **Architecture Review** — 0 issues found (Clean integration using MassTransit Sagas and ACL).
- **Code Quality Review** — 0 issues found (Uses isolated module dependencies).
- **Test Review** — 4 integration/unit test gaps identified and added to task list.
- **Performance Review** — Idempotency log defined to prevent duplicate processing.
- **NOT in scope** — Written (Refund deactivations, dynamic forex conversion, inventory levels).
- **What already exists** — Written (Reuses redemption/revocation sagas).
- **TODOS.md updates** — 2 items proposed to and resolved by user (IdempotentEvents table, refund/revocation manual lookup).
- **Failure modes** — 3 critical failure cases addressed and mitigated.
- **Outside voice** — Ran (Claude subagent) and incorporated key feedback (SKU abstraction vs hardcoded Course GUIDs).
- **Parallelization** — 3 lanes, 2 parallel lanes, 1 sequential lane.
- **Lake Score** — 2/2 recommendations chose the complete option (idempotency table, reusing sagas).

# AlphaZero Messaging & Outbox Architecture

This document outlines the messaging infrastructure used in the AlphaZero Modular Monolith, detailing the dual-bus approach, the implementation of the Transactional Outbox pattern, and the future transition to a serverless architecture.

## 1. The Dual-Bus Hybrid Strategy

AlphaZero utilizes **MassTransit** to handle all decoupled event-driven communication. To balance performance and external integrations, we employ a hybrid strategy with two separate buses:

1. **The Primary Bus (In-Memory):** 
   - Used exclusively for **internal, cross-module communication**.
   - Extremely fast, with zero network latency.
   - Registered globally via `x.UsingInMemory(...)`.
   - Consumers in modules that do *not* contain "sqs" in their name are automatically mapped to this bus.

2. **The External Bus (Amazon SQS):**
   - Used for **external integrations** and heavy background processing (like AWS MediaConvert video encoding jobs).
   - Registered via a MultiBus interface: `AddMassTransit<IExternalBus>(...)`.
   - Consumers with "sqs" in their name are automatically mapped to this bus.

---

## 2. The Transactional Outbox Pattern

In a modular monolith, maintaining eventual consistency between isolated modules is critical. We use MassTransit's **Entity Framework Core Outbox** to guarantee that events are never lost.

### How it Works
When a module modifies its database (e.g., updating a user's course progress), it also publishes an event (e.g., `CourseCompletedEvent`). 
1. MassTransit intercepts this event and writes it to an `OutboxState` table in the **exact same database transaction** as the business data.
2. If the database commit fails, the event is rolled back. 
3. If the commit succeeds, the event is guaranteed to be published, even if the application immediately crashes.

### Configuration & Optimization
Each of the 7 modules independently configures its own outbox tied to its isolated `AppDbContext`:

```csharp
configuration.AddEntityFrameworkOutbox<AppDbContext>(o =>
{
    o.UsePostgres();
    o.UseBusOutbox();
    o.QueryDelay = TimeSpan.FromMinutes(5); // Optimized polling
});
```

### The Dual-Delivery Mechanism
To avoid hammering the database with constant polling, the outbox utilizes a dual-delivery mechanism:

1. **Instant Memory Trigger (Primary):** We implemented a custom, thread-safe singleton (`ThreadSafeBusOutboxNotification`). The exact millisecond EF Core successfully commits the transaction, it signals this singleton. A background thread instantly wakes up, reads the message, and delivers it. Latency is measured in milliseconds.
2. **Fallback Polling (Secondary):** MassTransit has a background `BusOutboxDeliveryService` that polls the `OutboxState` table just in case the memory trigger fails (e.g., the server crashed immediately after the DB commit). By default, this polls every 10 seconds. We have **optimized this to 5 minutes** (`QueryDelay`) to eliminate unnecessary database noise, as it is strictly a disaster-recovery fallback.

---

## 3. The Future: Transitioning to Serverless

The current architecture is highly optimized for a single-server deployment. However, the use of MassTransit and Clean Architecture ensures that the transition to a distributed, serverless environment will be seamless.

### What Changes?
When we break the monolith apart into serverless functions (AWS Lambda) or containers (AWS Fargate):

1. **Swapping the Transport:** 
   The In-Memory bus will be replaced with Amazon SQS/SNS (or EventBridge). This requires changing exactly one block of configuration in `Program.cs` from `x.UsingInMemory(...)` to `x.UsingAmazonSqs(...)`. 
   **Zero business logic will change.** Modules will still call `await _publishEndpoint.Publish(...)`.

2. **Outbox Delivery in Serverless:**
   Serverless environments (like AWS Lambda) do not support long-running background services like MassTransit's default `BusOutboxDeliveryService`. When transitioning, we will have two options:
   - **Dedicated Worker:** Run a single, tiny ECS/Fargate container whose sole job is to host the MassTransit background polling services for the outboxes.
   - **Change Data Capture (CDC):** Alternatively, we can transition from EF Core polling to a CDC-based outbox (like Debezium or DynamoDB Streams), which automatically pushes database row inserts directly into the messaging queue without polling.

### Conclusion
By strictly adhering to domain isolation and relying on MassTransit for decoupled communication, AlphaZero is perfectly positioned. It currently benefits from the low cost and simplicity of a single-server deployment, while maintaining the exact structural boundaries required for a massive serverless scale-out.

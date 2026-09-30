using System;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Middleware.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class BusOutboxNotification<TBus> : BusOutboxNotification where TBus : class, IBus {}

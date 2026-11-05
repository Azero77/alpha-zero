using System.Text.Json;
using AlphaZero.Modules.Library.Application.DomainEventHandlers;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Library.UnitTests.Application.DomainEventHandlers;

public class LibraryAuditLogProjectionHandlerTests
{
    private readonly ILibraryAuditLogRepository _repository;
    private readonly ILogger<LibraryAuditLogProjectionHandler> _logger;
    private readonly LibraryAuditLogProjectionHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid LibraryId = Guid.NewGuid();
    private static readonly Guid ActorUserId = Guid.NewGuid();
    private static readonly Guid CodeId = Guid.NewGuid();
    private static readonly ResourceArn Resource = ResourceArn.Create("az:course:tenant:course/101").Value;

    public LibraryAuditLogProjectionHandlerTests()
    {
        _repository = Substitute.For<ILibraryAuditLogRepository>();
        _logger = Substitute.For<ILogger<LibraryAuditLogProjectionHandler>>();
        _handler = new LibraryAuditLogProjectionHandler(_repository, _logger);
    }

    [Fact]
    public async Task Handle_MintedEvent_Should_ProjectToRepository()
    {
        var @event = new AccessCodeMintedDomainEvent(
            CodeId,
            TenantId,
            LibraryId,
            Guid.NewGuid(),
            ActorUserId,
            "course-enrollment",
            Resource,
            50m,
            DateTime.UtcNow);

        await _handler.Handle(@event, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<LibraryAuditLog>(log =>
                log.EventId == @event.Id &&
                log.TenantId == TenantId &&
                log.LibraryId == LibraryId &&
                log.ActorUserId == ActorUserId &&
                log.Action == "Minted" &&
                log.AggregateId == CodeId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_DistributedEvent_Should_ProjectToRepository()
    {
        var @event = new AccessCodeDistributedDomainEvent(
            CodeId,
            TenantId,
            LibraryId,
            Guid.NewGuid(),
            DateTime.UtcNow,
            ActorUserId);

        await _handler.Handle(@event, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<LibraryAuditLog>(log =>
                log.EventId == @event.Id &&
                log.TenantId == TenantId &&
                log.LibraryId == LibraryId &&
                log.ActorUserId == ActorUserId &&
                log.Action == "Distributed" &&
                log.AggregateId == CodeId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_SoldEvent_Should_ProjectToRepository()
    {
        var @event = new AccessCodeSoldDomainEvent(
            CodeId,
            TenantId,
            LibraryId,
            ActorUserId,
            75.50m,
            "USD",
            DateTime.UtcNow);

        await _handler.Handle(@event, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<LibraryAuditLog>(log =>
                log.EventId == @event.Id &&
                log.TenantId == TenantId &&
                log.LibraryId == LibraryId &&
                log.ActorUserId == ActorUserId &&
                log.Action == "Sold" &&
                log.AggregateId == CodeId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_RedeemedEvent_Should_ProjectToRepository_WithForensicRefAndFraudFlag()
    {
        var forensicLogId = Guid.NewGuid();
        var @event = new AccessCodeRedeemedDomainEvent(
            CodeId,
            TenantId,
            LibraryId,
            ActorUserId,
            Resource,
            DateTime.UtcNow,
            forensicLogId: forensicLogId,
            isUnsoldRedemption: true);

        await _handler.Handle(@event, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<LibraryAuditLog>(log =>
                log.EventId == @event.Id &&
                log.TenantId == TenantId &&
                log.LibraryId == LibraryId &&
                log.ActorUserId == ActorUserId &&
                log.Action == "Redeemed" &&
                log.AggregateId == CodeId &&
                log.Payload.RootElement.GetProperty("UnsoldRedemption").GetBoolean() == true &&
                log.Payload.RootElement.GetProperty("ForensicLogId").GetGuid() == forensicLogId),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_VoidedEvent_Should_ProjectToRepository_WithReason()
    {
        var @event = new AccessCodeVoidedDomainEvent(
            CodeId,
            TenantId,
            LibraryId,
            ActorUserId,
            null,
            Resource,
            "Damaged in library",
            DateTime.UtcNow);

        await _handler.Handle(@event, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<LibraryAuditLog>(log =>
                log.EventId == @event.Id &&
                log.TenantId == TenantId &&
                log.LibraryId == LibraryId &&
                log.ActorUserId == ActorUserId &&
                log.Action == "Voided" &&
                log.AggregateId == CodeId &&
                log.Payload.RootElement.GetProperty("Reason").GetString() == "Damaged in library"),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ExpiredEvent_Should_ProjectToRepository()
    {
        var @event = new AccessCodeExpiredDomainEvent(
            CodeId,
            TenantId,
            LibraryId,
            DateTime.UtcNow);

        await _handler.Handle(@event, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<LibraryAuditLog>(log =>
                log.EventId == @event.Id &&
                log.TenantId == TenantId &&
                log.LibraryId == LibraryId &&
                log.ActorUserId == null &&
                log.Action == "Expired" &&
                log.AggregateId == CodeId),
            CancellationToken.None);
    }
}

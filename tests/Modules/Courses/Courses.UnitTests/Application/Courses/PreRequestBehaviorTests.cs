using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class PreRequestBehaviorTests
{
    public record TestRequest : IRequest<ErrorOr<string>>;

    [Fact]
    public async Task Handle_WhenNoPreRequestHandlers_CallsNext()
    {
        // Arrange
        var factory = Substitute.For<IAuthorizationContextFactory>();
        var handlers = Enumerable.Empty<IPreRequestHandler<TestRequest>>();
        var behavior = new PreRequestBehavior<TestRequest, ErrorOr<string>>(handlers, factory);
        var request = new TestRequest();
        var next = Substitute.For<RequestHandlerDelegate<ErrorOr<string>>>();
        next.Invoke().Returns(Task.FromResult<ErrorOr<string>>("Success"));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be("Success");
        await next.Received(1).Invoke();
    }

    [Fact]
    public async Task Handle_WhenPreRequestHandlerReturnsSuccess_CallsNext()
    {
        // Arrange
        var factory = Substitute.For<IAuthorizationContextFactory>();
        var authContext = new AuthorizationContext
        {
            Id = Guid.NewGuid(),
            AuthenticationMethod = "Principal"
        };
        factory.CurrentAuthorizationContext.Returns(authContext);

        var preHandler = Substitute.For<IPreRequestHandler<TestRequest>>();
        preHandler.PreHandle(Arg.Any<TestRequest>(), authContext, Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        var behavior = new PreRequestBehavior<TestRequest, ErrorOr<string>>(new[] { preHandler }, factory);
        var request = new TestRequest();
        var next = Substitute.For<RequestHandlerDelegate<ErrorOr<string>>>();
        next.Invoke().Returns(Task.FromResult<ErrorOr<string>>("Success"));

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be("Success");
        await next.Received(1).Invoke();
    }

    [Fact]
    public async Task Handle_WhenPreRequestHandlerReturnsError_ShortCircuitsWithoutCallingNext()
    {
        // Arrange
        var factory = Substitute.For<IAuthorizationContextFactory>();
        var preHandler = Substitute.For<IPreRequestHandler<TestRequest>>();
        preHandler.PreHandle(Arg.Any<TestRequest>(), Arg.Any<AuthorizationContext?>(), Arg.Any<CancellationToken>())
            .Returns(Error.Forbidden("Access.Denied", "Forbidden"));

        var behavior = new PreRequestBehavior<TestRequest, ErrorOr<string>>(new[] { preHandler }, factory);
        var request = new TestRequest();
        var next = Substitute.For<RequestHandlerDelegate<ErrorOr<string>>>();

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Access.Denied");
        await next.DidNotReceive().Invoke();
    }
}

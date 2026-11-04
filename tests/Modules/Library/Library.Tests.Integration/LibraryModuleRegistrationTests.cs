using AlphaZero.Modules.Library.Presentation;
using Autofac;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Tests.Integration;

/// <summary>
/// Unit-level tests for the Library module registration path (no host, no database).
/// Per ticket #49: the module must refuse to start without its persistence
/// configuration instead of logging a warning and continuing.
/// </summary>
public class LibraryModuleRegistrationTests
{
    [Fact]
    public void RegisterGlobal_ShouldThrow_WhenConfigurationMissing()
    {
        // Arrange
        var module = new LibraryModule { Configuration = null };

        // Act
        var act = () => module.RegisterGlobal(new ServiceCollection());

        // Assert: the throw names the missing configuration section
        act.Should().Throw<InvalidOperationException>().WithMessage("*DatabaseSettings*");
    }

    [Fact]
    public void RegisterPrivate_ShouldThrow_WhenConfigurationMissing()
    {
        // Arrange
        var module = new LibraryModule { Configuration = null };

        // Act
        var act = () => module.RegisterPrivate(new ServiceCollection(), new ContainerBuilder());

        // Assert: the throw names the missing configuration section
        act.Should().Throw<InvalidOperationException>().WithMessage("*DatabaseSettings*");
    }
}

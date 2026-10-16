using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Security;
using AlphaZero.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure;

public class PlaybackTicketSecretValidationTests
{
    [Fact]
    public void Add_WhenPlaybackTicketSecretMissingAndEnvironmentIsProduction_ThrowsStartupError()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build(); // no Cloudflare section
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        var act = () => services.AddSharedInfrastructure(config, env);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PlaybackTicketSecret*");
    }

    [Fact]
    public void Add_WhenPlaybackTicketSecretMissingAndEnvironmentIsDevelopment_StillAcceptsDevSecret()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build(); // no Cloudflare section
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        var act = () => services.AddSharedInfrastructure(config, env);

        act.Should().NotThrow();

        // The dev constant must still work end-to-end: the signer mints a ticket it can verify.
        using var provider = new ServiceCollection()
            .AddSharedInfrastructure(config, env)
            .BuildServiceProvider();
        var signer = provider.GetRequiredService<IPlaybackTicketSigner>();
        var ticket = signer.GenerateTicket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        signer.TryVerifyTicket(ticket, out _, out var error).Should().BeTrue($"dev secret is still acceptable in Development (error: {error})");
    }

    [Fact]
    public void Add_WhenPlaybackTicketSecretConfiguredAndEnvironmentIsProduction_Composes()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{CloudflareSettings.SectionName}:{nameof(CloudflareSettings.PlaybackTicketSecret)}"] =
                    "configured-prod-secret-min-32-chars-long!!!"
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        var act = () => services.AddSharedInfrastructure(config, env);

        act.Should().NotThrow();
    }
}

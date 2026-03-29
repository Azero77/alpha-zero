using System.Reflection;
using AlphaZero.Shared.Application;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace AlphaZero.ArchitectureTests;

public class CommandConventionTests
{
    private static readonly Assembly[] Assemblies = 
    [
        typeof(AlphaZero.Modules.VideoUploading.Application.IVideoUploadingApplicationMarker).Assembly,
        // Add other module application markers here as they are created
    ];

    [Fact]
    public void Commands_Should_EndWithCommandKeyword()
    {
        foreach (var assembly in Assemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .ImplementInterface(typeof(ICommand<>))
                .Should()
                .HaveNameEndingWith("Command")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"All commands in {assembly.GetName().Name} must end with 'Command' keyword.");
        }
    }

    [Fact]
    public void TypesEndingWithCommand_Should_ImplementICommand()
    {
        foreach (var assembly in Assemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .HaveNameEndingWith("Command")
                .And()
                .AreNotInterfaces()
                .Should()
                .ImplementInterface(typeof(ICommand<>))
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"All types ending with 'Command' in {assembly.GetName().Name} must implement ICommand interface.");
        }
    }
}

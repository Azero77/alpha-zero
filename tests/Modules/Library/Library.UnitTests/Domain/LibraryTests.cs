using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using FluentAssertions;

namespace Library.UnitTests.Domain;

public class LibraryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_Should_SetPropertiesCorrectly()
    {
        // Act
        var library = AlphaZero.Modules.Library.Domain.Library.Create("Branch 1", "Address 1", "123", TenantId);

        // Assert
        library.Name.Should().Be("Branch 1");
        library.TenantId.Should().Be(TenantId);
        library.AllowedResources.Should().BeEmpty();
    }

    [Fact]
    public void AuthorizeResource_Should_AddPatternToAllowedResources()
    {
        // Arrange
        var library = AlphaZero.Modules.Library.Domain.Library.Create("B1", "A1", "123", TenantId);
        var arn = ResourceArn.Create("az:courses:tenant:course/math-101").Value;

        // Act
        var result = library.AuthorizeResource(arn);

        // Assert
        result.IsError.Should().BeFalse();
        library.AllowedResources.Should().ContainSingle(p => p.Value == arn.Value);
    }

    [Fact]
    public void AuthorizeResource_Should_ReturnConflict_WhenAlreadyAuthorized()
    {
        // Arrange
        var library = AlphaZero.Modules.Library.Domain.Library.Create("B1", "A1", "123", TenantId);
        var arn = ResourceArn.Create("az:courses:tenant:course/math-101").Value;
        library.AuthorizeResource(arn);

        // Act
        var result = library.AuthorizeResource(arn);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Library.ResourceAlreadyAuthorized");
    }
}
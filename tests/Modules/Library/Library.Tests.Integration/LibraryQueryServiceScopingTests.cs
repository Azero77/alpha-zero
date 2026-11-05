using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Library.Application.Libraries.Queries.GetLibrary;
using AlphaZero.Modules.Library.Application.Queries;
using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Queries;
using Autofac;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Library.Tests.Integration;

public class LibraryQueryServiceScopingTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    private (ILibraryQueryService QueryService, ILifetimeScope Scope) CreateQueryService(Guid tenantId)
    {
        var module = Resolve<AlphaZero.Modules.Library.Presentation.LibraryModule>();
        var scope = module.CreateScope();
        var provider = scope.Resolve<ITenantProvider>() as TestTenantProvider;
        if (provider != null)
        {
            provider.CurrentTenantId = tenantId;
        }
        return (scope.Resolve<ILibraryQueryService>(), scope);
    }

    [Fact]
    public async Task ListLibrariesAsync_Should_EnforceTenantScoping()
    {
        // Arrange: Create Library in Tenant A and Library in Tenant B
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var libA = AlphaZero.Modules.Library.Domain.Library.Create("Library Alpha", "Damascus Street 1", "0911111111", tenantA);
        var libB = AlphaZero.Modules.Library.Domain.Library.Create("Library Beta", "Aleppo Street 2", "0922222222", tenantB);

        DbContext.Libraries.AddRange(libA, libB);
        await DbContext.SaveChangesAsync();

        // Act 1: Query as Tenant A
        var (queryServiceA, scopeA) = CreateQueryService(tenantA);
        using (scopeA)
        {
            var resultA = await queryServiceA.ListLibrariesAsync(1, 10);

            // Assert 1: Only Tenant A's library returned
            resultA.Should().NotBeNull();
            resultA.TotalCount.Should().Be(1);
            resultA.Items.Should().ContainSingle(l => l.Id == libA.Id && l.Name == "Library Alpha");
            resultA.Items.Should().NotContain(l => l.Id == libB.Id);
        }

        // Act 2: Query as Tenant B
        var (queryServiceB, scopeB) = CreateQueryService(tenantB);
        using (scopeB)
        {
            var resultB = await queryServiceB.ListLibrariesAsync(1, 10);

            // Assert 2: Only Tenant B's library returned
            resultB.Should().NotBeNull();
            resultB.TotalCount.Should().Be(1);
            resultB.Items.Should().ContainSingle(l => l.Id == libB.Id && l.Name == "Library Beta");
            resultB.Items.Should().NotContain(l => l.Id == libA.Id);
        }
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Should_EnforceTenantScoping()
    {
        // Arrange: Create Library in Tenant A
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var libA = AlphaZero.Modules.Library.Domain.Library.Create("Central Library", "Homs Center", "0933333333", tenantA);
        DbContext.Libraries.Add(libA);
        await DbContext.SaveChangesAsync();

        // Act 1: Query as Tenant A (owner)
        var (queryServiceA, scopeA) = CreateQueryService(tenantA);
        using (scopeA)
        {
            var detailA = await queryServiceA.GetLibraryByIdAsync(libA.Id);

            // Assert 1: Detail successfully retrieved with matching properties
            detailA.Should().NotBeNull();
            detailA!.Id.Should().Be(libA.Id);
            detailA.Name.Should().Be("Central Library");
            detailA.Address.Should().Be("Homs Center");
            detailA.ContactNumber.Should().Be("0933333333");
        }

        // Act 2: Query as Tenant B (non-owner)
        var (queryServiceB, scopeB) = CreateQueryService(tenantB);
        using (scopeB)
        {
            var detailB = await queryServiceB.GetLibraryByIdAsync(libA.Id);

            // Assert 2: Null returned (scoped enforcement prevents cross-tenant leak)
            detailB.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetRedemptionLogsAsync_Should_EnforceTenantAndLibraryScoping()
    {
        // Arrange:
        // Tenant A has Library A1 and Library A2
        // Tenant B has Library B1
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var libA1 = AlphaZero.Modules.Library.Domain.Library.Create("Lib A1", "Addr 1", "0911", tenantA);
        var libA2 = AlphaZero.Modules.Library.Domain.Library.Create("Lib A2", "Addr 2", "0922", tenantA);
        var libB1 = AlphaZero.Modules.Library.Domain.Library.Create("Lib B1", "Addr 3", "0933", tenantB);

        DbContext.Libraries.AddRange(libA1, libA2, libB1);
        await DbContext.SaveChangesAsync();

        var codeId1 = Guid.NewGuid();
        var codeId2 = Guid.NewGuid();
        var codeId3 = Guid.NewGuid();

        var ctxA1 = new RedemptionContext(
            tenantA,
            libA1.Id,
            Guid.NewGuid(),
            "course-enrollment",
            ResourceArn.Create($"az:course:{tenantA}:course/math-1").Value,
            DateTime.UtcNow.AddMinutes(-10),
            "10.0.0.1",
            "device-fp-1");
        var logA1 = RedemptionAuditLog.Record(ctxA1, codeId1);

        var ctxA2 = new RedemptionContext(
            tenantA,
            libA2.Id,
            Guid.NewGuid(),
            "course-enrollment",
            ResourceArn.Create($"az:course:{tenantA}:course/math-2").Value,
            DateTime.UtcNow.AddMinutes(-5),
            "10.0.0.2",
            "device-fp-2");
        var logA2 = RedemptionAuditLog.Record(ctxA2, codeId2);

        var ctxB1 = new RedemptionContext(
            tenantB,
            libB1.Id,
            Guid.NewGuid(),
            "course-enrollment",
            ResourceArn.Create($"az:course:{tenantB}:course/science-1").Value,
            DateTime.UtcNow.AddMinutes(-1),
            "10.0.0.3",
            "device-fp-3");
        var logB1 = RedemptionAuditLog.Record(ctxB1, codeId3);

        DbContext.RedemptionAuditLogs.AddRange(logA1, logA2, logB1);
        await DbContext.SaveChangesAsync();

        // Act 1: Query as Tenant A for Library A1
        var (queryServiceA, scopeA) = CreateQueryService(tenantA);
        using (scopeA)
        {
            var logsA1 = await queryServiceA.GetRedemptionLogsAsync(libA1.Id, null, null, 1, 50);

            // Assert 1: Only Lib A1's logs are returned, NOT Lib A2's (library scoping enforced)
            logsA1.Should().NotBeNull();
            logsA1.TotalCount.Should().Be(1);
            logsA1.Items.Should().ContainSingle(x => x.Id == logA1.Id && x.LibraryId == libA1.Id);
            logsA1.Items.Should().NotContain(x => x.Id == logA2.Id);
            logsA1.Items.Should().NotContain(x => x.Id == logB1.Id);
        }

        // Act 2: Query as Tenant B for Library A1
        var (queryServiceB, scopeB) = CreateQueryService(tenantB);
        using (scopeB)
        {
            var logsCrossTenant = await queryServiceB.GetRedemptionLogsAsync(libA1.Id, null, null, 1, 50);

            // Assert 2: Zero logs returned (cross-tenant leak prevented)
            logsCrossTenant.Should().NotBeNull();
            logsCrossTenant.TotalCount.Should().Be(0);
            logsCrossTenant.Items.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ListLibraries_Endpoint_Should_ReturnPagedResult_And_EnforcePaging()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        for (int i = 1; i <= 5; i++)
        {
            var lib = AlphaZero.Modules.Library.Domain.Library.Create($"Paged Lib {i:D2}", $"Address {i}", $"090000000{i}", tenantId);
            DbContext.Libraries.Add(lib);
        }
        await DbContext.SaveChangesAsync();

        // Act: Fetch Page 1 with PerPage = 2
        var resPage1 = await Client.GetAsync("/library/libraries?page=1&perPage=2");
        resPage1.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged1 = await resPage1.Content.ReadFromJsonAsync<PagedResult<LibraryDto>>();

        paged1.Should().NotBeNull();
        paged1!.TotalCount.Should().Be(5);
        paged1.CurrentPage.Should().Be(1);
        paged1.PageSize.Should().Be(2);
        var items1 = paged1.Items.ToList();
        items1.Should().HaveCount(2);
        items1[0].Name.Should().Be("Paged Lib 01");
        items1[1].Name.Should().Be("Paged Lib 02");

        // Act: Fetch Page 2 with PerPage = 2
        var resPage2 = await Client.GetAsync("/library/libraries?page=2&perPage=2");
        resPage2.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged2 = await resPage2.Content.ReadFromJsonAsync<PagedResult<LibraryDto>>();

        paged2.Should().NotBeNull();
        var items2 = paged2!.Items.ToList();
        items2.Should().HaveCount(2);
        items2[0].Name.Should().Be("Paged Lib 03");
        items2[1].Name.Should().Be("Paged Lib 04");
    }

    [Fact]
    public async Task GetLibrary_Endpoint_Should_ReturnLibraryDto_WhenExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);

        var lib = AlphaZero.Modules.Library.Domain.Library.Create("Detail Lib", "Latakia Corniche", "0944444444", tenantId);
        lib.AuthorizeResource(ResourceArn.Create($"az:course:{tenantId}:course/physics-101").Value);
        DbContext.Libraries.Add(lib);
        await DbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/library/libraries/{lib.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<LibraryDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(lib.Id);
        dto.Name.Should().Be("Detail Lib");
        dto.Address.Should().Be("Latakia Corniche");
        dto.ContactNumber.Should().Be("0944444444");
        dto.AllowedResources.Should().ContainSingle(r => r.Contains("physics-101"));
    }
}

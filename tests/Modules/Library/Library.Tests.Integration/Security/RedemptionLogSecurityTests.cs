using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Shared.Queries;
using FluentAssertions;
using Xunit;

namespace Library.Tests.Integration.Security;

[Collection("LibrarySecurityIntegration")]
public class RedemptionLogSecurityTests
{
    private readonly SecurityApiFactory _factory;
    private readonly HttpClient _client;

    public RedemptionLogSecurityTests(SecurityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, string persona, Guid tenantId, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-TenantId", tenantId.ToString());
        request.Headers.Add("X-Persona", persona);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }

    [Fact]
    public async Task GetRedemptionLogs_WithoutLibraryId_Should_Return400BadRequest()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act 1: Calling route with empty Guid
        var reqEmptyGuid = CreateRequest(HttpMethod.Get, $"/library/libraries/{Guid.Empty}/audit-logs", "Manager", tenantId);
        var resEmptyGuid = await _client.SendAsync(reqEmptyGuid);

        // Act 2: Calling route without LibraryId parameter
        var reqNoParam = CreateRequest(HttpMethod.Get, "/library/audit-logs", "Manager", tenantId);
        var resNoParam = await _client.SendAsync(reqNoParam);

        // Assert: Missing/empty library id must return 400 Bad Request
        resEmptyGuid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resNoParam.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRedemptionLogs_ForAnotherLibrary_Should_Return403Forbidden()
    {
        // Arrange: Manager is scoped to Library A
        var tenantId = Guid.NewGuid();
        var libraryA = Guid.NewGuid();
        var libraryB = Guid.NewGuid();

        // Act: Manager attempts to query logs for Library B
        var request = CreateRequest(HttpMethod.Get, $"/library/libraries/{libraryB}/audit-logs", "Manager", tenantId);
        request.Headers.Add("X-Scoped-Library-Id", libraryA.ToString());
        var response = await _client.SendAsync(request);

        // Assert: Authorization must be library-scoped; manager cannot view another library's logs
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRedemptionLogs_ForOwnLibrary_Should_ReturnPagedLogs()
    {
        // Arrange: Manager is scoped to Library A
        var tenantId = Guid.NewGuid();
        var libraryA = Guid.NewGuid();

        // Act: Manager queries logs for their own library
        var request = CreateRequest(HttpMethod.Get, $"/library/libraries/{libraryA}/audit-logs", "Manager", tenantId);
        request.Headers.Add("X-Scoped-Library-Id", libraryA.ToString());
        var response = await _client.SendAsync(request);

        // Assert: Successfully returns paged logs
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<RedemptionAuditLogDto>>();
        pagedResult.Should().NotBeNull();
        pagedResult!.CurrentPage.Should().Be(1);
    }

    [Fact]
    public async Task GetRedemptionLogs_TenantWideFallback_IsUnreachable()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act: Attempt to access tenant-wide logs without library id
        var request = CreateRequest(HttpMethod.Get, "/library/audit-logs", "Accountant", tenantId);
        var response = await _client.SendAsync(request);

        // Assert: Tenant-wide fallback is deleted; endpoint rejects missing library id with 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

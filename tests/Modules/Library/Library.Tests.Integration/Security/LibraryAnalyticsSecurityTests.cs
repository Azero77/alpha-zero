using System.Net;
using FluentAssertions;
using Library.Tests.Integration.Abstractions;
using Library.Tests.Integration.Security;
using Xunit;

namespace Library.Tests.Integration.Security;

/// <summary>
/// Authorization matrix for the P4 analytics endpoints (issue #59): Accountant (library:Audit)
/// sees tenant-wide; Manager rides the library-scoped ARN for their own library; Worker 403
/// everywhere; Manager cross-library 403.
/// </summary>
[Collection("LibrarySecurityIntegration")]
public class LibraryAnalyticsSecurityTests(SecurityApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string persona, Guid tenantId, Guid? scopedLibrary = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-TenantId", tenantId.ToString());
        request.Headers.Add("X-Persona", persona);
        if (scopedLibrary.HasValue)
        {
            request.Headers.Add("X-Scoped-Library-Id", scopedLibrary.Value.ToString());
        }
        return request;
    }

    [Theory]
    [InlineData("/library/analytics/funnel")]
    [InlineData("/library/analytics/revenue")]
    [InlineData("/library/analytics/fraud-queue")]
    [InlineData("/library/analytics/provisioning-queue")]
    public async Task Analytics_Worker_Should_Return403Forbidden_OnAllQueues(string uri)
    {
        var response = await _client.SendAsync(CreateRequest(HttpMethod.Get, uri, "Worker", Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/library/analytics/funnel")]
    [InlineData("/library/analytics/revenue")]
    [InlineData("/library/analytics/fraud-queue")]
    [InlineData("/library/analytics/provisioning-queue")]
    public async Task Analytics_Accountant_Should_Return200TenantWide(string uri)
    {
        var response = await _client.SendAsync(CreateRequest(HttpMethod.Get, uri, "Accountant", Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/library/analytics/funnel")]
    [InlineData("/library/analytics/revenue")]
    [InlineData("/library/analytics/fraud-queue")]
    [InlineData("/library/analytics/provisioning-queue")]
    public async Task Analytics_Manager_OwnLibrary_Should_Return200(string uri)
    {
        var tenantId = Guid.NewGuid();
        var scopedLibrary = Guid.NewGuid();

        var response = await _client.SendAsync(
            CreateRequest(HttpMethod.Get, $"{uri}?libraryId={scopedLibrary}", "Manager", tenantId, scopedLibrary));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/library/analytics/funnel")]
    [InlineData("/library/analytics/revenue")]
    [InlineData("/library/analytics/fraud-queue")]
    [InlineData("/library/analytics/provisioning-queue")]
    public async Task Analytics_Manager_CrossLibrary_Should_Return403Forbidden(string uri)
    {
        var tenantId = Guid.NewGuid();
        var ownLibrary = Guid.NewGuid();
        var otherLibrary = Guid.NewGuid();

        var response = await _client.SendAsync(
            CreateRequest(HttpMethod.Get, $"{uri}?libraryId={otherLibrary}", "Manager", tenantId, ownLibrary));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

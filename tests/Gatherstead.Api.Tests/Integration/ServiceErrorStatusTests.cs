using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Tests.Integration;

/// <summary>
/// Covers the service-tier HTTP status mapping (<c>ServiceErrorActionResult</c>): an authorization
/// denial raised inside a service must surface as 403, not 400, while other service errors stay 400.
/// The attribute tier (<c>RequireTenantAccessAttribute</c>) is covered separately — these tests
/// deliberately give the caller a real membership so the request clears the attribute and the denial
/// can only come from the service.
/// </summary>
public class ServiceErrorStatusTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ServiceErrorStatusTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientFor(string externalId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.TokenHelper.GenerateToken(sub: externalId));
        return client;
    }

    // Creating a property requires Manager+ (AuthorizeTenantManageAsync). A Member is a legitimate
    // tenant user — so RequireTenantAccess admits the request — and is then refused by the service.
    // That refusal is the case that used to return 400 ("malformed request") for what is really
    // "authenticated, but not allowed".
    [Fact]
    public async Task CreateProperty_AsMember_Returns403_NotBadRequest()
    {
        var userId = Guid.NewGuid();
        var externalId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        _factory.SeedUser(userId, externalId);
        _factory.SeedTenant(tenantId, $"Tenant {tenantId}", userId);
        _factory.SeedTenantUser(tenantId, userId, TenantRole.Member);

        var response = await CreateClientFor(externalId).PostAsJsonAsync(
            $"/api/tenants/{tenantId}/properties",
            new { name = "Lakeside Cabin" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // The body must survive the status change: the frontend localizes by ErrorCode and never sniffs
    // HTTP statuses, so a bare ControllerBase.Forbid() (empty body, auth-handler challenge) would
    // leave it with nothing to render.
    [Fact]
    public async Task CreateProperty_AsMember_PreservesPermissionCodeInBody()
    {
        var userId = Guid.NewGuid();
        var externalId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        _factory.SeedUser(userId, externalId);
        _factory.SeedTenant(tenantId, $"Tenant {tenantId}", userId);
        _factory.SeedTenantUser(tenantId, userId, TenantRole.Member);

        var response = await CreateClientFor(externalId).PostAsJsonAsync(
            $"/api/tenants/{tenantId}/properties",
            new { name = "Lakeside Cabin" },
            TestContext.Current.CancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var messages = body.GetProperty("messages").EnumerateArray().ToList();

        Assert.NotEmpty(messages);
        Assert.Contains(messages, m =>
            m.GetProperty("type").GetString() == "ERROR"
            && m.GetProperty("code").GetString() == "PERMISSION_TENANT_MANAGE");
    }

    // Guards the other direction: the mapper must not blanket-403 every failure. An Owner is allowed
    // to create properties, so a second create with the same name fails on the tenant-unique-name rule
    // — a genuine 400.
    [Fact]
    public async Task CreateProperty_AsOwner_WithDuplicateName_StillReturns400()
    {
        var userId = Guid.NewGuid();
        var externalId = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        _factory.SeedUser(userId, externalId);
        _factory.SeedTenant(tenantId, $"Tenant {tenantId}", userId);
        _factory.SeedTenantUser(tenantId, userId, TenantRole.Owner);

        var client = CreateClientFor(externalId);
        var url = $"/api/tenants/{tenantId}/properties";
        var body = new { name = "Duplicate Ridge" };

        var first = await client.PostAsJsonAsync(url, body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(url, body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gatherstead.Api.Tests.Fixtures;

namespace Gatherstead.Api.Tests.Integration;

public class MeControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MeControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Provision the user through the real bootstrap flow (EF insert) rather than a raw-SQL seed, so
    // the row's GUID PK is stored exactly as EF expects and later UPDATEs match it. The token's
    // "name" claim seeds DisplayName.
    private async Task<HttpClient> ProvisionedClientAsync(string externalId, string? name = null)
    {
        var client = _factory.CreateClient();
        var token = _factory.TokenHelper.GenerateToken(sub: externalId, name: name);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var bootstrap = await client.PostAsync("/api/me/bootstrap", content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
        return client;
    }

    private sealed record MeDto(Guid UserId, string? Email, string? DisplayName);
    private sealed record MeResponse(MeDto? Entity, bool Successful);

    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsBootstrapSeededDisplayName()
    {
        var client = await ProvisionedClientAsync(Guid.NewGuid().ToString(), name: "Grace Hopper");

        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", TestContext.Current.CancellationToken);

        Assert.NotNull(me);
        Assert.True(me!.Successful);
        Assert.Equal("Grace Hopper", me.Entity!.DisplayName);
    }

    [Fact]
    public async Task Put_ThenGet_ReturnsUpdatedDisplayName()
    {
        var client = await ProvisionedClientAsync(Guid.NewGuid().ToString(), name: "Grace Hopper");

        var put = await client.PutAsJsonAsync("/api/me", new { displayName = "Ada Lovelace" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var get = await client.GetFromJsonAsync<MeResponse>("/api/me", TestContext.Current.CancellationToken);
        Assert.NotNull(get);
        Assert.True(get!.Successful);
        Assert.Equal("Ada Lovelace", get.Entity!.DisplayName);
    }

    [Fact]
    public async Task Put_BlankDisplayName_Returns400()
    {
        var client = await ProvisionedClientAsync(Guid.NewGuid().ToString());

        var put = await client.PutAsJsonAsync("/api/me", new { displayName = "   " }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Put_TooLongDisplayName_Returns400()
    {
        var client = await ProvisionedClientAsync(Guid.NewGuid().ToString());

        var put = await client.PutAsJsonAsync("/api/me", new { displayName = new string('x', 257) }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }
}

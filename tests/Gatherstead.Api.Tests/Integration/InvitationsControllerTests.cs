using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gatherstead.Api.Tests.Fixtures;

namespace Gatherstead.Api.Tests.Integration;

public class InvitationsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public InvitationsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Regression guard for the invitation 500. InvitationHouseholdGrant is a positional record used
    // as request input (the element type of CreateInvitationRequest.Households). When such a record
    // carries property-targeted validation ([property: Required]) instead of parameter-targeted
    // ([Required]), MVC throws ThrowIfRecordTypeHasValidationOnProperties while building model
    // metadata for the request body — a 500 before the action (or the service) ever runs, on every
    // invite. Service-level unit tests construct CreateInvitationRequest directly and bypass model
    // binding, so they cannot catch it; this drives the real HTTP pipeline instead.
    //
    // The caller is an App Admin so the pre-binding authorization filters (RequireTenantAccess) are
    // bypassed and the request reaches model binding. The body mirrors the payload that failed in
    // production (a populated households array plus a linkedMemberId); a populated array forces MVC
    // to materialize an InvitationHouseholdGrant, which is what triggered the throw.
    [Fact]
    public async Task Post_WithHouseholdGrants_BindsBodyAndReachesService_NotServerError()
    {
        var externalId = Guid.NewGuid().ToString();
        _factory.SeedUser(Guid.NewGuid(), externalId, isAppAdmin: true);

        var client = _factory.CreateClient();
        var token = _factory.TokenHelper.GenerateToken(sub: externalId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            email = "invitee@test.com",
            role = "Member",
            households = new[] { new { householdId = Guid.NewGuid(), role = "Manager" } },
            linkedMemberId = Guid.NewGuid(),
        };

        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{Guid.NewGuid()}/invitations", body, TestContext.Current.CancellationToken);

        // Pre-fix this was a 500 during model binding. Post-fix the body binds, the request reaches
        // the service, and the unknown household is rejected with a clean 400 (no DB write).
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using Gatherstead.Api.Security;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Gatherstead.Api.Tests.Security;

public class RequireTenantAccessAttributeTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId, currentUserId: _userId);

        // Seed parent entities required by foreign keys
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = $"Test Tenant {_tenantId}" });
        _dbContext.Users.Add(new User { Id = _userId, ExternalId = _userId.ToString() });
        await _dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private AuthorizationFilterContext CreateContext(
        Guid? routeTenantId,
        Guid? userId,
        string? includeDeleted = null)
    {
        var httpContext = new DefaultHttpContext();

        if (routeTenantId.HasValue)
            httpContext.Request.RouteValues["tenantId"] = routeTenantId.Value.ToString();

        if (includeDeleted != null)
            httpContext.Request.QueryString = new QueryString($"?includeDeleted={includeDeleted}");

        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == userId);
        var appAdminContext = Mock.Of<IAppAdminContext>(c => c.IsAppAdminAsync(It.IsAny<CancellationToken>()) == Task.FromResult<bool?>(false));

        var services = new ServiceCollection();
        services.AddSingleton(userContext);
        services.AddSingleton<IAppAdminContext>(appAdminContext);
        services.AddSingleton(_dbContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private async Task SeedTenantUser(TenantRole role)
    {
        _dbContext.TenantUsers.Add(new TenantUser
        {
            TenantId = _tenantId,
            UserId = _userId,
            Role = role
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task NoTenantIdInRoute_Passes()
    {
        var attribute = new RequireTenantAccessAttribute();
        var context = CreateContext(routeTenantId: null, userId: _userId);

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task InvalidTenantIdFormat_ReturnsBadRequest()
    {
        var attribute = new RequireTenantAccessAttribute();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["tenantId"] = "not-a-guid";

        var userContext = Mock.Of<ICurrentUserContext>(c => c.UserId == _userId);
        var appAdminContext = Mock.Of<IAppAdminContext>(c => c.IsAppAdminAsync(It.IsAny<CancellationToken>()) == Task.FromResult<bool?>(false));
        var services = new ServiceCollection();
        services.AddSingleton(userContext);
        services.AddSingleton<IAppAdminContext>(appAdminContext);
        services.AddSingleton(_dbContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    [Fact]
    public async Task UnauthenticatedUser_ReturnsUnauthorized()
    {
        var attribute = new RequireTenantAccessAttribute();
        var context = CreateContext(routeTenantId: _tenantId, userId: null);

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task NonMember_ReturnsForbid()
    {
        var attribute = new RequireTenantAccessAttribute();
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId);

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Theory]
    [InlineData(TenantRole.Owner)]
    [InlineData(TenantRole.Manager)]
    [InlineData(TenantRole.Member)]
    [InlineData(TenantRole.Guest)]
    public async Task MemberWithNoMinimumRole_Passes(TenantRole role)
    {
        await SeedTenantUser(role);
        var attribute = new RequireTenantAccessAttribute();
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId);

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OwnerAccessesOwnerRequired_Passes()
    {
        await SeedTenantUser(TenantRole.Owner);
        var attribute = new RequireTenantAccessAttribute(TenantRole.Owner);
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId);

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task ManagerAccessesOwnerRequired_ReturnsForbid()
    {
        await SeedTenantUser(TenantRole.Manager);
        var attribute = new RequireTenantAccessAttribute(TenantRole.Owner);
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId);

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task MemberAccessesMemberRequired_Passes()
    {
        await SeedTenantUser(TenantRole.Member);
        var attribute = new RequireTenantAccessAttribute(TenantRole.Member);
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId);

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task GuestAccessesMemberRequired_ReturnsForbid()
    {
        await SeedTenantUser(TenantRole.Guest);
        var attribute = new RequireTenantAccessAttribute(TenantRole.Member);
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId);

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task OwnerAccessesMemberRequired_Passes()
    {
        await SeedTenantUser(TenantRole.Owner);
        var attribute = new RequireTenantAccessAttribute(TenantRole.Member);
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId);

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task IncludeDeletedTrue_ManagerRole_SetsHttpContextItem()
    {
        await SeedTenantUser(TenantRole.Manager);
        var attribute = new RequireTenantAccessAttribute();
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId, includeDeleted: "true");

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.True(context.HttpContext.Items["IncludeDeletedAuthorized"] is true);
    }

    [Fact]
    public async Task IncludeDeletedTrue_MemberRole_DoesNotSetHttpContextItem()
    {
        await SeedTenantUser(TenantRole.Member);
        var attribute = new RequireTenantAccessAttribute();
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId, includeDeleted: "true");

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.False(context.HttpContext.Items.ContainsKey("IncludeDeletedAuthorized"));
    }

    [Fact]
    public async Task IncludeDeletedFalse_Manager_DoesNotSetHttpContextItem()
    {
        await SeedTenantUser(TenantRole.Manager);
        var attribute = new RequireTenantAccessAttribute();
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId, includeDeleted: "false");

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.False(context.HttpContext.Items.ContainsKey("IncludeDeletedAuthorized"));
    }

    [Fact]
    public async Task IncludeDeletedTrue_OwnerRole_SetsHttpContextItem()
    {
        await SeedTenantUser(TenantRole.Owner);
        var attribute = new RequireTenantAccessAttribute();
        var context = CreateContext(routeTenantId: _tenantId, userId: _userId, includeDeleted: "true");

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.True(context.HttpContext.Items["IncludeDeletedAuthorized"] is true);
    }
}

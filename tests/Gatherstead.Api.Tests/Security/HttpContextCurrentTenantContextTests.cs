using Gatherstead.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace Gatherstead.Api.Tests.Security;

public class HttpContextCurrentTenantContextTests
{
    [Fact]
    public void TenantId_NullHttpContext_ReturnsNull()
    {
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == null);
        var context = new HttpContextCurrentTenantContext(accessor);

        Assert.Null(context.TenantId);
    }

    [Fact]
    public void TenantId_NoRouteValue_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentTenantContext(accessor);

        Assert.Null(context.TenantId);
    }

    [Fact]
    public void TenantId_ValidGuidRouteValue_ReturnsGuid()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = new RouteValueDictionary { ["tenantId"] = tenantId.ToString() };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentTenantContext(accessor);

        Assert.Equal(tenantId, context.TenantId);
    }

    [Fact]
    public void TenantId_InvalidGuidRouteValue_ThrowsInvalidOperationException()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = new RouteValueDictionary { ["tenantId"] = "not-a-guid" };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentTenantContext(accessor);

        Assert.Throws<InvalidOperationException>(() => context.TenantId);
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpContextCurrentTenantContext(null!));
    }
}

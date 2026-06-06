using Gatherstead.Api.Security;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Gatherstead.Api.Tests.Security;

public class HttpContextAuditVisibilityContextTests
{
    [Fact]
    public void IncludeAudit_NullHttpContext_ReturnsFalse()
    {
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == null);
        var context = new HttpContextAuditVisibilityContext(accessor);

        Assert.False(context.IncludeAudit);
    }

    [Fact]
    public void IncludeAudit_ItemNotSet_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextAuditVisibilityContext(accessor);

        Assert.False(context.IncludeAudit);
    }

    [Fact]
    public void IncludeAudit_ItemSetToTrue_ReturnsTrue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["IncludeAuditAuthorized"] = true;
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextAuditVisibilityContext(accessor);

        Assert.True(context.IncludeAudit);
    }

    [Fact]
    public void IncludeAudit_ItemSetToFalse_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["IncludeAuditAuthorized"] = false;
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextAuditVisibilityContext(accessor);

        Assert.False(context.IncludeAudit);
    }

    [Fact]
    public void IncludeAudit_ItemSetToString_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["IncludeAuditAuthorized"] = "true";
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextAuditVisibilityContext(accessor);

        Assert.False(context.IncludeAudit);
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpContextAuditVisibilityContext(null!));
    }
}

using Gatherstead.Api.Security;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Gatherstead.Api.Tests.Security;

public class HttpContextIncludeDeletedContextTests
{
    [Fact]
    public void IncludeDeleted_NullHttpContext_ReturnsFalse()
    {
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == null);
        var context = new HttpContextIncludeDeletedContext(accessor);

        Assert.False(context.IncludeDeleted);
    }

    [Fact]
    public void IncludeDeleted_ItemNotSet_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextIncludeDeletedContext(accessor);

        Assert.False(context.IncludeDeleted);
    }

    [Fact]
    public void IncludeDeleted_ItemSetToTrue_ReturnsTrue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["IncludeDeletedAuthorized"] = true;
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextIncludeDeletedContext(accessor);

        Assert.True(context.IncludeDeleted);
    }

    [Fact]
    public void IncludeDeleted_ItemSetToFalse_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["IncludeDeletedAuthorized"] = false;
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextIncludeDeletedContext(accessor);

        Assert.False(context.IncludeDeleted);
    }

    [Fact]
    public void IncludeDeleted_ItemSetToString_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["IncludeDeletedAuthorized"] = "true";
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextIncludeDeletedContext(accessor);

        Assert.False(context.IncludeDeleted);
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpContextIncludeDeletedContext(null!));
    }
}

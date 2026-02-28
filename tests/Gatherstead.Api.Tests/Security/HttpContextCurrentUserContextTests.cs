using System.Security.Claims;
using Gatherstead.Api.Security;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Gatherstead.Api.Tests.Security;

public class HttpContextCurrentUserContextTests
{
    [Fact]
    public void UserId_NullHttpContext_ReturnsNull()
    {
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == null);
        var context = new HttpContextCurrentUserContext(accessor);

        Assert.Null(context.UserId);
    }

    [Fact]
    public void UserId_UnauthenticatedUser_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor);

        Assert.Null(context.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithNameIdentifier_ReturnsGuid()
    {
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor);

        Assert.Equal(userId, context.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithSubClaim_ReturnsGuid()
    {
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim("sub", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor);

        Assert.Equal(userId, context.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithNoIdentifierClaim_ThrowsInvalidOperationException()
    {
        var claims = new[] { new Claim(ClaimTypes.Email, "test@example.com") };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor);

        Assert.Throws<InvalidOperationException>(() => context.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithNonGuidClaim_ThrowsInvalidOperationException()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor);

        Assert.Throws<InvalidOperationException>(() => context.UserId);
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpContextCurrentUserContext(null!));
    }
}

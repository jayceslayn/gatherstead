using System.Security.Claims;
using Gatherstead.Api.Security;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Gatherstead.Api.Tests.Security;

public class HttpContextCurrentUserContextTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private IServiceProvider _serviceProvider = null!;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _externalId = Guid.NewGuid().ToString();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(currentUserId: _userId);

        _dbContext.Users.Add(new User
        {
            Id = _userId,
            ExternalId = _externalId,
        });
        await _dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        _serviceProvider = services.BuildServiceProvider();
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void UserId_NullHttpContext_ReturnsNull()
    {
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == null);
        var context = new HttpContextCurrentUserContext(accessor, _serviceProvider);

        Assert.Null(context.UserId);
    }

    [Fact]
    public void UserId_UnauthenticatedUser_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor, _serviceProvider);

        Assert.Null(context.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithNameIdentifier_ReturnsInternalUserId()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, _externalId) };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor, _serviceProvider);

        Assert.Equal(_userId, context.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithSubClaim_ReturnsInternalUserId()
    {
        var claims = new[] { new Claim("sub", _externalId) };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor, _serviceProvider);

        Assert.Equal(_userId, context.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithNoIdentifierClaim_ThrowsInvalidOperationException()
    {
        var claims = new[] { new Claim(ClaimTypes.Email, "test@example.com") };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor, _serviceProvider);

        Assert.Throws<InvalidOperationException>(() => context.UserId);
    }

    [Fact]
    public void UserId_AuthenticatedWithUnknownExternalId_ReturnsNull()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "unknown-external-id") };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor, _serviceProvider);

        Assert.Null(context.UserId);
    }

    [Fact]
    public void UserId_CachesResultPerRequest()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, _externalId) };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var context = new HttpContextCurrentUserContext(accessor, _serviceProvider);

        var first = context.UserId;
        var second = context.UserId;

        Assert.Equal(first, second);
        Assert.Equal(_userId, first);
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpContextCurrentUserContext(null!, _serviceProvider));
    }

    [Fact]
    public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
    {
        var accessor = Mock.Of<IHttpContextAccessor>();
        Assert.Throws<ArgumentNullException>(() => new HttpContextCurrentUserContext(accessor, null!));
    }
}

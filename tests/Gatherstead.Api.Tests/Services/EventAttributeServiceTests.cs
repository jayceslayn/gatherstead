using Gatherstead.Api.Contracts.EventAttributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.EventAttributes;
using Gatherstead.Api.Tests.Services.Attributes;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class EventAttributeServiceTests
    : ParentScopedAttributeServiceTestBase<
        EventAttribute, EventAttributeDto,
        CreateEventAttributeRequest, UpdateEventAttributeRequest,
        IEventAttributeService>
{
    private readonly Guid _propertyId = Guid.NewGuid();

    protected override void SeedParent(GathersteadDbContext db)
    {
        db.Properties.Add(new Property { Id = _propertyId, TenantId = TenantId, Name = "Test Property" });
        db.Events.Add(new Event
        {
            Id = ParentId,
            TenantId = TenantId,
            PropertyId = _propertyId,
            Name = "Test Event",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        });
    }

    protected override EventAttribute NewAttribute(string key, string value, byte tenantMinRole) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        EventId = ParentId,
        Key = key,
        Value = value,
        TenantMinRole = tenantMinRole,
    };

    protected override CreateEventAttributeRequest NewCreateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override UpdateEventAttributeRequest NewUpdateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override IEventAttributeService MakeService(TenantRole? callerTenantRole, bool canManage)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        return BuildService(tenantContext, callerTenantRole, canManage);
    }

    protected override IEventAttributeService MakeServiceWithContext(ICurrentTenantContext tenantContext, bool canManage)
        => BuildService(tenantContext, TenantRole.Manager, canManage);

    private IEventAttributeService BuildService(ICurrentTenantContext tenantContext, TenantRole? callerRole, bool canManage)
    {
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(callerRole);
        auth.Setup(a => a.CanManageEventAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(canManage);
        return new EventAttributeService(DbContext, tenantContext, auth.Object);
    }
}

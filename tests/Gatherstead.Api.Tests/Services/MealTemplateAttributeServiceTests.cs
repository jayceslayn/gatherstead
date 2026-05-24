using Gatherstead.Api.Contracts.MealTemplateAttributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.MealTemplateAttributes;
using Gatherstead.Api.Tests.Services.Attributes;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class MealTemplateAttributeServiceTests
    : ParentScopedAttributeServiceTestBase<
        MealTemplateAttribute, MealTemplateAttributeDto,
        CreateMealTemplateAttributeRequest, UpdateMealTemplateAttributeRequest,
        IMealTemplateAttributeService>
{
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();

    protected override void SeedParent(GathersteadDbContext db)
    {
        db.Properties.Add(new Property { Id = _propertyId, TenantId = TenantId, Name = "Test Property" });
        db.Events.Add(new Event
        {
            Id = _eventId,
            TenantId = TenantId,
            PropertyId = _propertyId,
            Name = "Test Event",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        });
        db.MealTemplates.Add(new MealTemplate { Id = ParentId, TenantId = TenantId, EventId = _eventId, Name = "Test Meal Template" });
    }

    protected override MealTemplateAttribute NewAttribute(string key, string value, byte tenantMinRole) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        MealTemplateId = ParentId,
        Key = key,
        Value = value,
        TenantMinRole = tenantMinRole,
    };

    protected override CreateMealTemplateAttributeRequest NewCreateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override UpdateMealTemplateAttributeRequest NewUpdateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override IMealTemplateAttributeService MakeService(TenantRole? callerTenantRole, bool canManage)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        return BuildService(tenantContext, callerTenantRole, canManage);
    }

    protected override IMealTemplateAttributeService MakeServiceWithContext(ICurrentTenantContext tenantContext, bool canManage)
        => BuildService(tenantContext, TenantRole.Manager, canManage);

    private IMealTemplateAttributeService BuildService(ICurrentTenantContext tenantContext, TenantRole? callerRole, bool canManage)
    {
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(callerRole);
        auth.Setup(a => a.CanManageEventAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(canManage);
        return new MealTemplateAttributeService(DbContext, tenantContext, auth.Object);
    }
}

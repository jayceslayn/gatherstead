using Gatherstead.Api.Contracts.TaskTemplateAttributes;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.TaskTemplateAttributes;
using Gatherstead.Api.Tests.Services.Attributes;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class TaskTemplateAttributeServiceTests
    : ParentScopedAttributeServiceTestBase<
        TaskTemplateAttribute, TaskTemplateAttributeDto,
        CreateTaskTemplateAttributeRequest, UpdateTaskTemplateAttributeRequest,
        ITaskTemplateAttributeService>
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
        db.TaskTemplates.Add(new TaskTemplate { Id = ParentId, TenantId = TenantId, EventId = _eventId, Name = "Test Task Template" });
    }

    protected override TaskTemplateAttribute NewAttribute(string key, string value, byte tenantMinRole) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        TaskTemplateId = ParentId,
        Key = key,
        Value = value,
        TenantMinRole = tenantMinRole,
    };

    protected override CreateTaskTemplateAttributeRequest NewCreateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override UpdateTaskTemplateAttributeRequest NewUpdateRequest(string key, string value, byte tenantMinRole)
        => new() { Key = key, Value = value, TenantMinRole = tenantMinRole };

    protected override ITaskTemplateAttributeService MakeService(TenantRole? callerTenantRole, bool canManage)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == TenantId);
        return BuildService(tenantContext, callerTenantRole, canManage);
    }

    protected override ITaskTemplateAttributeService MakeServiceWithContext(ICurrentTenantContext tenantContext, bool canManage)
        => BuildService(tenantContext, TenantRole.Manager, canManage);

    private ITaskTemplateAttributeService BuildService(ICurrentTenantContext tenantContext, TenantRole? callerRole, bool canManage)
    {
        var auth = new Mock<IMemberAuthorizationService>();
        auth.Setup(a => a.GetCallerTenantRoleAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(callerRole);
        auth.Setup(a => a.CanManageEventAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(canManage);
        return new TaskTemplateAttributeService(DbContext, tenantContext, auth.Object);
    }
}

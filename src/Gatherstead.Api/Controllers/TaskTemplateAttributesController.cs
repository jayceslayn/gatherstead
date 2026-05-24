using Gatherstead.Api.Contracts.TaskTemplateAttributes;
using Gatherstead.Api.Controllers.Attributes;
using Gatherstead.Api.Services.TaskTemplateAttributes;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[Route("api/tenants/{tenantId:guid}/task-templates/{parentId:guid}/attributes")]
public class TaskTemplateAttributesController(ITaskTemplateAttributeService service)
    : ParentScopedAttributeControllerBase<ITaskTemplateAttributeService, TaskTemplateAttributeDto, CreateTaskTemplateAttributeRequest, UpdateTaskTemplateAttributeRequest>(service);

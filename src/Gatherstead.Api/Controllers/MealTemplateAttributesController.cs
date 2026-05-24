using Gatherstead.Api.Contracts.MealTemplateAttributes;
using Gatherstead.Api.Controllers.Attributes;
using Gatherstead.Api.Services.MealTemplateAttributes;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[Route("api/tenants/{tenantId:guid}/meal-templates/{parentId:guid}/attributes")]
public class MealTemplateAttributesController(IMealTemplateAttributeService service)
    : ParentScopedAttributeControllerBase<IMealTemplateAttributeService, MealTemplateAttributeDto, CreateMealTemplateAttributeRequest, UpdateMealTemplateAttributeRequest>(service);

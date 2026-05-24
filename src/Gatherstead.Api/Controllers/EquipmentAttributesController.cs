using Gatherstead.Api.Contracts.EquipmentAttributes;
using Gatherstead.Api.Controllers.Attributes;
using Gatherstead.Api.Services.EquipmentAttributes;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[Route("api/tenants/{tenantId:guid}/equipment/{parentId:guid}/attributes")]
public class EquipmentAttributesController(IEquipmentAttributeService service)
    : ParentScopedAttributeControllerBase<IEquipmentAttributeService, EquipmentAttributeDto, CreateEquipmentAttributeRequest, UpdateEquipmentAttributeRequest>(service);

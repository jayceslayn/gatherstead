using Gatherstead.Api.Contracts.HouseholdAttributes;
using Gatherstead.Api.Controllers.Attributes;
using Gatherstead.Api.Services.HouseholdAttributes;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[Route("api/tenants/{tenantId:guid}/households/{parentId:guid}/attributes")]
public class HouseholdAttributesController(IHouseholdAttributeService service)
    : ParentScopedAttributeControllerBase<IHouseholdAttributeService, HouseholdAttributeDto, CreateHouseholdAttributeRequest, UpdateHouseholdAttributeRequest>(service);

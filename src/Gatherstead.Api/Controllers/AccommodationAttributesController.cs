using Gatherstead.Api.Contracts.AccommodationAttributes;
using Gatherstead.Api.Controllers.Attributes;
using Gatherstead.Api.Services.AccommodationAttributes;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[Route("api/tenants/{tenantId:guid}/accommodations/{parentId:guid}/attributes")]
public class AccommodationAttributesController(IAccommodationAttributeService service)
    : ParentScopedAttributeControllerBase<IAccommodationAttributeService, AccommodationAttributeDto, CreateAccommodationAttributeRequest, UpdateAccommodationAttributeRequest>(service);

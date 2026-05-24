using Gatherstead.Api.Contracts.PropertyAttributes;
using Gatherstead.Api.Controllers.Attributes;
using Gatherstead.Api.Services.PropertyAttributes;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[Route("api/tenants/{tenantId:guid}/properties/{parentId:guid}/attributes")]
public class PropertyAttributesController(IPropertyAttributeService service)
    : ParentScopedAttributeControllerBase<IPropertyAttributeService, PropertyAttributeDto, CreatePropertyAttributeRequest, UpdatePropertyAttributeRequest>(service);

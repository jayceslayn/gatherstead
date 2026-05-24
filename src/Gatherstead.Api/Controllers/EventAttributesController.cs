using Gatherstead.Api.Contracts.EventAttributes;
using Gatherstead.Api.Controllers.Attributes;
using Gatherstead.Api.Services.EventAttributes;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

[Route("api/tenants/{tenantId:guid}/events/{parentId:guid}/attributes")]
public class EventAttributesController(IEventAttributeService service)
    : ParentScopedAttributeControllerBase<IEventAttributeService, EventAttributeDto, CreateEventAttributeRequest, UpdateEventAttributeRequest>(service);

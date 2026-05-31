using Gatherstead.Api.Contracts.Reports;
using Gatherstead.Api.Security;
using Gatherstead.Api.Services.Reports;
using Gatherstead.Api.Services.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>
/// Root for tenant-scoped reports. The event meal/attendance report is the first report type;
/// future report types live under this same controller/route so they share tenant authorization.
/// </summary>
[ApiController]
[Authorize]
[RequireTenantAccess]
[Route("api/tenants/{tenantId:guid}/reports")]
public class ReportsController : ControllerBase
{
    private readonly IEventReportService _eventReportService;

    public ReportsController(IEventReportService eventReportService)
    {
        _eventReportService = eventReportService ?? throw new ArgumentNullException(nameof(eventReportService));
    }

    [HttpGet("events/{eventId:guid}")]
    public async Task<ActionResult<EventReportResponse>> GetEventReport(
        Guid tenantId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var response = await _eventReportService.GetEventMealReportAsync(tenantId, eventId, cancellationToken);

        if (ServiceValidationHelper.HasErrors(response))
            return BadRequest(response);

        if (response.Entity is null)
            return NotFound(response);

        return Ok(response);
    }
}

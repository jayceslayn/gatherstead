using System.Collections.Frozen;
using Gatherstead.Api.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>
/// Maps a failed <see cref="BaseEntityResponse{T}"/> to its HTTP status: 403 when the failure is an
/// authorization denial (any <c>PERMISSION_*</c> <see cref="ErrorCode"/>), otherwise 400. This is the
/// service-layer counterpart to the attribute tier (<c>RequireTenantAccessAttribute</c>,
/// <c>RequireAppAdminAttribute</c>), which already separates 401 from 403 before a request reaches a
/// service.
/// </summary>
/// <remarks>
/// The response body is preserved on both paths, so the frontend keeps localizing by
/// <see cref="ErrorCode"/> rather than sniffing HTTP statuses. Deliberately not
/// <c>ControllerBase.Forbid()</c>: that returns an empty body and invokes the authentication handler's
/// challenge, which would discard the error payload the client renders.
/// </remarks>
internal static class ServiceErrorActionResult
{
    // Keyed off the enum's PERMISSION_ prefix rather than a hand-maintained set, so a newly added
    // permission code returns 403 without having to be registered here. Member names are part of the
    // API contract (see ErrorCode) and so are stable to match on. Built once per process.
    private static readonly FrozenSet<ErrorCode> PermissionCodes =
        Enum.GetValues<ErrorCode>()
            .Where(code => code.ToString().StartsWith("PERMISSION_", StringComparison.Ordinal))
            .ToFrozenSet();

    /// <summary>
    /// 403 when <paramref name="response"/> carries a permission error, otherwise 400. Call only after
    /// <c>ServiceValidationHelper.HasErrors</c> has confirmed the response failed.
    /// </summary>
    public static ObjectResult ToErrorResult<T>(this ControllerBase controller, BaseEntityResponse<T> response) =>
        IsPermissionDenied(response)
            ? controller.StatusCode(StatusCodes.Status403Forbidden, response)
            : controller.BadRequest(response);

    private static bool IsPermissionDenied<T>(BaseEntityResponse<T> response) =>
        response.Messages.Any(message =>
            message.Type == MessageType.ERROR
            && message.Code.HasValue
            && PermissionCodes.Contains(message.Code.Value));
}

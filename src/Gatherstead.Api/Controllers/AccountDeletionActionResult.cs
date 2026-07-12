using Gatherstead.Api.Contracts.AccountDeletion;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.AccountDeletion;
using Microsoft.AspNetCore.Mvc;

namespace Gatherstead.Api.Controllers;

/// <summary>Maps an <see cref="AccountDeletionResult"/> to the appropriate HTTP response, shared by the
/// self-service (<c>/api/me</c>) and admin (<c>/api/admin/users</c>) deletion endpoints. Errors travel
/// on the standard <see cref="BaseEntityResponse{T}"/> message rail so the frontend localizes them by
/// <see cref="ErrorCode"/> rather than sniffing HTTP statuses.</summary>
internal static class AccountDeletionActionResult
{
    public static ActionResult<AccountDeletionResponse> ToActionResult(ControllerBase controller, AccountDeletionResult result)
    {
        var response = new AccountDeletionResponse();
        switch (result.Status)
        {
            case AccountDeletionStatus.Deleted:
                response.SetSuccess(new AccountDeletionResultDto(
                    result.UserId,
                    result.TenantsPurged,
                    result.MembershipsRemoved,
                    result.DirectoryOutcome.ToString()));
                return controller.Ok(response);

            case AccountDeletionStatus.NotFound:
                response.AddResponseMessage(
                    MessageType.ERROR,
                    ErrorCode.ENTITY_NOT_FOUND,
                    "User not found.",
                    new Dictionary<string, string> { ["entity"] = "user" });
                return controller.NotFound(response);

            case AccountDeletionStatus.BlockedByOwnership:
                response.AddResponseMessage(
                    MessageType.ERROR,
                    ErrorCode.ACCOUNT_SOLE_OWNER,
                    "You are the sole owner of one or more shared groups. Transfer ownership, " +
                    "remove the other members, or delete the group before deleting your account.",
                    new Dictionary<string, string> { ["groups"] = string.Join(", ", result.BlockingTenantNames) });
                return controller.Conflict(response);

            default:
                return controller.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

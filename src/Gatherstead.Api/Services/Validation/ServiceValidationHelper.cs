using System.Linq;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Db;

namespace Gatherstead.Api.Services.Validation;

public static class ServiceValidationHelper
{
    public static bool ValidateTenantContext<T>(Guid tenantId, ICurrentTenantContext currentTenantContext, BaseEntityResponse<T> response)
    {
        if (tenantId == Guid.Empty)
        {
            response.AddResponseMessage(MessageType.ERROR, "A valid tenant identifier is required.");
        }

        var currentTenantId = currentTenantContext.TenantId;
        if (currentTenantId.HasValue && currentTenantId.Value != tenantId)
        {
            response.AddResponseMessage(MessageType.ERROR, "The tenant context does not match the requested tenant.");
        }

        return !HasErrors(response);
    }

    public static bool TryNormalizeString<T>(string? value, string fieldName, BaseEntityResponse<T> response, out string normalizedValue)
    {
        normalizedValue = (value ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            response.AddResponseMessage(MessageType.ERROR, $"{fieldName} is required.");
            return false;
        }

        return true;
    }

    public static bool HasErrors<T>(BaseEntityResponse<T> response) =>
        response.Messages.Any(message => message.Type == MessageType.ERROR);
}

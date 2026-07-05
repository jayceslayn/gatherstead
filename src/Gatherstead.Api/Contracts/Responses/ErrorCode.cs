namespace Gatherstead.Api.Contracts.Responses;

/// <summary>
/// Stable, machine-readable codes for business-rule errors returned on a <see cref="ResponseMessage"/>.
/// The frontend maps these to localized message templates (interpolating <see cref="ResponseMessage.Params"/>),
/// falling back to the human-readable <see cref="ResponseMessage.Message"/> when it has no template for a code.
/// Members are SCREAMING_SNAKE so the global JsonStringEnumConverter serializes them verbatim
/// (e.g. "ENTITY_CONFLICT"); do not rename existing members — the codes are part of the API contract.
/// </summary>
public enum ErrorCode
{
    /// <summary>A referenced entity does not exist. Param: <c>entity</c> (token).</summary>
    ENTITY_NOT_FOUND,

    /// <summary>A uniqueness constraint was violated (e.g. duplicate name). Params: <c>entity</c> (token), <c>name</c>.</summary>
    ENTITY_CONFLICT,

    /// <summary>A required field or request was missing.</summary>
    VALIDATION_REQUIRED,

    /// <summary>A field value was malformed.</summary>
    VALIDATION_FORMAT,

    /// <summary>A field value was outside its permitted range.</summary>
    VALIDATION_RANGE,

    /// <summary>An operation would exceed a capacity limit.</summary>
    CAPACITY_EXCEEDED,

    /// <summary>The target entity is in a state that does not permit the operation.</summary>
    INVALID_STATE,

    /// <summary>The entity cannot be changed/removed because other records depend on it.</summary>
    DEPENDENCY_EXISTS,

    /// <summary>A supplied reference (id/foreign key) is invalid or does not resolve.</summary>
    INVALID_REFERENCE,

    // Permission variants — distinct codes so each localizes cleanly (rather than one code with an
    // English action phrase carried as a param).
    PERMISSION_TENANT_MANAGE,
    PERMISSION_EVENT_MANAGE,
    PERMISSION_HOUSEHOLD_MANAGE,
    PERMISSION_MEMBER_EDIT,
    PERMISSION_INTENT_ASSIGN,
    PERMISSION_MEALPLAN_MENU,
    PERMISSION_SENSITIVE_READ,
    PERMISSION_ROLE_ESCALATION,
}

namespace Gatherstead.Api.Contracts.Attributes;

public record AttributeDto(
    Guid Id,
    string Key,
    string Value,
    byte TenantMinRole,
    byte? HouseholdMinRole = null);

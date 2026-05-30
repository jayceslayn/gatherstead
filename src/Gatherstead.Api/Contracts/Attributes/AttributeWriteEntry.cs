namespace Gatherstead.Api.Contracts.Attributes;

public class AttributeWriteEntry
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public byte TenantMinRole { get; init; }
    public byte? HouseholdMinRole { get; init; }
}

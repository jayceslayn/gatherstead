namespace Gatherstead.Api.Contracts.Attributes;

public interface IAttributeWriteRequest
{
    string Key { get; }
    string Value { get; }
    byte TenantMinRole { get; }
}

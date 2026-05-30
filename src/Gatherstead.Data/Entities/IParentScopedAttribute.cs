namespace Gatherstead.Data.Entities;

public interface IParentScopedAttribute
{
    Guid Id { get; set; }
    Guid TenantId { get; set; }
    string Key { get; set; }
    string Value { get; set; }
    byte TenantMinRole { get; set; }
}

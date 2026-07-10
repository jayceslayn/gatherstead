namespace Gatherstead.Data.Entities;

/// A parent-scoped attribute that can additionally be gated by the caller's role in an owning
/// household (null means no household-level grant; visibility then hinges on TenantMinRole alone).
public interface IHouseholdScopedAttribute : IParentScopedAttribute
{
    byte? HouseholdMinRole { get; set; }
}

namespace Gatherstead.Api.Contracts.TenantUsers;

public class SetLinkedMemberRequest
{
    /// <summary>Null to unlink; a MemberId to link this user's TenantUser to that HouseholdMember.</summary>
    public Guid? MemberId { get; init; }
}

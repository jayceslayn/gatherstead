using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId), IsUnique = true)]
public class MemberPreferenceSettings : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdMemberId { get; set; }
    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    [MaxLength(35)]
    public string PreferredLanguage { get; set; } = "en-US";

    [MaxLength(100)]
    public string TimeZone { get; set; } = "UTC";
}

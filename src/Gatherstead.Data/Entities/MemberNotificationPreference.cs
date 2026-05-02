using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(HouseholdMemberId), nameof(Channel), nameof(Category), IsUnique = true)]
public class MemberNotificationPreference : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid HouseholdMemberId { get; set; }
    [ForeignKey(nameof(HouseholdMemberId))]
    public HouseholdMember? HouseholdMember { get; set; }

    public NotificationChannel Channel { get; set; }

    public NotificationCategory Category { get; set; }

    public NotificationMode Mode { get; set; } = NotificationMode.Immediate;
}

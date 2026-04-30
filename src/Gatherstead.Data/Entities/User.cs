using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(ExternalId), IsUnique = true)]
public class User : AuditableEntity
{
    public Guid Id { get; set; }
    [Required]
    [MaxLength(200)]
    public string ExternalId { get; set; } = string.Empty; // Entra ID subject

    public bool IsAppAdmin { get; set; }

    [EmailAddress]
    [MaxLength(320)]
    public string? PreferredEmail { get; set; }

    [Phone]
    [MaxLength(32)]
    public string? PreferredPhoneNumber { get; set; }

    public ICollection<TenantUser> Tenants { get; set; } = new List<TenantUser>();
    public ICollection<HouseholdMember> HouseholdMembers { get; set; } = new List<HouseholdMember>();
    public ICollection<UserNotificationPreference> NotificationPreferences { get; set; } = new List<UserNotificationPreference>();
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(ExternalId), IsUnique = true)]
[Index(nameof(Email))]
public class User : AuditableEntity
{
    public Guid Id { get; set; }
    [Required]
    [MaxLength(200)]
    public string ExternalId { get; set; } = string.Empty; // Entra ID subject

    // Normalized (lower-cased) email captured from the identity provider on first login.
    // Used to match pending invitations so an invite can be claimed without knowing the
    // user's external subject ahead of time.
    [MaxLength(256)]
    public string? Email { get; set; }

    // Seeded once from the id_token "name" claim when the row is first created, then app-owned:
    // the user can edit it via PUT /api/me and later logins never overwrite it (Entra has no
    // self-service profile editing, so the editable copy lives here, not in the directory).
    [MaxLength(256)]
    public string? DisplayName { get; set; }

    public bool IsAppAdmin { get; set; }

    public ICollection<TenantUser> Tenants { get; set; } = new List<TenantUser>();
    public ICollection<HouseholdUser> HouseholdUsers { get; set; } = new List<HouseholdUser>();
}

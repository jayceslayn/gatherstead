using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(UserId), IsUnique = true)]
public class UserPreferenceSettings : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [EmailAddress]
    [MaxLength(320)]
    public string? PreferredEmail { get; set; }

    [Phone]
    [MaxLength(32)]
    public string? PreferredPhoneNumber { get; set; }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(Name), IsUnique = true)]
public class Tenant : AuditableEntity
{
    public Guid Id { get; set; }
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public ICollection<Household> Households { get; set; } = new List<Household>();
    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
}

using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(TenantId), nameof(AccommodationId))]
[Index(nameof(TenantId), nameof(AccommodationId), nameof(Size), IsUnique = true)]
public class AccommodationBed : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid AccommodationId { get; set; }

    [ForeignKey(nameof(AccommodationId))]
    public Accommodation? Accommodation { get; set; }

    public BedSize Size { get; set; }
    public int Quantity { get; set; }
}

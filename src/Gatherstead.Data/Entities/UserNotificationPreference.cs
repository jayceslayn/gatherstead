using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gatherstead.Data.Entities;

[Index(nameof(UserId), nameof(Channel), nameof(Category), IsUnique = true)]
public class UserNotificationPreference : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public NotificationChannel Channel { get; set; }

    public NotificationCategory Category { get; set; }

    public NotificationMode Mode { get; set; } = NotificationMode.Immediate;
}

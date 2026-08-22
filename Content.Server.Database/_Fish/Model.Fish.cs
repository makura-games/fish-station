using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbContext
{
    public DbSet<FishAchievementProgress> FishAchievementProgress { get; set; } = default!;

    private static void ConfigureFishModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FishAchievementProgress>()
            .HasIndex(e => new { e.PlayerUserId, e.AchievementId })
            .IsUnique();
    }
}

/// <summary>
/// Account-wide прогресс/unlock достижения. Строка создаётся только при прогрессе или unlock.
/// </summary>
[Table("fish_achievement_progress")]
public sealed class FishAchievementProgress
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, ForeignKey("Player")]
    public Guid PlayerUserId { get; set; }

    [Required, MaxLength(128)]
    public string AchievementId { get; set; } = string.Empty;

    public int Progress { get; set; }

    public DateTimeOffset? UnlockedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

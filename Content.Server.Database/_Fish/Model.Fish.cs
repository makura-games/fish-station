using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbContext
{
    public DbSet<FishAchievementProgress> FishAchievementProgress { get; set; } = default!;

    public DbSet<FishRpuProfile> FishRpuProfile { get; set; } = default!;

    private static void ConfigureFishModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FishAchievementProgress>()
            .HasIndex(e => new { e.PlayerUserId, e.AchievementId })
            .IsUnique();

        modelBuilder.Entity<FishRpuProfile>()
            .HasIndex(e => e.PlayerUserId)
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

/// <summary>
/// Карма игрока для режима персонального угнетения. Хранится по аккаунту, переживает раунды.
/// </summary>
[Table("fish_rpu_profile")]
public sealed class FishRpuProfile
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, ForeignKey("Player")]
    public Guid PlayerUserId { get; set; }

    /// <summary>Текущая карма от -100 до +100.</summary>
    public float Karma { get; set; }

    /// <summary>Карма на момент последнего сохранения (для отладки сбросов).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

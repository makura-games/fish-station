using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;

namespace Content.Server._Fish.Research.Bluespace;

/// <summary>Owns finite, station-scoped bluespace background, passive decay and safety warnings.</summary>
public sealed partial class FishBluespaceBackgroundSystem : EntitySystem
{
    [Dependency] private StationSystem _station = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityQuery<FishBluespaceBackgroundComponent> _backgroundQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;
    [Dependency] private EntityQuery<MetaDataComponent> _metaQuery = default!;
    [Dependency] private EntityQuery<StationDataComponent> _stationDataQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(FishBluespaceResearchSystem));
    }

    /// <summary>Finds the owning station; unowned devices never share a fallback background.</summary>
    public bool TryGetScope(EntityUid device, out EntityUid scope)
    {
        scope = default;
        if (TerminatingOrDeleted(device) || !_transformQuery.TryComp(device, out var transform))
            return false;

        if (_station.GetOwningStation(device, transform) is not { } station ||
            TerminatingOrDeleted(station) || !_stationDataQuery.HasComp(station))
            return false;

        scope = station;
        return true;
    }

    /// <summary>Reads the current finite background without creating or changing station state.</summary>
    public float GetLevel(EntityUid device)
    {
        return TryGetScope(device, out var scope) && _backgroundQuery.TryComp(scope, out var background)
            ? FiniteLevel(background.Level, 0f)
            : 0f;
    }

    /// <summary>Checks the station safety latch, including its configured recovery hysteresis.</summary>
    public bool IsCritical(EntityUid device)
    {
        if (!TryGetScope(device, out var scope) || !_backgroundQuery.TryComp(scope, out var background))
            return false;

        return background.Critical ||
               FiniteLevel(background.Level, 0f) >= FiniteLevel(background.CriticalThreshold, 95f);
    }

    /// <summary>Adjusts an unpaused station's background, rejecting nonfinite input and missing ownership.</summary>
    public bool TryAdjustLevel(EntityUid device, float delta)
    {
        if (!CanAdjustLevel(device, delta) || !TryGetScope(device, out var scope))
            return false;

        var background = EnsureComp<FishBluespaceBackgroundComponent>(scope);
        SetLevel(background, (double) FiniteLevel(background.Level, 0f) + delta);
        return true;
    }

    /// <summary>Checks whether a finite background adjustment can affect an active station.</summary>
    public bool CanAdjustLevel(EntityUid device, float delta)
    {
        return float.IsFinite(delta) && TryGetScope(device, out var scope) &&
               _metaQuery.TryComp(device, out var deviceMeta) && !deviceMeta.EntityPaused &&
               _metaQuery.TryComp(scope, out var stationMeta) && !stationMeta.EntityPaused;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!float.IsFinite(frameTime) || frameTime <= 0f)
            return;

        var elapsed = Math.Min(frameTime, 1f);
        var query = EntityQueryEnumerator<FishBluespaceBackgroundComponent>();
        while (query.MoveNext(out var uid, out var background))
        {
            if (TerminatingOrDeleted(uid) || !_stationDataQuery.HasComp(uid) ||
                !_metaQuery.TryComp(uid, out var meta) || meta.EntityPaused)
                continue;

            background.WarningCooldownRemaining = Math.Max(0f, background.WarningCooldownRemaining - elapsed);
            var decay = float.IsFinite(background.DecayPerSecond)
                ? Math.Clamp(background.DecayPerSecond, 0f, 100f)
                : 0f;
            SetLevel(background, (double) FiniteLevel(background.Level, 0f) - decay * elapsed);
            UpdateWarning((uid, background));
        }
    }

    private static void SetLevel(FishBluespaceBackgroundComponent background, double level)
    {
        background.Level = double.IsFinite(level) ? (float) Math.Clamp(level, 0d, 100d) : 0f;
        var critical = FiniteLevel(background.CriticalThreshold, 95f);
        var recovery = Math.Min(FiniteLevel(background.RecoveryThreshold, 80f), critical);
        if (background.Critical)
        {
            if (background.Level < recovery)
                background.Critical = false;
        }
        else if (background.Level >= critical)
        {
            background.Critical = true;
        }
    }

    private void UpdateWarning(Entity<FishBluespaceBackgroundComponent> ent)
    {
        var critical = FiniteLevel(ent.Comp.CriticalThreshold, 95f);
        var danger = Math.Min(FiniteLevel(ent.Comp.DangerThreshold, 85f), critical);
        var warning = Math.Min(FiniteLevel(ent.Comp.WarningThreshold, 60f), danger);
        var band = ent.Comp.Critical || ent.Comp.Level >= critical
            ? 3
            : ent.Comp.Level >= danger ? 2 : ent.Comp.Level >= warning ? 1 : 0;
        // Fish-edit: до конца cooldown помним максимальную объявленную угрозу, чтобы колебания порога не повторяли предупреждение.
        if (band < ent.Comp.LastWarningBand && ent.Comp.WarningCooldownRemaining <= 0f)
            ent.Comp.LastWarningBand = band;

        if (band <= ent.Comp.LastWarningBand)
            return;

        ent.Comp.LastWarningBand = band;
        ent.Comp.WarningCooldownRemaining = (float) Math.Clamp(ent.Comp.WarningCooldown.TotalSeconds, 1d, 3600d);
        var message = band switch
        {
            3 => Loc.GetString("fish-bluespace-critical", ("level", MathF.Round(ent.Comp.Level))),
            2 => Loc.GetString("fish-bluespace-danger", ("level", MathF.Round(ent.Comp.Level))),
            _ => Loc.GetString("fish-bluespace-warning", ("level", MathF.Round(ent.Comp.Level))),
        };
        _chat.DispatchStationAnnouncement(ent, message, Loc.GetString("fish-bluespace-warning-source"),
            playDefault: false, playTts: false);
    }

    private static float FiniteLevel(float level, float fallback)
    {
        return float.IsFinite(level) ? Math.Clamp(level, 0f, 100f) : fallback;
    }
}

using Content.Shared.CCVar;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Shared.AlertLevel;

public sealed partial class AlertLevelSystem
{
    /// <summary>
    /// Returns whether crew may manually change alert protocols on the station.
    /// </summary>
    public bool IsSelectable(Entity<AlertLevelComponent?> station)
    {
        if (!Resolve(station, ref station.Comp)
            || station.Comp.IsLevelLocked)
        {
            return false;
        }

        foreach (var additionalLevel in station.Comp.ActiveAdditionalLevels)
        {
            if (_prototype.Resolve(additionalLevel, out var proto)
                && proto.DisableSelection)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether an additional station alert level can be enabled or disabled.
    /// </summary>
    public bool CanSetAdditionalLevel(
        Entity<AlertLevelComponent?> station,
        ProtoId<AlertLevelPrototype> level,
        bool enabled,
        bool force = false)
    {
        if (!Resolve(station, ref station.Comp)
            || !station.Comp.AvailableAlertLevels.Contains(level)
            || !_prototype.Resolve(level, out var proto)
            || !proto.IsAdditional
            || station.Comp.ActiveAdditionalLevels.Contains(level) == enabled)
        {
            return false;
        }

        if (force)
            return true;

        if (!CanChangeAlertLevel(station))
            return false;

        return proto.Selectable && !proto.DisableSelection;
    }

    /// <summary>
    /// Attempts to explicitly enable or disable an additional station alert level.
    /// </summary>
    public bool TrySetAdditionalLevel(
        Entity<AlertLevelComponent?> station,
        ProtoId<AlertLevelPrototype> level,
        bool enabled,
        bool playSound = true,
        bool announce = true,
        bool force = false)
    {
        if (!CanSetAdditionalLevel(station, level, enabled, force))
            return false;

        Resolve(station, ref station.Comp);

        if (!force)
        {
            station.Comp!.DelayedUntil = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.GameAlertLevelChangeDelay));
        }

        DoSetAdditionalLevel((station.Owner, station.Comp!), level, enabled, playSound, announce);
        return true;
    }

    /// <summary>
    /// Returns whether the specified primary or additional alert level is active.
    /// </summary>
    public bool IsLevelActive(Entity<AlertLevelComponent?> station, ProtoId<AlertLevelPrototype> level)
    {
        if (!Resolve(station, ref station.Comp))
            return false;

        return station.Comp.CurrentAlertLevel == level || station.Comp.ActiveAdditionalLevels.Contains(level);
    }

    /// <summary>
    /// Returns whether any of the supplied primary or additional alert levels is active.
    /// </summary>
    public bool IsAnyLevelActive(
        Entity<AlertLevelComponent?> station,
        IReadOnlyCollection<ProtoId<AlertLevelPrototype>> levels)
    {
        if (!Resolve(station, ref station.Comp))
            return false;

        foreach (var level in levels)
        {
            if (station.Comp.CurrentAlertLevel == level || station.Comp.ActiveAdditionalLevels.Contains(level))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the primary level followed by all active additional levels in prototype order.
    /// </summary>
    public List<ProtoId<AlertLevelPrototype>> GetActiveLevels(Entity<AlertLevelComponent?> station)
    {
        var result = new List<ProtoId<AlertLevelPrototype>>();
        if (!Resolve(station, ref station.Comp))
            return result;

        result.Add(station.Comp.CurrentAlertLevel);
        foreach (var level in station.Comp.AvailableAlertLevels)
        {
            if (station.Comp.ActiveAdditionalLevels.Contains(level))
                result.Add(level);
        }

        return result;
    }

    /// <summary>
    /// Gets the active alert level with the highest visual priority.
    /// </summary>
    public bool TryGetVisualAlertLevel(
        Entity<AlertLevelComponent?> station,
        out ProtoId<AlertLevelPrototype> level,
        out AlertLevelPrototype detail)
    {
        level = default;
        detail = default!;

        if (!Resolve(station, ref station.Comp)
            || !_prototype.Resolve(station.Comp.CurrentAlertLevel, out var currentDetail))
        {
            return false;
        }

        detail = currentDetail;
        level = station.Comp.CurrentAlertLevel;
        foreach (var additionalLevel in station.Comp.ActiveAdditionalLevels)
        {
            if (!_prototype.Resolve(additionalLevel, out var additionalDetail)
                || additionalDetail.VisualPriority <= detail.VisualPriority)
            {
                continue;
            }

            level = additionalLevel;
            detail = additionalDetail;
        }

        return true;
    }

    private void DoSetAdditionalLevel(
        Entity<AlertLevelComponent> station,
        ProtoId<AlertLevelPrototype> level,
        bool enabled,
        bool playSound,
        bool announce)
    {
        var detail = _prototype.Index(level);

        if (enabled)
            station.Comp.ActiveAdditionalLevels.Add(level);
        else
            station.Comp.ActiveAdditionalLevels.Remove(level);

        Dirty(station);

        var ev = new AdditionalAlertLevelChangedEvent(station, level, enabled);
        RaiseLocalEvent(ref ev);

        if (_net.IsClient)
            return;

        if (announce)
            AnnounceAdditionalLevel(station, detail, enabled, playSound);
    }

    private void AnnounceAdditionalLevel(
        EntityUid station,
        AlertLevelPrototype detail,
        bool enabled,
        bool playSound)
    {
        var name = detail.LocalizedName.ToLowerInvariant();

        string announcement;
        if (enabled)
        {
            announcement = Loc.GetString("alert-level-announcement",
                ("name", name),
                ("announcement", AlertLevelAnnouncement(detail)));
        }
        else
        {
            announcement = Loc.GetString("alert-level-additional-disabled-announcement", ("name", name));
        }

        var playDefault = false;
        if (enabled && playSound)
        {
            if (detail.Sound != null)
            {
                var filter = _station.GetInOwningStation(station);
                _audio.PlayGlobal(detail.Sound, filter, true);
            }
            else
            {
                playDefault = true;
            }
        }

        _chat.DispatchStationAnnouncement(
            station,
            announcement,
            playDefaultSound: playDefault,
            colorOverride: detail.Color,
            sender: MetaData(station).EntityName);
    }
}

/// <summary>
/// Raised after an additional station alert level is enabled or disabled.
/// </summary>
[ByRefEvent]
public readonly record struct AdditionalAlertLevelChangedEvent(
    EntityUid Station,
    ProtoId<AlertLevelPrototype> AlertLevel,
    bool Enabled);

using System.Threading;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Sunrise.BluespaceArtillery;
using Content.Shared.Administration.Logs;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sunrise.BluespaceArtillery;

/// <summary>
/// Серверная логика наведения и запуска блюспейс-артиллерии.
/// </summary>
public sealed class BluespaceArtilleryDesignatorSystem : SharedBluespaceArtilleryDesignatorSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceArtilleryDesignatorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BluespaceArtilleryDesignatorComponent, BluespaceArtilleryDesignatorDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<BluespaceArtilleryDesignatorComponent> ent, ref AfterInteractEvent args)
    {
        // В attack mode InteractionSystem не доходит сюда при предмете в руке — отдельная проверка не нужна.
        if (args.Handled)
            return;

        if (!args.ClickLocation.IsValid(EntityManager))
            return;

        if (!TryGetCurrentMode(ent, out _))
            return;

        if (!TryComp<LimitedChargesComponent>(ent, out var charges) || !_charges.HasCharges((ent.Owner, charges), 1))
        {
            _popup.PopupEntity(Loc.GetString("bluespace-artillery-empty"), ent, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var location = args.ClickLocation;
        var marker = Spawn(ent.Comp.TargetMarker, location);
        var modeIndex = ent.Comp.CurrentFireMode;

        if (ent.Comp.TargetingDelay <= TimeSpan.Zero)
        {
            ConfirmStrike(ent, location, modeIndex, marker, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.TargetingDelay,
            new BluespaceArtilleryDesignatorDoAfterEvent(GetNetCoordinates(location), modeIndex, GetNetEntity(marker)),
            eventTarget: ent,
            used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            BreakOnHandChange = true,
            NeedHand = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTool,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            QueueDel(marker);
    }

    private void OnDoAfter(Entity<BluespaceArtilleryDesignatorComponent> ent, ref BluespaceArtilleryDesignatorDoAfterEvent args)
    {
        EntityUid? marker = null;
        if (args.Effect is { } netMarker)
            marker = GetEntity(netMarker);

        if (args.Cancelled)
        {
            if (marker != null && !Deleted(marker.Value))
                QueueDel(marker.Value);
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;

        var location = GetCoordinates(args.Location);
        if (!location.IsValid(EntityManager))
        {
            if (marker != null && !Deleted(marker.Value))
                QueueDel(marker.Value);
            return;
        }

        ConfirmStrike(ent, location, args.FireModeIndex, marker, args.User);
    }

    private void ConfirmStrike(
        Entity<BluespaceArtilleryDesignatorComponent> ent,
        EntityCoordinates location,
        int modeIndex,
        EntityUid? marker,
        EntityUid? user)
    {
        if (modeIndex < 0 || modeIndex >= ent.Comp.FireModes.Count)
        {
            DeleteMarker(marker);
            return;
        }

        if (!TryComp<LimitedChargesComponent>(ent, out var charges) || !_charges.TryUseCharge((ent.Owner, charges)))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("bluespace-artillery-empty"), ent, user.Value);
            DeleteMarker(marker);
            return;
        }

        var mode = ent.Comp.FireModes[modeIndex];
        var mapCoords = _transform.ToMapCoordinates(location);

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(ent.Comp.Announcement),
            Loc.GetString(ent.Comp.AnnouncementSender),
            playDefault: false,
            colorOverride: ent.Comp.AnnouncementColor);

        _audio.PlayGlobal(ent.Comp.AnnouncementSound, Filter.Broadcast(), true);

        if (user != null)
        {
            _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
                $"{ToPrettyString(user.Value):user} requested bluespace artillery ({Loc.GetString(mode.Name)}) at {mapCoords:coordinates} using {ToPrettyString(ent):tool}");
        }

        var strikeDelay = ent.Comp.StrikeDelay;
        var explosionType = mode.ExplosionType.Id;
        var totalIntensity = mode.TotalIntensity;
        var slope = mode.IntensitySlope;
        var maxIntensity = mode.MaxIntensity;
        var tileBreakScale = mode.TileBreakScale;
        var maxTileBreak = mode.MaxTileBreak;
        var canCreateVacuum = mode.CanCreateVacuum;
        var markerUid = marker;
        var cause = user;

        // Удар фиксируется в выбранной точке и не зависит от дальнейшего положения игрока/предмета.
        Timer.Spawn(strikeDelay, () =>
        {
            DeleteMarker(markerUid);

            if (!_mapManager.MapExists(mapCoords.MapId))
                return;

            _explosion.QueueExplosion(
                mapCoords,
                explosionType,
                totalIntensity,
                slope,
                maxIntensity,
                cause,
                tileBreakScale,
                maxTileBreak,
                canCreateVacuum);
        }, CancellationToken.None);
    }

    private void DeleteMarker(EntityUid? marker)
    {
        if (marker != null && !Deleted(marker.Value))
            QueueDel(marker.Value);
    }
}

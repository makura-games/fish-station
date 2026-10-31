using Content.Server.Power.Components;
using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Integration Cog: вставка в APC и пассивный drain ~5 W/s в пул культа.
/// </summary>
public sealed partial class FishIntegrationCogSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishClockworkPowerSystem _power = default!;
    [Dependency] private FishClockworkServantSystem _servant = default!;

    private EntityQuery<BatteryComponent> _batteryQuery;

    public override void Initialize()
    {
        base.Initialize();
        _batteryQuery = GetEntityQuery<BatteryComponent>();
        SubscribeLocalEvent<FishIntegrationCogComponent, AfterInteractEvent>(OnAfterInteract);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Fish-edit: drain на APC-хосте после удаления предмета-шестерни.
        var query = EntityQueryEnumerator<FishIntegrationCogHostComponent>();
        while (query.MoveNext(out var uid, out var host))
        {
            var drain = host.DrainWattsPerSecond * frameTime;
            if (_batteryQuery.TryComp(uid, out var battery) &&
                _battery.TryUseCharge((uid, battery), drain))
            {
                _power.TryAdjustPower(drain, quiet: true);
            }
        }
    }

    private void OnAfterInteract(Entity<FishIntegrationCogComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (!_servant.IsServant(args.User))
            return;

        TryInstall(ent, args.User, args.Target.Value);
        args.Handled = true;
    }

    public bool TryInstall(Entity<FishIntegrationCogComponent> cog, EntityUid user, EntityUid target)
    {
        if (!CanInstall(cog, user, target))
            return false;

        DoInstall(cog, user, target);
        return true;
    }

    public bool CanInstall(Entity<FishIntegrationCogComponent> cog, EntityUid user, EntityUid target, bool quiet = false)
    {
        if (cog.Comp.Installed)
            return false;

        if (!HasComp<ApcComponent>(target))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-clockwork-cog-need-apc"), user, user);
            return false;
        }

        if (HasComp<FishIntegrationCogHostComponent>(target))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("fish-clockwork-cog-installed"), user, user);
            return false;
        }

        return true;
    }

    private void DoInstall(Entity<FishIntegrationCogComponent> cog, EntityUid user, EntityUid apc)
    {
        var host = EnsureComp<FishIntegrationCogHostComponent>(apc);
        host.DrainWattsPerSecond = cog.Comp.DrainWattsPerSecond;
        Dirty(apc, host);

        _audio.PlayPvs(
            new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/machines/clockcult/integration_cog_install.ogg"),
            apc);
        _popup.PopupEntity(Loc.GetString("fish-clockwork-cog-installed"), user, user);

        // Fish-edit: удаляем предмет, чтобы нельзя было поднять установленную шестерню.
        QueueDel(cog.Owner);
    }
}

using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Fish.Netrunner;

/// <summary>Predicted handling for stackable coolant packs that lower a character's body temperature down to 20 C.</summary>
public sealed class NetrunnerCoolantSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTemperatureSystem _temperature = default!;
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<StackComponent> _stackQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NetrunnerCoolantPackComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<NetrunnerCoolantPackComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<NetrunnerCoolantPackComponent, NetrunnerCoolantDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<NetrunnerCoolantPackComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryApplyCoolant(ent, args.User, target))
            args.Handled = true;
    }

    private void OnUseInHand(Entity<NetrunnerCoolantPackComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryApplyCoolant(ent, args.User, args.User))
            args.Handled = true;
    }

    /// <summary>Attempts to start cooling a character using the coolant pack.</summary>
    public bool TryApplyCoolant(Entity<NetrunnerCoolantPackComponent> ent, EntityUid user, EntityUid target)
    {
        if (!CanApplyCoolant(ent, user, target))
            return false;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new NetrunnerCoolantDoAfterEvent(),
            ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return false;

        _audio.PlayPredicted(ent.Comp.BeginSound, target, user);
        return true;
    }

    /// <summary>Checks whether the coolant pack can cool the target without raising its temperature.</summary>
    public bool CanApplyCoolant(Entity<NetrunnerCoolantPackComponent> ent, EntityUid user, EntityUid target, bool quiet = false)
    {
        if (!_mobStateQuery.HasComp(target) || !_temperatureQuery.TryComp(target, out var temp) || temp.HeatCapacity <= 0)
            return false;

        if (_stackQuery.TryComp(ent.Owner, out var stack) && stack.Count <= 0)
            return false;

        if (temp.Temperature <= ent.Comp.TargetTemperature)
        {
            if (!quiet)
                _popup.PopupClient(Loc.GetString("fish-coolant-pack-already-cool"), target, user);
            return false;
        }

        return true;
    }

    private void OnDoAfter(Entity<NetrunnerCoolantPackComponent> ent, ref NetrunnerCoolantDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!CanApplyCoolant(ent, args.User, target, quiet: true))
            return;

        if (_stackQuery.TryComp(ent.Owner, out var stack))
        {
            if (!_stack.TryUse((ent.Owner, stack), 1))
                return;
        }
        else if (_net.IsServer)
        {
            QueueDel(ent.Owner);
        }

        if (_net.IsServer && _temperatureQuery.TryComp(target, out var temp) && temp.Temperature > ent.Comp.TargetTemperature)
        {
            var delta = ent.Comp.TargetTemperature - temp.Temperature;
            _temperature.ChangeHeat((target, temp), delta * temp.HeatCapacity, ignoreHeatResistance: true);
        }

        _audio.PlayPredicted(ent.Comp.EndSound, target, args.User);
    }
}

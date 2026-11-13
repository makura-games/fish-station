using Content.Server._Fish.Mech;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared._Fish.Mech.Events;

namespace Content.Server.Mech.Systems;

public sealed partial class MechSystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MechBrainCombatSystem _mechBrainCombat = default!;

    private void InitializeFish()
    {
        SubscribeLocalEvent<MechComponent, MechBrainInsertDoAfterEvent>(OnBrainInsertDoAfter);
    }

    private void OnBrainInsertDoAfter(Entity<MechComponent> ent, ref MechBrainInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target || args.Args.Used is not { } used)
            return;

        args.Handled = TryEnterMech(ent.AsNullable(), args.User, used);
    }

    private void RestoreFishBrainState(EntityUid pilot)
    {
        if (HasComp<MechBrainCombatComponent>(pilot))
            _mechBrainCombat.RestoreBrainState(pilot);
    }

    // Установка мозга и обычная посадка используют общие проверки, различающие актора и пилота.
    private bool HandleFishBrainInteraction(Entity<MechComponent> ent, InteractUsingEvent args)
    {
        if (args.Handled)
            return true;

        if (!HasComp<BorgBrainComponent>(args.Used))
            return false;

        if (!CanEnterMech(ent.AsNullable(), args.User, args.Used, quiet: false))
            return true;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5), new MechBrainInsertDoAfterEvent(), ent.Owner, target: ent.Owner, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
        return true;
    }

    /// <summary>
    /// Attempts to seat a pilot, checking access on the actor installing or entering it.
    /// </summary>
    public bool TryEnterMech(Entity<MechComponent?> ent, EntityUid actor, EntityUid pilot)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!CanEnterMech(ent, actor, pilot, quiet: false))
            return false;

        return DoEnterMech((ent, ent.Comp), actor, pilot);
    }

    /// <summary>
    /// Checks pilot eligibility and the actor's access without dropping items or recording access.
    /// </summary>
    public bool CanEnterMech(Entity<MechComponent?> ent, EntityUid actor, EntityUid pilot, bool quiet = true)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!CanInsert(ent, pilot, ent.Comp))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", ent.Owner)), actor);
            return false;
        }

        if (TryComp<AccessReaderComponent>(ent, out var accessReader) && !CanAccessFishMech(ent, actor, accessReader))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("mech-no-access", ("item", ent.Owner)), actor);
            return false;
        }

        return true;
    }

    private bool CanAccessFishMech(EntityUid mech, EntityUid actor, AccessReaderComponent reader)
    {
        if (!reader.Enabled)
            return true;

        // Перегрузка с сущностью записывает доступ в журнал даже при простом просмотре verb.
        var sources = _accessReader.FindPotentialAccessItems(actor);
        var access = _accessReader.FindAccessTags(actor, sources);
        _accessReader.FindStationRecordKeys(actor, out var keys, sources);
        return _accessReader.IsAllowed(access, keys, mech, reader);
    }

    private bool DoEnterMech(Entity<MechComponent> ent, EntityUid actor, EntityUid pilot)
    {
        // Штатная проверка с журналированием выполняется только при фактической попытке входа.
        if (!_accessReader.IsAllowed(actor, ent))
            return false;

        if (actor == pilot)
        {
            foreach (var hand in _hands.EnumerateHands(actor))
            {
                _hands.DoDrop(actor, hand);
            }
        }

        if (!TryInsert(ent, pilot, ent.Comp))
            return false;

        _faction.Up(pilot, ent);
        Vehicle.RefreshCanRun(ent.Owner);
        return true;
    }
}

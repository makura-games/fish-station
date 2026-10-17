using Content.Shared.Access.Components;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Server.Mech.Systems;

public sealed partial class MechSystem
{
    // Установка мозга и обычная посадка используют общие проверки, различающие актора и пилота.
    private bool HandleFishBrainInteraction(Entity<MechComponent> ent, InteractUsingEvent args)
    {
        if (args.Handled)
            return true;

        if (!HasComp<BorgBrainComponent>(args.Used))
            return false;

        args.Handled = TryEnterMech(ent.AsNullable(), args.User, args.Used);
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

    private bool CanOfferFishMechEntry(Entity<MechComponent> ent, EntityUid actor, EntityUid? held)
    {
        if (HasComp<BorgBrainComponent>(actor))
            return false;

        return held is not { } brain ||
               !HasComp<BorgBrainComponent>(brain) ||
               !CanEnterMech(ent.AsNullable(), actor, brain);
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

        _factionSystem.Up(pilot, ent);
        _actionBlocker.UpdateCanMove(ent);
        return true;
    }
}

using Content.Shared.Access.Components;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;

#pragma warning disable IDE0130 // Частичный класс расширяет ванильную систему в папке Fish.
namespace Content.Server.Mech.Systems;
#pragma warning restore IDE0130

public sealed partial class MechSystem
{
    private bool HandleFishBrainInteraction(EntityUid uid, MechComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return false;

        if (component.Broken || !CanInsertBrain(uid, args.Used) || !CanInsert(uid, args.Used, component))
            return false;

        if (TryComp<AccessReaderComponent>(uid, out var accessReader) &&
            !_accessReader.IsAllowed(args.User, uid, accessReader))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-access", ("item", uid)), args.User);
            args.Handled = true;
            return true;
        }

        if (_whitelistSystem.IsWhitelistPass(component.PilotBlacklist, args.Used))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", uid)), args.User);
            args.Handled = true;
            return true;
        }

        // FIsh edit - ЛКМ с мозгом, pAI или MMI в руке вставляет его в мех.
        args.Handled = TryInsert(uid, args.Used, component);
        if (args.Handled)
        {
            _factionSystem.Up(args.Used, uid);
            _actionBlocker.UpdateCanMove(uid);
        }

        return args.Handled;
    }
}

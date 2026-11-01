using Content.Shared._Fish.Xenobiology.Machines;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.Xenobiology.Machines;

/// <summary>
/// Мультитул: консоль → ресайклер (упрощённый линк SS220).
/// </summary>
public sealed partial class FishXenobiologyMultitoolLinkSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<TagPrototype> MultitoolTag = "Multitool";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishXenobiologyConsoleComponent, InteractUsingEvent>(OnConsoleInteract);
        SubscribeLocalEvent<FishMonkeyRecyclerComponent, InteractUsingEvent>(OnRecyclerInteract);
    }

    private void OnConsoleInteract(Entity<FishXenobiologyConsoleComponent> console, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsMultitool(args.Used))
            return;

        args.Handled = true;
        var link = EnsureComp<FishXenobiologyMultitoolLinkComponent>(args.User);
        link.PendingConsole = console;
        _popup.PopupEntity(Loc.GetString("fish-xeno-link-console-selected"), console, args.User);
    }

    private void OnRecyclerInteract(Entity<FishMonkeyRecyclerComponent> recycler, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsMultitool(args.Used))
            return;

        if (!TryComp<FishXenobiologyMultitoolLinkComponent>(args.User, out var link) || link.PendingConsole is not { } consoleUid)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-link-need-console"), recycler, args.User);
            return;
        }

        if (!TryComp<FishXenobiologyConsoleComponent>(consoleUid, out _))
        {
            RemComp<FishXenobiologyMultitoolLinkComponent>(args.User);
            return;
        }

        args.Handled = true;
        var console = Comp<FishXenobiologyConsoleComponent>(consoleUid);
        console.LinkedRecycler = recycler;
        recycler.Comp.LinkedConsole = consoleUid;
        Dirty(consoleUid, console);
        Dirty(recycler);
        RemComp<FishXenobiologyMultitoolLinkComponent>(args.User);
        _popup.PopupEntity(Loc.GetString("fish-xeno-link-done"), recycler, args.User);
    }

    private bool IsMultitool(EntityUid uid)
    {
        return TryComp<TagComponent>(uid, out var tags) && tags.Tags.Contains(MultitoolTag);
    }
}

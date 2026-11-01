using Content.Shared._Fish.Xenobiology.Machines;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.Components;

namespace Content.Server._Fish.Xenobiology.Machines;

/// <summary>
/// Ресайклер обезьян: несколько мёртвых малышей → один куб.
/// </summary>
public sealed partial class FishMonkeyRecyclerSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishMonkeyRecyclerComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<FishMonkeyRecyclerComponent> recycler, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<MonkeyAccentComponent>(args.Used))
            return;

        if (_mobState.IsAlive(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-recycler-alive"), args.User, args.User);
            return;
        }

        args.Handled = true;
        recycler.Comp.Stored++;
        Dirty(recycler);

        QueueDel(args.Used);

        if (recycler.Comp.Stored < recycler.Comp.RequiredGrind)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-recycler-progress",
                ("stored", recycler.Comp.Stored),
                ("need", recycler.Comp.RequiredGrind)), recycler, args.User);
            return;
        }

        recycler.Comp.Stored = 0;
        Dirty(recycler);
        SpawnNextToOrDrop("FishXenobiologyMonkeyCube", recycler);
        _popup.PopupEntity(Loc.GetString("fish-xeno-recycler-done"), recycler, args.User);
    }
}

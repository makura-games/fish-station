using Content.Shared._Fish.Xenobiology.Machines;
using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server._Fish.Xenobiology.Machines;

/// <summary>
/// Переработка мёртвого слайма в экстракты по числу ядер.
/// </summary>
public sealed partial class FishSlimeProcessorSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishSlimeProcessorComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<FishSlimeProcessorComponent> processor, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishSlimeComponent>(args.Used, out var slime))
            return;

        if (_mobState.IsAlive(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-processor-alive"), args.User, args.User);
            return;
        }

        args.Handled = true;
        var extractId = FishSlimeMutation.ExtractPrototypeId(slime.ColorId);
        var count = Math.Max(1, slime.Cores);

        for (var i = 0; i < count; i++)
            SpawnNextToOrDrop(extractId, processor);

        _popup.PopupEntity(Loc.GetString("fish-xeno-processor-done", ("count", count)), processor, args.User);
        QueueDel(args.Used);
    }
}

using Content.Shared.Chemistry.EntitySystems;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Перелив реагентов в шасси КПБ не лечит его.
/// Пластыри, гипоспреи и еда отсекаются Fish-edit в своих системах:
/// на одно событие компонента движок пускает только одну подписку.
/// </summary>
public sealed partial class FishIpcMedicineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishIpcComponent, SolutionTransferAttemptEvent>(OnSolutionTransferAttempt);
    }

    private void OnSolutionTransferAttempt(Entity<FishIpcComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner || args.CancelReason != null)
            return;

        args.Cancel(Loc.GetString("fish-ipc-not-organic"));
    }
}

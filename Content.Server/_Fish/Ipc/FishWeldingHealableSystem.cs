using Content.Server.Atmos.EntitySystems;
using Content.Shared._Fish.Ipc;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server._Fish.Ipc;

/// <summary>
/// Клик сваркой по шасси. Раньше поджога: иначе зажжённая горелка поджигает Flammable и взаимодействие теряется.
/// </summary>
public sealed partial class FishWeldingHealableSystem : EntitySystem
{
    [Dependency] private SharedFishWeldingHealableSystem _weld = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishWeldingHealableComponent, InteractUsingEvent>(OnInteract, before: [typeof(FlammableSystem)]);
    }

    private void OnInteract(Entity<FishWeldingHealableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishWeldingHealingComponent>(args.Used, out var healing))
            return;

        switch (_weld.TryStartWeld((args.Used, healing), args.User, ent))
        {
            case FishWeldResult.Started:
                args.Handled = true;
                break;
            case FishWeldResult.NoDamage:
                _popup.PopupEntity(Loc.GetString("fish-ipc-weld-no-damage"), ent, args.User);
                args.Handled = true;
                break;
        }
    }
}

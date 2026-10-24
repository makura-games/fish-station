using Content.Shared._Fish.Ipc;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Popups;

namespace Content.Server._Fish.Ipc;

/// <summary>EMP: burn + shock (Paradise EMP на IPC).</summary>
public sealed partial class FishIpcEmpSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishIpcComponent, EmpPulseEvent>(OnEmp);
    }

    private void OnEmp(EntityUid uid, FishIpcComponent component, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;

        var energy = Math.Clamp(args.EnergyConsumption / 1000f, 5f, 60f);
        _damageable.TryChangeDamage(uid, new DamageSpecifier
        {
            DamageDict =
            {
                ["Heat"] = energy * 0.6f,
                ["Shock"] = energy * 0.4f,
            }
        }, true, false, origin: args.User);

        _popup.PopupEntity(Loc.GetString("fish-ipc-emp-hit"), uid, uid, PopupType.LargeCaution);
    }
}

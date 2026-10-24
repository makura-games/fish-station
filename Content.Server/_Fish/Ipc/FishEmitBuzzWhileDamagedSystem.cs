using Content.Shared._Fish.Ipc;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Ipc;

public sealed partial class FishEmitBuzzWhileDamagedSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishEmitBuzzWhileDamagedComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var buzz, out _))
        {
            if (!_mobState.IsCritical(uid) && _damageable.GetTotalDamage(uid) < 40)
                continue;

            buzz.AccumulatedFrametime += frameTime;
            if (buzz.AccumulatedFrametime < buzz.CycleDelay)
                continue;

            buzz.AccumulatedFrametime = 0;
            _audio.PlayPvs(buzz.Sound, uid);

            if (_timing.CurTime - buzz.LastBuzzPopupTime >= buzz.BuzzPopupCooldown)
            {
                buzz.LastBuzzPopupTime = _timing.CurTime;
                _popup.PopupEntity(Loc.GetString("fish-ipc-crit"), uid, uid, PopupType.MediumCaution);
            }
        }
    }
}

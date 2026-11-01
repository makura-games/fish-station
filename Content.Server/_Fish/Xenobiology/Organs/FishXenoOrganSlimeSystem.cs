using Content.Shared._Fish.Xenobiology.Organs;
using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology.Organs;

/// <summary>
/// Обученный взрослый слайм без разума игрока переваривает массу (~50 life-тиков SS220 → 100 с).
/// </summary>
public sealed partial class FishXenoOrganSlimeSystem : EntitySystem
{
    private static readonly TimeSpan ProcessDuration = TimeSpan.FromSeconds(100);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishSlimeComponent, InteractUsingEvent>(OnSlimeInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FishXenoOrganProcessingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var proc, out _))
        {
            if (_timing.CurTime < proc.FinishAt)
                continue;

            FinishProcessing(uid, proc);
        }
    }

    private void OnSlimeInteractUsing(Entity<FishSlimeComponent> slime, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<FishXenoOrganMassComponent>(args.Used, out var mass))
            return;

        if (!_mobState.IsAlive(slime) || !slime.Comp.IsAdult || !slime.Comp.Trained)
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-organ-slime-invalid"), args.User, args.User);
            return;
        }

        if (_mind.TryGetMind(slime, out _, out var mind) && mind.UserId is { } userId && _players.TryGetSessionById(userId, out _))
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-organ-slime-mind"), args.User, args.User);
            return;
        }

        if (HasComp<FishXenoOrganProcessingComponent>(slime))
        {
            _popup.PopupEntity(Loc.GetString("fish-xeno-organ-slime-busy"), args.User, args.User);
            return;
        }

        args.Handled = true;
        var proc = EnsureComp<FishXenoOrganProcessingComponent>(slime);
        proc.TrueOrganPrototype = mass.TrueOrganPrototype;
        proc.FinishAt = _timing.CurTime + ProcessDuration;
        Dirty(slime, proc);
        QueueDel(args.Used);
        _popup.PopupEntity(Loc.GetString("fish-xeno-organ-slime-start"), slime, args.User);
    }

    private void FinishProcessing(EntityUid slime, FishXenoOrganProcessingComponent proc)
    {
        RemComp<FishXenoOrganProcessingComponent>(slime);
        var organ = Spawn(proc.TrueOrganPrototype, Transform(slime).Coordinates);
        if (TryComp<FishXenoOrganComponent>(organ, out var organComp))
        {
            organComp.Identified = true;
            organComp.Quality = FishXenoOrganQuality.Pristine;
            Dirty(organ, organComp);
        }

        _popup.PopupEntity(Loc.GetString("fish-xeno-organ-slime-done"), slime);
    }
}

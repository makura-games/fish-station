using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared.Chemistry;
using Content.Shared._Fish.Xenobiology.Slimes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.Components;
using Content.Shared.Stacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Temperature;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Fish.Xenobiology.Slimes;

/// <summary>
/// Голод, рост, деление, кормление и охота слайма. Тик жизни 2 секунды, шаг ИИ 0.5 секунды.
/// </summary>
public sealed partial class FishSlimeSystem : EntitySystem
{
    private static readonly string[] SpeechKeys =
    [
        "fish-slime-speech-blop",
        "fish-slime-speech-rawr",
        "fish-slime-speech-blorble",
    ];

    private static readonly TimeSpan FrozenParalyzeDuration = TimeSpan.FromSeconds(4);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishSlimeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FishSlimeComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<FishSlimeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FishSlimeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<FishSlimeComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<FishSlimeComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<FishSlimeComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<FishSlimeComponent, RefreshMovementSpeedModifiersEvent>(OnMoveSpeed);
        SubscribeLocalEvent<FishSlimeComponent, TemperatureChangedEvent>(OnTemperature);
        SubscribeLocalEvent<FishSlimeComponent, EmpPulseEvent>(OnEmp);
        SubscribeLocalEvent<FishSlimeComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<FishSlimeComponent, ReactionEntityEvent>(OnWaterReaction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FishSlimeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var slime, out _))
        {
            if (slime.DeathHandled)
                continue;

            if (slime.NextAi <= now)
            {
                slime.NextAi = now + TimeSpan.FromSeconds(0.5);
                TickAi((uid, slime));
            }

            if (slime.NextLife <= now)
            {
                slime.NextLife = now + TimeSpan.FromSeconds(2);
                TickLife((uid, slime));
            }
        }
    }

    private void OnMapInit(Entity<FishSlimeComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        ent.Comp.NextLife = now + TimeSpan.FromSeconds(2);
        ent.Comp.NextAi = now + TimeSpan.FromSeconds(0.5);
        NameSlime(ent);
        Dirty(ent);
    }

    private void OnExamine(Entity<FishSlimeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var slime = ent.Comp;
        args.PushMarkup(Loc.GetString("fish-slime-examine",
            ("nutrition", (int) slime.Nutrition),
            ("max", (int) slime.MaxNutrition),
            ("grown", slime.AmountGrown),
            ("need", FishSlimeMutation.EvolutionThreshold),
            ("mutation", slime.MutationChance),
            ("cores", slime.Cores),
            ("power", slime.PowerLevel),
            ("adult", slime.IsAdult),
            ("rabid", slime.Rabid),
            ("docile", slime.Docile),
            ("trained", slime.Trained)));
    }

    private void OnVerbs(Entity<FishSlimeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || args.User != ent.Owner || !_mobState.IsAlive(ent))
            return;

        if (ent.Comp.AmountGrown < FishSlimeMutation.EvolutionThreshold)
            return;

        var evolve = !ent.Comp.IsAdult;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(evolve ? "fish-slime-verb-evolve" : "fish-slime-verb-reproduce"),
            Act = () =>
            {
                if (evolve)
                    TryEvolve(ent);
                else
                    TryReproduce(ent);
            },
        });
    }

    private void OnInteractHand(Entity<FishSlimeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !_mobState.IsAlive(ent))
            return;

        // Погладить: 4%, как attack_hand с INTENT_HELP.
        if (_random.Prob(0.04f))
            Discipline(ent, positive: true);

        if (args.User == ent.Owner)
            TryFeedNearby(ent);

        args.Handled = true;
    }

    private void OnAttacked(Entity<FishSlimeComponent> ent, ref AttackedEvent args)
    {
        ent.Comp.Attacked = Math.Min(50, ent.Comp.Attacked + 10);
        if (ent.Comp.FeedTarget != null && _random.Prob(0.4f))
        {
            StopFeed(ent, silent: false);
            Discipline(ent, positive: false);
        }
        else if (_random.Prob(0.25f))
        {
            Discipline(ent, positive: false);
        }

        if (ent.Comp.Trained && _random.Prob(0.2f))
        {
            ent.Comp.Trained = false;
            _popup.PopupEntity(Loc.GetString("fish-slime-spooked"), ent);
        }

        Dirty(ent);
    }

    private void OnInteractUsing(Entity<FishSlimeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_mobState.IsAlive(ent))
            return;

        var proto = MetaData(args.Used).EntityPrototype?.ID;
        if (proto == null || !proto.StartsWith("SheetPlasma"))
            return;

        args.Handled = true;
        if (!_stack.TryUse(args.Used, 5))
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-plasma-need-five"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("fish-slime-plasma-fed"), ent, args.User);
        if (ent.Comp.Discipline > 0 && !ent.Comp.Trained)
        {
            ent.Comp.Trained = true;
            _popup.PopupEntity(Loc.GetString("fish-slime-trained"), ent, args.User);
        }
        else
        {
            Discipline(ent, positive: true);
        }

        Dirty(ent);
    }

    private void OnMobState(Entity<FishSlimeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !ent.Comp.IsAdult || ent.Comp.DeathHandled)
            return;

        // Взрослый не оставляет труп: откатывается в малыша и рожает бешеного.
        ent.Comp.DeathHandled = true;
        StopFeed(ent, silent: true);
        var coords = _transform.GetMapCoordinates(ent);
        var rabid = SpawnSlime(ent.Comp.ColorId, adult: false, coords);
        if (TryComp<FishSlimeComponent>(rabid, out var rabidSlime))
        {
            rabidSlime.Rabid = true;
            Dirty(rabid, rabidSlime);
        }

        var baby = SpawnSlime(ent.Comp.ColorId, adult: false, coords);
        CopyLineage(ent.Comp, baby);
        TransferActor(ent.Owner, baby);
        QueueDel(ent.Owner);
    }

    private void OnMoveSpeed(Entity<FishSlimeComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var temp = ent.Comp.AmbientTemperature;
        var mod = 1f;
        if (temp >= 330.23f)
            mod = 1.6f;
        else if (temp <= Atmospherics.T0C - 40f)
            mod = 0.05f;

        if (ent.Comp.SlowMultiplier > 1f)
            mod /= ent.Comp.SlowMultiplier;

        args.ModifySpeed(mod, mod);
    }

    private void OnTemperature(Entity<FishSlimeComponent> ent, ref TemperatureChangedEvent args)
    {
        var wasFrozen = ent.Comp.AmbientTemperature <= Atmospherics.T0C - 40f;
        ent.Comp.AmbientTemperature = args.CurrentTemperature;
        var frozen = args.CurrentTemperature <= Atmospherics.T0C - 40f;
        if (frozen && !wasFrozen)
            _popup.PopupEntity(Loc.GetString("fish-slime-frozen"), ent, ent);
        else if (!frozen && wasFrozen)
        {
            _status.TryRemoveStatusEffect(ent, SharedStunSystem.StunId);
            _popup.PopupEntity(Loc.GetString("fish-slime-thawed"), ent, ent);
        }

        if (frozen)
            RefreshFrozenParalyze(ent);

        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        Dirty(ent);
    }

    private void OnEmp(Entity<FishSlimeComponent> ent, ref EmpPulseEvent args)
    {
        ent.Comp.PowerLevel = 0;
        Dirty(ent);
    }

    private void OnDamageModify(Entity<FishSlimeComponent> ent, ref DamageModifyEvent args)
    {
        if (!args.Damage.DamageDict.TryGetValue("Heat", out var heat) || heat <= 0)
            return;

        // Огонь и burn-снаряды лечат, а не жгут.
        args.Damage.DamageDict["Heat"] = FixedPoint2.Zero;
        var blunt = args.Damage.DamageDict.GetValueOrDefault("Blunt");
        args.Damage.DamageDict["Blunt"] = blunt - heat;
    }

    private void OnWaterReaction(Entity<FishSlimeComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || args.Reagent.ID != "Water")
            return;

        var volume = args.ReagentQuantity.Quantity.Float();
        if (volume <= 0f)
            return;

        // Fish-edit: как SS220 — rand(10,15) * объём реагента.
        var damage = _random.Next(10, 16) * volume;
        Hurt(ent, "Blunt", FixedPoint2.New(damage));

        if (_random.Prob(0.25f))
            _popup.PopupEntity(Loc.GetString("fish-slime-hurt-by-water"), ent, ent);
    }

    private void TickLife(Entity<FishSlimeComponent> ent)
    {
        if (!_mobState.IsAlive(ent))
            return;

        var slime = ent.Comp;
        UpdateReagentSlow(ent);

        if (slime.AmbientTemperature <= Atmospherics.T0C - 40f)
            RefreshFrozenParalyze(ent);

        if (slime.AmbientTemperature <= Atmospherics.T0C - 50f)
            Hurt(ent, "Blunt", Math.Clamp((int) ((Atmospherics.T0C - 50f - slime.AmbientTemperature) / 10f), 1, 20));

        if (TryComp<FlammableComponent>(ent, out var flame) && flame.OnFire)
            Heal(ent, "Blunt", 2);

        TickFeed(ent);
        TickHunger(ent);

        if (slime.Attacked > 0)
            slime.Attacked--;

        if (slime.Discipline > 0 && !slime.Trained && _random.Prob(0.1f))
            slime.Discipline--;

        if (slime.Rabid && slime.Discipline >= 5 && _random.Prob(0.6f))
            slime.Rabid = false;

        if (_random.Prob(0.3f))
            Heal(ent, "Blunt", 1);

        if (_random.Prob(0.02f) && _mobState.IsAlive(ent))
            Say(ent, Loc.GetString(PickSpeech(slime, _random.Next(SpeechKeys.Length))));

        Dirty(ent);
    }

    private void TickHunger(Entity<FishSlimeComponent> ent)
    {
        var slime = ent.Comp;
        if (slime.Docile)
        {
            slime.Nutrition = 700f;
            return;
        }

        if (_random.Prob(0.15f))
            slime.Nutrition = Math.Max(0f, slime.Nutrition - (1 + (slime.IsAdult ? 1 : 0)));

        if (slime.Nutrition <= 0f && _random.Prob(0.75f))
            Hurt(ent, "Blunt", _random.Next(0, 6));

        if (slime.Nutrition >= slime.GrowNutrition && slime.AmountGrown < FishSlimeMutation.EvolutionThreshold)
        {
            slime.Nutrition -= 20f;
            slime.AmountGrown++;
        }

        if (slime.AmountGrown >= FishSlimeMutation.EvolutionThreshold
            && slime.FeedTarget == null
            && !HasComp<ActorComponent>(ent))
        {
            if (slime.IsAdult)
                TryReproduce(ent);
            else
                TryEvolve(ent);
        }
    }

    private void TickFeed(Entity<FishSlimeComponent> ent)
    {
        var target = ent.Comp.FeedTarget;
        if (target == null)
            return;

        if (!StillThere(target.Value) || !_mobState.IsAlive(target.Value))
        {
            if (target != null && StillThere(target.Value) && HasComp<ActorComponent>(target.Value) && _random.Prob(0.85f))
                ent.Comp.Rabid = true;
            StopFeed(ent, silent: false);
            return;
        }

        _transform.SetMapCoordinates(ent.Owner, _transform.GetMapCoordinates(target.Value));
        Hurt(target.Value, "Cellular", _random.Next(2, 5));
        Hurt(target.Value, "Poison", _random.Next(1, 3));
        Heal(ent, "Blunt", 3);
        AddNutrition(ent, _random.Next(7, 16));
    }

    private void TickAi(Entity<FishSlimeComponent> ent)
    {
        if (HasComp<ActorComponent>(ent) || !_mobState.IsAlive(ent))
            return;

        if (ent.Comp.AmbientTemperature <= Atmospherics.T0C - 40f)
            return;

        if (ent.Comp.FeedTarget != null)
        {
            if (StillThere(ent.Comp.FeedTarget.Value))
                _transform.SetMapCoordinates(ent.Owner, _transform.GetMapCoordinates(ent.Comp.FeedTarget.Value));
            return;
        }

        if (ent.Comp.Docile)
            return;

        var hungry = ent.Comp.Nutrition < ent.Comp.HungerNutrition;
        var starving = ent.Comp.Nutrition < ent.Comp.StarveNutrition;
        var desperate = starving || ent.Comp.Rabid || ent.Comp.Attacked > 0;
        if (!hungry && !desperate)
            return;

        EntityUid? pick = null;
        foreach (var uid in _lookup.GetEntitiesInRange(ent.Owner, 7f))
        {
            if (!IsEdible(ent, uid) || IsVictimAlreadyLatched(uid, ent.Owner))
                continue;

            var monkey = HasComp<MonkeyAccentComponent>(uid);
            if (!monkey && !desperate)
                continue;

            pick = uid;
            if (monkey)
                break;
        }

        if (pick == null)
        {
            if (hungry && _random.Prob(0.5f))
                Nudge(ent.Owner, _random.NextAngle().ToVec() * 0.8f);
            return;
        }

        if (InRange(ent.Owner, pick.Value, 1.4f))
            StartFeed(ent, pick.Value);
        else
            Nudge(ent.Owner, DirectionTo(ent.Owner, pick.Value) * 0.9f);
    }

    public bool TryEvolve(Entity<FishSlimeComponent> ent)
    {
        if (ent.Comp.DeathHandled || !_mobState.IsAlive(ent) || ent.Comp.IsAdult)
            return false;

        if (ent.Comp.AmountGrown < FishSlimeMutation.EvolutionThreshold)
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-not-ready"), ent, ent);
            return false;
        }

        var adult = SpawnSlime(ent.Comp.ColorId, adult: true, _transform.GetMapCoordinates(ent));
        CopyLineage(ent.Comp, adult);
        if (TryComp<FishSlimeComponent>(adult, out var grown))
        {
            grown.AmountGrown = 0;
            grown.Nutrition = Math.Min(grown.MaxNutrition, ent.Comp.Nutrition);
            Dirty(adult, grown);
        }

        TransferActor(ent.Owner, adult);
        ent.Comp.DeathHandled = true;
        QueueDel(ent.Owner);
        return true;
    }

    public bool TryReproduce(Entity<FishSlimeComponent> ent)
    {
        if (ent.Comp.DeathHandled || !_mobState.IsAlive(ent) || !ent.Comp.IsAdult)
            return false;

        if (ent.Comp.AmountGrown < FishSlimeMutation.EvolutionThreshold)
        {
            _popup.PopupEntity(Loc.GetString("fish-slime-not-ready"), ent, ent);
            return false;
        }

        StopFeed(ent, silent: true);
        var coords = _transform.GetMapCoordinates(ent);
        var babies = new EntityUid[4];
        for (var i = 0; i < 4; i++)
        {
            var color = FishSlimeMutation.RollChild(
                ent.Comp.ColorId,
                ent.Comp.MutationChance,
                _random.Next(100),
                _random.Next(4));
            babies[i] = SpawnSlime(color, adult: false, coords);
            if (!TryComp<FishSlimeComponent>(babies[i], out var baby))
                continue;

            baby.MutationChance = FishSlimeMutation.ClampChance(ent.Comp.MutationChance + _random.Next(-5, 6));
            baby.PowerLevel = ent.Comp.PowerLevel / 4;
            if (HasComp<ActorComponent>(ent))
                baby.Nutrition = Math.Min(baby.MaxNutrition, ent.Comp.Nutrition * 0.9f);
            Dirty(babies[i], baby);
            if (i != 0)
                Nudge(babies[i], _random.NextAngle().ToVec() * 0.6f);
        }

        TransferActor(ent.Owner, babies[_random.Next(babies.Length)]);
        ent.Comp.DeathHandled = true;
        QueueDel(ent.Owner);
        return true;
    }

    public void AddNutrition(Entity<FishSlimeComponent> ent, float amount)
    {
        var slime = ent.Comp;
        slime.Nutrition = Math.Min(slime.MaxNutrition, slime.Nutrition + amount);
        if (slime.Nutrition >= slime.GrowNutrition && slime.PowerLevel < 10 && _random.Prob(0.3f - slime.PowerLevel * 0.02f))
            slime.PowerLevel++;
        else if (slime.Nutrition >= slime.HungerNutrition + 100f && slime.PowerLevel < 5 && _random.Prob(0.25f))
            slime.PowerLevel++;
    }

    public void Discipline(Entity<FishSlimeComponent> ent, bool positive)
    {
        if (!_mobState.IsAlive(ent))
            return;

        ent.Comp.Discipline++;
        StopFeed(ent, silent: true);
        if (!positive && ent.Comp.Trained)
            Say(ent, Loc.GetString("fish-slime-sorry"));
    }

    private void UpdateReagentSlow(Entity<FishSlimeComponent> ent)
    {
        var morphine = _solutions.GetTotalPrototypeQuantity(ent.Owner, "Morphine") > FixedPoint2.Zero;
        var frost = _solutions.GetTotalPrototypeQuantity(ent.Owner, "FrostOil") > FixedPoint2.Zero;
        var slow = 1f;
        if (morphine)
            slow *= 2f;
        if (frost)
            slow *= 5f;

        if (Math.Abs(ent.Comp.SlowMultiplier - slow) > 0.01f)
        {
            ent.Comp.SlowMultiplier = slow;
            _movement.RefreshMovementSpeedModifiers(ent.Owner);
        }
    }

    private void TryFeedNearby(Entity<FishSlimeComponent> ent)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(ent.Owner, 1.5f))
        {
            if (!CanFeed(ent, uid))
                continue;

            StartFeed(ent, uid);
            return;
        }

        _popup.PopupEntity(Loc.GetString("fish-slime-no-food"), ent, ent);
    }

    private bool IsEdible(Entity<FishSlimeComponent> ent, EntityUid target)
    {
        if (target == ent.Owner || ent.Comp.Docile || !_mobState.IsAlive(ent) || !_mobState.IsAlive(target))
            return false;

        return !HasComp<FishSlimeComponent>(target);
    }

    private bool CanFeed(Entity<FishSlimeComponent> ent, EntityUid target)
    {
        return IsEdible(ent, target)
               && InRange(ent.Owner, target, 1.5f)
               && !IsVictimAlreadyLatched(target, ent.Owner);
    }

    // Fish-edit: на одну жертву — один слайм (как SS220).
    private bool IsVictimAlreadyLatched(EntityUid victim, EntityUid? exceptSlime = null)
    {
        var query = EntityQueryEnumerator<FishSlimeComponent>();
        while (query.MoveNext(out var uid, out var slime))
        {
            if (exceptSlime != null && uid == exceptSlime)
                continue;

            if (slime.FeedTarget == victim)
                return true;
        }

        return false;
    }

    private void StartFeed(Entity<FishSlimeComponent> ent, EntityUid target)
    {
        if (IsVictimAlreadyLatched(target, ent.Owner))
            return;

        ent.Comp.FeedTarget = target;
        _popup.PopupEntity(Loc.GetString("fish-slime-latched", ("target", target)), ent);
        Dirty(ent);
    }

    private void StopFeed(Entity<FishSlimeComponent> ent, bool silent)
    {
        if (ent.Comp.FeedTarget == null)
            return;

        ent.Comp.FeedTarget = null;
        if (!silent)
            _popup.PopupEntity(Loc.GetString("fish-slime-let-go"), ent);
    }

    private void CopyLineage(FishSlimeComponent from, EntityUid to)
    {
        if (!TryComp<FishSlimeComponent>(to, out var slime))
            return;

        slime.MutationChance = from.MutationChance;
        slime.Cores = from.Cores;
        slime.PowerLevel = from.PowerLevel;
        slime.Discipline = from.Discipline;
        slime.Trained = from.Trained;
        slime.MutatorUsed = from.MutatorUsed;
        slime.Docile = from.Docile;
        slime.AmountGrown = 0;
        Dirty(to, slime);
    }

    private void TransferActor(EntityUid from, EntityUid to)
    {
        if (!_mind.TryGetMind(from, out var mindId, out var mind))
            return;

        _mind.TransferTo(mindId, to, mind: mind);
    }

    public EntityUid SpawnSlime(string color, bool adult, MapCoordinates coords)
    {
        var proto = FishSlimeMutation.EntityId(color, adult);
        var uid = Spawn(proto, coords);
        if (TryComp<FishSlimeComponent>(uid, out var slime))
        {
            slime.ColorId = color;
            slime.IsAdult = adult;
            NameSlime((uid, slime));
            Dirty(uid, slime);
        }

        return uid;
    }

    private void NameSlime(Entity<FishSlimeComponent> ent)
    {
        _meta.SetEntityName(ent, Loc.GetString("fish-slime-name",
            ("color", Loc.GetString($"fish-slime-color-{ent.Comp.ColorId}")),
            ("stage", Loc.GetString(ent.Comp.IsAdult ? "fish-slime-stage-adult" : "fish-slime-stage-baby")),
            ("number", _random.Next(1, 1000))));
    }

    private void Say(EntityUid uid, string message)
    {
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, ChatTransmitRange.Normal);
    }

    private static string PickSpeech(FishSlimeComponent slime, int idleIndex)
    {
        if (slime.Rabid || slime.Attacked > 0)
            return "fish-slime-speech-angry";
        if (slime.Nutrition < slime.StarveNutrition)
            return "fish-slime-speech-starving";
        if (slime.Nutrition < slime.HungerNutrition)
            return "fish-slime-speech-hungry";
        if (slime.Docile)
            return "fish-slime-speech-purr";
        if (slime.AmbientTemperature < Atmospherics.T0C)
            return "fish-slime-speech-cold";
        if (slime.PowerLevel > 5)
            return "fish-slime-speech-zap";
        return SpeechKeys[idleIndex % SpeechKeys.Length];
    }

    // Fish-edit: паралич держится, пока температура не выше T0C−40.
    private void RefreshFrozenParalyze(Entity<FishSlimeComponent> ent)
    {
        if (!_mobState.IsAlive(ent))
            return;

        _stun.TryUpdateParalyzeDuration(ent, FrozenParalyzeDuration);
    }

    private void Hurt(EntityUid uid, string type, FixedPoint2 amount)
    {
        if (amount == 0 || !StillThere(uid))
            return;

        var damage = new DamageSpecifier { DamageDict = { [type] = amount } };
        _damage.TryChangeDamage(uid, damage, interruptsDoAfters: false);
    }

    private void Heal(EntityUid uid, string type, FixedPoint2 amount)
    {
        Hurt(uid, type, -amount);
    }

    private bool InRange(EntityUid a, EntityUid b, float range)
    {
        if (!StillThere(a) || !StillThere(b))
            return false;

        return (_transform.GetWorldPosition(a) - _transform.GetWorldPosition(b)).Length() <= range;
    }

    private Vector2 DirectionTo(EntityUid from, EntityUid to)
    {
        var delta = _transform.GetWorldPosition(to) - _transform.GetWorldPosition(from);
        return delta.LengthSquared() < 0.0001f ? Vector2.Zero : delta.Normalized();
    }

    private void Nudge(EntityUid uid, Vector2 delta)
    {
        if (!StillThere(uid))
            return;

        var xform = Transform(uid);
        _transform.SetWorldPosition((uid, xform), _transform.GetWorldPosition(uid) + delta);
    }

    private bool StillThere(EntityUid uid)
    {
        return uid.IsValid() && !TerminatingOrDeleted(uid) && !Deleted(uid);
    }
}

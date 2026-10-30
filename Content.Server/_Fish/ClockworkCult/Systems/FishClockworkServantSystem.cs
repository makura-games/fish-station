using Content.Server._Fish.ClockworkCult;
using Content.Server._Fish.ClockworkCult.GameRule;
using Content.Server.Chat.Managers;
using Content.Shared._Fish.ClockworkCult.Components;
using Content.Shared._Fish.ClockworkCult.Events;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.CollectiveMind;
using Content.Shared.Bible.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.ClockworkCult.Systems;

/// <summary>
/// Добавление/удаление слуг Ратвара и проверки eligibility.
/// </summary>
public sealed partial class FishClockworkServantSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FishClockworkPowerSystem _power = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    private static readonly ProtoId<NpcFactionPrototype> ClockworkFaction = "FishClockworkCult";
    private static readonly ProtoId<NpcFactionPrototype> NanoTrasenFaction = "NanoTrasen";
    private static readonly EntProtoId DefaultMindRoleId = "FishMindRoleClockworkServant";
    private static readonly EntProtoId DefaultStartingSlab = "FishClockworkSlab";
    private static readonly SoundSpecifier DefaultGreetingSound =
        new SoundPathSpecifier("/Audio/_Fish/ClockworkCult/ambience/antag/clockcultalr.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishClockworkServantComponent, ComponentShutdown>(OnServantShutdown);
    }

    public bool CanBecomeServant(EntityUid uid, bool forConversion = true, bool quiet = false)
    {
        if (HasComp<FishClockworkServantComponent>(uid))
            return false;

        if (HasComp<BloodCultistComponent>(uid))
            return false;

        if (forConversion && HasComp<MindShieldComponent>(uid))
            return false;

        // Стартовые слуги не могут быть chaplain (как Captain/Chaplain restricted на tg).
        if (!forConversion && HasComp<BibleUserComponent>(uid))
            return false;

        if (!_mind.TryGetMind(uid, out _, out _))
            return false;

        return true;
    }

    public bool TryAddServant(EntityUid uid, bool silent = false, bool giveSlab = true)
    {
        if (!CanBecomeServant(uid, forConversion: true, quiet: silent))
            return false;

        DoAddServant(uid, silent, giveSlab, isConversion: true);
        return true;
    }

    public bool TryAddStartingServant(EntityUid uid)
    {
        if (!CanBecomeServant(uid, forConversion: false, quiet: true))
            return false;

        // Стартовая экипировка antag уже выдаёт слэб — не дублируем.
        DoAddServant(uid, silent: false, giveSlab: false, isConversion: false);
        return true;
    }

    private void DoAddServant(EntityUid uid, bool silent, bool giveSlab, bool isConversion)
    {
        var servantComp = EnsureComp<FishClockworkServantComponent>(uid);

        var mindRole = DefaultMindRoleId;
        var startingSlab = DefaultStartingSlab;
        var greetingSound = DefaultGreetingSound;
        var collectiveMind = servantComp.CollectiveMind;

        if (TryGetRule(out var rule))
        {
            mindRole = rule.Comp.MindRolePrototype;
            startingSlab = rule.Comp.StartingSlab;
            greetingSound = rule.Comp.GreetingSound;
            rule.Comp.Servants.Add(uid);
        }

        var collective = EnsureComp<CollectiveMindComponent>(uid);
        if (!collective.Minds.Contains(collectiveMind))
            collective.Minds.Add(collectiveMind);

        _faction.RemoveFaction(uid, NanoTrasenFaction, false);
        _faction.AddFaction(uid, ClockworkFaction);

        if (_mind.TryGetMind(uid, out var mindId, out _) &&
            !_roles.MindHasRole<FishClockworkServantRoleComponent>(mindId))
        {
            _roles.MindAddRole(mindId, mindRole);
        }

        // Script unlock: 50kW ИЛИ конверсия слуги (на tg — convert at least one).
        if (isConversion)
            _power.UnlockScript();

        // Application unlock: 100kW ИЛИ convert a servant.
        if (isConversion)
            _power.UnlockApplication();

        if (giveSlab)
            Spawn(startingSlab, Transform(uid).Coordinates);

        if (!silent && _mind.TryGetMind(uid, out _, out var mind) &&
            mind.UserId != null &&
            _player.TryGetSessionById(mind.UserId.Value, out var session))
        {
            _audio.PlayGlobal(
                greetingSound,
                Filter.Empty().AddPlayer(session),
                false);

            _chatManager.DispatchServerMessage(session, Loc.GetString("fish-clockwork-role-greeting"));
        }

        _power.HierophantAnnounce(Loc.GetString("fish-clockwork-servant-joined", ("name", Name(uid))));
        RaiseLocalEvent(new FishClockworkServantChangedEvent(uid, added: true));
    }

    public bool TryRemoveServant(EntityUid uid, bool silent = false)
    {
        if (!HasComp<FishClockworkServantComponent>(uid))
            return false;

        var collectiveMind = Comp<FishClockworkServantComponent>(uid).CollectiveMind;

        if (_mind.TryGetMind(uid, out var mindId, out _))
            _roles.MindRemoveRole<FishClockworkServantRoleComponent>(mindId);

        RemComp<FishClockworkServantComponent>(uid);
        RemCompDeferred<FishClockworkArmamentsComponent>(uid);
        RemCompDeferred<FishEminenceComponent>(uid);

        if (TryComp<CollectiveMindComponent>(uid, out var collective))
        {
            collective.Minds.Remove(collectiveMind);
            if (collective.Minds.Count == 0)
                RemCompDeferred<CollectiveMindComponent>(uid);
        }

        _faction.RemoveFaction(uid, ClockworkFaction, false);
        _faction.AddFaction(uid, NanoTrasenFaction);

        if (TryGetRule(out var rule))
            rule.Comp.Servants.Remove(uid);

        if (!silent)
            _power.HierophantAnnounce(Loc.GetString("fish-clockwork-servant-left", ("name", Name(uid))));

        RaiseLocalEvent(new FishClockworkServantChangedEvent(uid, added: false));
        return true;
    }

    private void OnServantShutdown(Entity<FishClockworkServantComponent> ent, ref ComponentShutdown args)
    {
        if (TryGetRule(out var rule))
            rule.Comp.Servants.Remove(ent.Owner);
    }

    public bool IsServant(EntityUid uid)
    {
        return HasComp<FishClockworkServantComponent>(uid);
    }

    public int CountServants()
    {
        return TryGetRule(out var rule) ? rule.Comp.Servants.Count : 0;
    }

    private bool TryGetRule(out Entity<FishClockworkCultRuleComponent> rule)
    {
        rule = default;
        var query = EntityQueryEnumerator<FishClockworkCultRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            rule = (uid, comp);
            return true;
        }

        return false;
    }
}

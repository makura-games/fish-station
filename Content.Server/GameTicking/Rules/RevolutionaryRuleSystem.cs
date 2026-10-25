using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Revolutionary;
using Content.Server.Revolutionary.Components;
using Content.Server.Roles;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Database;
using Content.Shared.Flash;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Roles.Components;
using Content.Shared.Stunnable;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Cuffs.Components;
using Robust.Shared.Player;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.RoundEnd;
using Content.Shared.Popups;
using Content.Server._Fish.ObrCall; // Fish edit - автоматический вызов ОБР Гамма после победы революции
using Content.Shared._Fish.ObrCall; // Fish edit - прототип отряда ОБР

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Where all the main stuff for Revolutionaries happens (Assigning Head Revs, Command on station, and checking for the game to end.)
/// </summary>
public sealed partial class RevolutionaryRuleSystem : GameRuleSystem<RevolutionaryRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private EuiManager _euiMan = default!;
    [Dependency] private IAdminLogManager _adminLogManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private AdminVerbSystem _adminVerbSystem = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!; // Fish edit - код Гамма после потери командования
    [Dependency] private ChatSystem _chat = default!; // Fish edit - объявление о переходе под командование ДСО
    [Dependency] private ObrCallSystem _obr = default!; // Fish edit - автоматический вызов ОБР Гамма

    //Used in OnPostFlash, no reference to the rule component is available
    public readonly ProtoId<NpcFactionPrototype> RevolutionaryNpcFaction = "Revolutionary";
    public readonly ProtoId<NpcFactionPrototype> RevPrototypeId = "Rev";
    // Fish edit start - отряд, который вызывается после победы революции
    private static readonly ProtoId<ObrTeamPrototype> GammaTeamId = "ObrGamma";
    private const string GammaAlertLevel = "gamma";
    // Fish edit end
    private Dictionary<EntityUid, TimeSpan> _scheduledSmites = new();



    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CommandStaffComponent, MobStateChangedEvent>(OnCommandMobStateChanged);

        SubscribeLocalEvent<HeadRevolutionaryComponent, AfterFlashedEvent>(OnPostFlash);
        SubscribeLocalEvent<HeadRevolutionaryComponent, MobStateChangedEvent>(OnHeadRevMobStateChanged);

        SubscribeLocalEvent<RevolutionaryRoleComponent, GetBriefingEvent>(OnGetBriefing);

    }

    protected override void Started(EntityUid uid, RevolutionaryRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        component.CommandCheck = _timing.CurTime + component.TimerWait;
    }

    protected override void ActiveTick(EntityUid uid, RevolutionaryRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        // Check for command loss or if a banned player needs to be smited.
        if (component.CommandCheck <= _timing.CurTime)
        {
            component.CommandCheck = _timing.CurTime + component.TimerWait;

            // Fish edit start - смерть всех глав завершает раунд (Эпсилон), а закованные живые главы вызывают ОБР Гамма
            if (CheckAllCommandDead())
            {
                // Все главы мертвы: как раньше — код Эпсилон и завершение раунда.
                var stations = _stationSystem.GetStations();
                foreach (var station in stations)
                {
                    _alertLevel.SetLevel(station, "epsilon", playSound: true, announce: true, force: true);
                }

                _roundEnd.EndRound();
                GameTicker.EndGameRule(uid, gameRule);
            }
            else if (!component.GammaCalled && CheckAllCommandDetained())
            {
                // Все живые главы закованы: вызываем большой отряд ОБР Гамма.
                var deployed = _obr.IsTeamActive(GammaTeamId) ||
                               _obr.TryCallTeamAutomatically(
                                   GammaTeamId,
                                   Loc.GetString("rev-ert-gamma-mission"));

                if (deployed)
                {
                    component.GammaCalled = true;

                    if (_obr.TryGetMainStation(out var station))
                    {
                        // Код Гамма должен быть выставлен, если он ещё не активен.
                        if (_alertLevel.GetLevel(station) != GammaAlertLevel)
                        {
                            _alertLevel.SetLevel(
                                station,
                                GammaAlertLevel,
                                playSound: false,
                                announce: false,
                                force: true);
                        }

                        _chat.DispatchStationAnnouncement(
                            station,
                            Loc.GetString("rev-ert-gamma-announcement"),
                            sender: Loc.GetString("chat-manager-sender-announcement"),
                            playDefaultSound: true,
                            colorOverride: Color.Gold);
                    }
                }
            }
            // Fish edit end

            // Execute scheduled smites for banned players
            if (_scheduledSmites.Count > 0)
            {
                var currentTime = _timing.CurTime;
                foreach (var entity in _scheduledSmites.Keys.ToList().Where(entity => _scheduledSmites[entity] <= currentTime))
                {
                    if (Exists(entity))
                    {
                        _adminVerbSystem.RandomDeath(entity);
                    }
                    _scheduledSmites.Remove(entity);
                }
            }
        }
    }

    protected override void AppendRoundEndText(EntityUid uid,
        RevolutionaryRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var revsLost = CheckRevsLose();
        var commandLost = CheckCommandLose();
        // This is (revsLost, commandsLost) concatted together
        // (moony wrote this comment idk what it means)
        var index = (commandLost ? 1 : 0) | (revsLost ? 2 : 0);
        args.AddLine(Loc.GetString(Outcomes[index]));

        var sessionData = _antag.GetAntagIdentifiers(uid).ToList();
        args.AddLine(Loc.GetString("rev-headrev-count", ("initialCount", sessionData.Count)));
        foreach (var (mind, data, name) in sessionData)
        {
            _role.MindHasRole<RevolutionaryRoleComponent>(mind, out var role);
            var count = CompOrNull<RevolutionaryRoleComponent>(role)?.ConvertedCount ?? 0;

            args.AddLine(Loc.GetString("rev-headrev-name-user",
                ("name", name),
                ("username", data.UserName),
                ("count", count)));

            // TODO: someone suggested listing all alive? revs maybe implement at some point
        }
        args.AddLine("");
    }

    private void OnGetBriefing(EntityUid uid, RevolutionaryRoleComponent comp, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;
        var head = HasComp<HeadRevolutionaryComponent>(ent);
        args.Append(Loc.GetString(head ? "head-rev-briefing" : "rev-briefing"));
    }

    /// <summary>
    /// Called when a Head Rev uses a flash in melee to convert somebody else.
    /// </summary>
    private void OnPostFlash(EntityUid uid, HeadRevolutionaryComponent comp, ref AfterFlashedEvent ev)
    {
        if (uid != ev.User || !ev.Melee)
            return;

        var alwaysConvertible = HasComp<AlwaysRevolutionaryConvertibleComponent>(ev.Target);

        if (!_mind.TryGetMind(ev.Target, out var mindId, out var mind) && !alwaysConvertible)
            return;

        if (HasComp<RevolutionaryComponent>(ev.Target) ||
            HasComp<MindShieldComponent>(ev.Target) ||
            HasComp<CommandStaffComponent>(ev.Target) ||
            !HasComp<HumanoidProfileComponent>(ev.Target) &&
            !alwaysConvertible ||
            !_mobState.IsAlive(ev.Target) ||
            HasComp<ZombieComponent>(ev.Target) ||
            !HasComp<RevolutionaryConverterComponent>(ev.Used))
        {
            return;
        }

        _npcFaction.AddFaction(ev.Target, RevolutionaryNpcFaction);
        var revComp = EnsureComp<RevolutionaryComponent>(ev.Target);

        if (ev.User != null)
        {
            _adminLogManager.Add(LogType.Mind,
                LogImpact.Medium,
                $"{ToPrettyString(ev.User.Value)} converted {ToPrettyString(ev.Target)} into a Revolutionary");

            if (_mind.TryGetMind(ev.User.Value, out var revMindId, out _))
            {
                if (_role.MindHasRole<RevolutionaryRoleComponent>(revMindId, out var role))
                {
                    role.Value.Comp2.ConvertedCount++;
                    Dirty(role.Value.Owner, role.Value.Comp2);
                }
            }
        }

        if (mindId == default || !_role.MindHasRole<RevolutionaryRoleComponent>(mindId))
        {
            _role.MindAddRole(mindId, "MindRoleRevolutionary");
        }

        if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
            _antag.SendBriefing(session, Loc.GetString("rev-role-greeting"), Color.Red, revComp.RevStartSound);
    }

    //TODO: Enemies of the revolution
    private void OnCommandMobStateChanged(EntityUid uid, CommandStaffComponent comp, MobStateChangedEvent ev)
    {
        if (ev.NewMobState != MobState.Dead && ev.NewMobState != MobState.Invalid)
            return;

        // Fish edit start - не ждём плановой проверки, чтобы условия победы проверялись сразу после гибели командования
        var rules = QueryAllRules();
        while (rules.MoveNext(out var ruleUid, out var ruleComp, out var ruleData))
        {
            if (GameTicker.IsGameRuleActive(ruleUid, ruleData))
                ruleComp.CommandCheck = _timing.CurTime;
        }
        // Fish edit end

        CheckCommandLose();
    }

    /// <summary>
    /// Checks if all of command is dead and if so will remove all sec and command jobs if there were any left.
    /// </summary>
    private bool CheckCommandLose()
    {
        var commandList = new List<EntityUid>();

        var heads = AllEntityQuery<CommandStaffComponent>();
        while (heads.MoveNext(out var id, out _))
        {
            commandList.Add(id);
        }

        return IsGroupDetainedOrDead(commandList, true, true, true);
    }

    // Fish edit start - новые условия победы революции: Эпсилон при смерти всех глав, Гамма при их заковывании
    /// <summary>
    /// Все главы мертвы (или их нет). Мёртвыми считаются также главы без <see cref="MobStateComponent"/>.
    /// </summary>
    private bool CheckAllCommandDead()
    {
        var heads = AllEntityQuery<CommandStaffComponent>();
        while (heads.MoveNext(out var id, out _))
        {
            if (!TryComp<MobStateComponent>(id, out var state))
                continue;

            if (state.CurrentState != MobState.Dead && state.CurrentState != MobState.Invalid)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Все живые главы закованы в наручники/стяжки. Мёртвые главы не учитываются.
    /// </summary>
    private bool CheckAllCommandDetained()
    {
        var heads = AllEntityQuery<CommandStaffComponent>();
        while (heads.MoveNext(out var id, out _))
        {
            // Нет MobState — считаем главу мёртвым и пропускаем.
            if (!TryComp<MobStateComponent>(id, out var state))
                continue;

            if (state.CurrentState == MobState.Dead || state.CurrentState == MobState.Invalid)
                continue;

            if (!TryComp<CuffableComponent>(id, out var cuffed) || cuffed.CuffedHandCount <= 0)
                return false;
        }

        return true;
    }
    // Fish edit end

    private void OnHeadRevMobStateChanged(EntityUid uid, HeadRevolutionaryComponent comp, MobStateChangedEvent ev)
    {
        if (ev.NewMobState == MobState.Dead || ev.NewMobState == MobState.Invalid)
            CheckRevsLose();
    }

    /// <summary>
    /// Checks if all the Head Revs are dead and if so will deconvert all regular revs.
    /// </summary>
    private bool CheckRevsLose()
    {
        var stunTime = TimeSpan.FromSeconds(4);
        var headRevList = new List<EntityUid>();

        var headRevs = AllEntityQuery<HeadRevolutionaryComponent, MobStateComponent>();
        while (headRevs.MoveNext(out var uid, out _, out _))
        {
            headRevList.Add(uid);
        }

        // If no Head Revs are alive all normal Revs will lose their Rev status and rejoin Nanotrasen
        // Cuffing Head Revs is not enough - they must be killed.
        if (IsGroupDetainedOrDead(headRevList, false, false, false))
        {
            var rev = AllEntityQuery<RevolutionaryComponent, MindContainerComponent>();
            while (rev.MoveNext(out var uid, out _, out var mc))
            {
                if (HasComp<HeadRevolutionaryComponent>(uid))
                    continue;

                _npcFaction.RemoveFaction(uid, RevolutionaryNpcFaction);
                _stun.TryUpdateParalyzeDuration(uid, stunTime);
                RemCompDeferred<RevolutionaryComponent>(uid);
                _popup.PopupEntity(Loc.GetString("rev-break-control", ("name", Identity.Entity(uid, EntityManager))), uid);
                _adminLogManager.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(uid)} was deconverted due to all Head Revolutionaries dying.");

                if (!_mind.TryGetMind(uid, out var mindId, out var mind, mc))
                    continue;

                // remove their antag role
                _role.MindRemoveRole<RevolutionaryRoleComponent>(mindId);

                // make it very obvious to the rev they've been deconverted since
                // they may not see the popup due to antag and/or new player tunnel vision
                if (_player.TryGetSessionById(mind.UserId, out var session))
                    _euiMan.OpenEui(new DeconvertedEui(), session);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Will take a group of entities and check if these entities are alive, dead or cuffed.
    /// </summary>
    /// <param name="list">The list of the entities</param>
    /// <param name="checkOffStation">Bool for if you want to check if someone is in space and consider them missing in action. (Won't check when emergency shuttle arrives just in case)</param>
    /// <param name="countCuffed">Bool for if you don't want to count cuffed entities.</param>
    /// <param name="countRevolutionaries">Bool for if you want to count revolutionaries.</param>
    /// <returns></returns>
    private bool IsGroupDetainedOrDead(List<EntityUid> list, bool checkOffStation, bool countCuffed, bool countRevolutionaries)
    {
        var gone = 0;

        foreach (var entity in list)
        {
            if (TryComp<CuffableComponent>(entity, out var cuffed) && cuffed.CuffedHandCount > 0 && countCuffed)
            {
                gone++;
                continue;
            }

            if (TryComp<MobStateComponent>(entity, out var state))
            {
                if (state.CurrentState == MobState.Dead || state.CurrentState == MobState.Invalid)
                {
                    gone++;
                    continue;
                }

                if (checkOffStation && _stationSystem.GetOwningStation(entity) == null && !_emergencyShuttle.EmergencyShuttleArrived)
                {
                    gone++;
                    continue;
                }
            }
            //If they don't have the MobStateComponent they might as well be dead.
            else
            {
                gone++;
                continue;
            }

            if ((HasComp<RevolutionaryComponent>(entity) || HasComp<HeadRevolutionaryComponent>(entity)) && countRevolutionaries)
            {
                gone++;
                continue;
            }
        }

        return gone == list.Count || list.Count == 0;
    }

    private void KillDueToBan(EntityUid target)
    {
        _popup.PopupEntity(Loc.GetString("rev-banned"), target, target, PopupType.LargeCaution);

        var randomDelay = new Random().Next(10, 60); // 10-60 seconds
        var targetTime = _timing.CurTime + TimeSpan.FromSeconds(randomDelay);
        _scheduledSmites[target] = targetTime;
    }

    private static readonly string[] Outcomes =
    {
        // revs survived and heads survived... how
        "rev-reverse-stalemate",
        // revs won and heads died
        "rev-won",
        // revs lost and heads survived
        "rev-lost",
        // revs lost and heads died
        "rev-stalemate"
    };
}

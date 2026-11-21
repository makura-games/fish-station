using Content.Server._Fish.MentalHealth;
using Content.Server.Radiation.Components;
using Content.Shared._Fish.Psychosis;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Content.Server._Fish.Psychosis;

/// <summary>
/// Управляет уровнем психоза и отправляет обновления клиенту.
/// </summary>
public sealed partial class PsychosisSystem : EntitySystem
{
    [Dependency] private MentalHealthConfigSystem _config = default!;

    private const float LowPsychosisDecayPerSecond = 0.5f;
    private const float ModeratePsychosisDecayPerSecond = 0.05f;
    private const float HighPsychosisDecayPerSecond = 0.005f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    /// <summary>
    /// Уменьшает активные источники психоза и рассылает изменения уровня клиентам.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PsychosisComponent>();
        while (query.MoveNext(out var uid, out var psychosis))
        {
            if (TryComp<RadiationReceiverComponent>(uid, out var radiation) &&
                radiation.CurrentRadiation >= 1f)
            {
                AddSource(uid, "Radiation", radiation.CurrentRadiation * 0.05f * frameTime);
            }

            var oldLevel = psychosis.Current;
            var decayPerSecond = GetDecayPerSecond(oldLevel);
            var sources = new string[psychosis.Sources.Count];
            psychosis.Sources.Keys.CopyTo(sources, 0);
            foreach (var source in sources)
            {
                var nextValue = MathF.Max(0f, psychosis.Sources[source] - decayPerSecond * frameTime);
                if (nextValue <= 0f)
                    psychosis.Sources.Remove(source);
                else
                    psychosis.Sources[source] = nextValue;
            }

            var current = 0f;
            foreach (var value in psychosis.Sources.Values)
                current += value;

            psychosis.Current = Math.Clamp(current, 0f, 100f);
            var reachedZero = oldLevel > 0f && psychosis.Current == 0f;

            var oldStage = psychosis.Stage;
            var newStage = PsychosisStages.Get(psychosis.Current);
            if (oldStage != newStage)
            {
                psychosis.Stage = newStage;
                RaiseLocalEvent(uid, new PsychosisStageChangedEvent(uid, oldStage, newStage, psychosis.Current));
            }

            if (oldLevel == psychosis.Current)
                continue;

            Dirty(uid, psychosis);
            if ((int)oldLevel != (int)psychosis.Current || reachedZero)
                RaiseNetworkEvent(new PsychosisUpdateEvent(GetNetEntity(uid), psychosis.Current), Filter.Entities(uid));
        }
    }

    /// <summary>
    /// Добавляет уровень психоза к именованному источнику сущности.
    /// </summary>
    public void AddSource(EntityUid uid, string source, float amount)
    {
        if (amount <= 0f || string.IsNullOrWhiteSpace(source))
            return;

        var psychosis = EnsureComp<PsychosisComponent>(uid);
        psychosis.Sources[source] = Math.Clamp(
            psychosis.Sources.GetValueOrDefault(source) + amount,
            0f,
            100f);
        Dirty(uid, psychosis);
    }

    private static float GetDecayPerSecond(float psychosisLevel)
    {
        return psychosisLevel switch
        {
            < PsychosisStages.UnsettledMin => LowPsychosisDecayPerSecond,
            < PsychosisStages.DeliriousMin => ModeratePsychosisDecayPerSecond,
            _ => HighPsychosisDecayPerSecond,
        };
    }

    /// <summary>
    /// Отправляет клиенту игрока конфигурацию доступных галлюцинаций.
    /// </summary>
    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        SendConfig(ev.Mob);
        SendCurrentLevel(ev.Mob);
    }

    /// <summary>
    /// Отправляет состояние психоза и конфигурацию при подключении к уже существующему персонажу.
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        SendConfig(ev.Entity);
        SendCurrentLevel(ev.Entity);
    }

    private void SendConfig(EntityUid uid)
    {
        RaiseNetworkEvent(new PsychosisConfigEvent(
            _config.HallucinationMobPrototypes,
            _config.HallucinationCorpsePrototypes,
            _config.HallucinationItemPrototypes,
            _config.HallucinationSounds
        ), Filter.Entities(uid));
    }

    private void SendCurrentLevel(EntityUid uid)
    {
        var level = TryComp<PsychosisComponent>(uid, out var psychosis) ? psychosis.Current : 0f;
        RaiseNetworkEvent(new PsychosisUpdateEvent(GetNetEntity(uid), level, true), Filter.Entities(uid));
    }
}

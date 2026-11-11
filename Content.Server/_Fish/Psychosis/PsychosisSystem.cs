using System.Linq;
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
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
            foreach (var source in psychosis.Sources.Keys.ToArray())
            {
                var nextValue = MathF.Max(0f, psychosis.Sources[source] - 0.5f * frameTime);
                if (nextValue <= 0f)
                    psychosis.Sources.Remove(source);
                else
                    psychosis.Sources[source] = nextValue;
            }

            psychosis.Current = Math.Clamp(psychosis.Sources.Values.Sum(), 0f, 100f);
            if (Math.Abs(oldLevel - psychosis.Current) < 0.001f)
                continue;

            Dirty(uid, psychosis);
            if ((int) oldLevel != (int) psychosis.Current)
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

    /// <summary>
    /// Отправляет клиенту игрока конфигурацию доступных галлюцинаций.
    /// </summary>
    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        RaiseNetworkEvent(new PsychosisConfigEvent(
            _config.HallucinationMobPrototypes,
            _config.HallucinationCorpsePrototypes,
            _config.HallucinationItemPrototypes,
            _config.HallucinationSounds
        ), Filter.Entities(ev.Mob));
    }
}
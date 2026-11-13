using Content.Shared._Fish.Psychosis;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Psychosis;

/// <summary>
/// Добавляет запаздывающие повторы уже играющих звуков при высоком психозе.
/// </summary>
public sealed partial class SoundEchoSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float MinPsychosisLevel = 50f;
    private const int EchoCount = 3;
    private const float EchoDelay = 0.22f;
    private const float EchoVolumeStep = -7f;

    private float _currentPsychosisLevel;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
    }

    /// <summary>
    /// Сохраняет текущий уровень психоза локального игрока для звуковых эффектов.
    /// </summary>
    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        _currentPsychosisLevel = ev.Level;
    }

    /// <summary>
    /// Создаёт затухающие повторы запущенных звуков при высоком уровне психоза.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_currentPsychosisLevel < MinPsychosisLevel)
            return;

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (!audio.Started || !audio.Playing || string.IsNullOrWhiteSpace(audio.FileName) ||
                HasComp<EchoSoundComponent>(uid))
                continue;

            // Fish-edit: помечаем исходный звук, чтобы он не порождал эхо каждый кадр.
            EnsureComp<EchoSoundComponent>(uid);

            var fileName = audio.FileName;
            var volume = audio.Volume;
            var mapCoordinates = _transform.GetMapCoordinates(uid);

            for (var i = 1; i <= EchoCount; i++)
            {
                var delay = EchoDelay * i;
                var echoParams = AudioParams.Default.WithVolume(volume + EchoVolumeStep * i);

                Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
                {
                    var coordinates = _transform.ToCoordinates(mapCoordinates);
                    var sound = _audio.ResolveSound(new SoundPathSpecifier(fileName));
                    var echo = _audio.PlayStatic(sound, Filter.Local(), coordinates, false, echoParams);
                    if (echo is { } echoData)
                        EnsureComp<EchoSoundComponent>(echoData.Entity);
                });
            }
        }
    }
}

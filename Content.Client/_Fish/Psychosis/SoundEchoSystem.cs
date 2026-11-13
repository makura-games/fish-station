using Content.Shared._Fish.Psychosis;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Psychosis;

public sealed partial class SoundEchoSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;

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

        if (_currentPsychosisLevel <= 66f)
            return;

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (!audio.Started || !audio.Playing || string.IsNullOrWhiteSpace(audio.FileName) ||
                HasComp<EchoSoundComponent>(uid))
                continue;

            // Fish-edit: отмечаем исходный звук, чтобы он не создавал повторное эхо каждый кадр.
            EnsureComp<EchoSoundComponent>(uid);
            var fileName = audio.FileName;

            for (var i = 1; i <= 3; i++)
            {
                var delay = 0.2f * i;
                var echoParams = AudioParams.Default.WithVolume(-6f * i);
                Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
                {
                    if (!Exists(uid))
                        return;

                    var sound = _audio.ResolveSound(new SoundPathSpecifier(fileName));
                    var echo = _audio.PlayEntity(sound, Filter.Local(), uid, false, echoParams);
                    if (echo is { } echoData)
                        EnsureComp<EchoSoundComponent>(echoData.Entity);
                });
            }
        }
    }
}
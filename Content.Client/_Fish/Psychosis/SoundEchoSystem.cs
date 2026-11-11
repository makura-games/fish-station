using Content.Shared._Fish.Psychosis;
using Content.Shared.Audio;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Fish.Psychosis;

public sealed partial class SoundEchoSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;

    private float _currentPsychosisLevel;
    private bool _creatingEcho;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsychosisUpdateEvent>(OnPsychosisUpdate);
        SubscribeLocalEvent<AudioComponent, ComponentInit>(OnAudioInit);
    }

    /// <summary>
    /// Сохраняет текущий уровень психоза локального игрока для звуковых эффектов.
    /// </summary>
    private void OnPsychosisUpdate(PsychosisUpdateEvent ev)
    {
        _currentPsychosisLevel = ev.Level;
    }

    /// <summary>
    /// Создаёт затухающие повторы звука при высоком уровне психоза.
    /// </summary>
    private void OnAudioInit(Entity<AudioComponent> ent, ref ComponentInit args)
    {
        if (_currentPsychosisLevel <= 66f || _creatingEcho)
            return;

        if (HasComp<EchoSoundComponent>(ent))
            return;

        var uid = ent.Owner;
        var fileName = ent.Comp.FileName;
        for (int i = 1; i <= 3; i++)
        {
            float delay = 0.2f * i;
            var echoParams = AudioParams.Default.WithVolume(-6f * i);
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (!Exists(uid))
                    return;

                _creatingEcho = true;
                try
                {
                    var sound = _audio.ResolveSound(new SoundPathSpecifier(fileName));
                    var echo = _audio.PlayEntity(sound, Filter.Local(), uid, false, echoParams);
                    if (echo is { } echoData)
                        EnsureComp<EchoSoundComponent>(echoData.Entity);
                }
                finally
                {
                    _creatingEcho = false;
                }
            });
        }
    }
}
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Fish.Ipc;

/// <summary>
/// Короткий жужжащий звук на удар и редкие эмоции в крите.
/// Постоянного цикла в крите нет.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishEmitBuzzWhileDamagedComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("FishIpcBuzzes");

    /// <summary>Кулдаун жужжания после получения урона, секунды.</summary>
    [DataField]
    public float BuzzCooldown = 5f;

    /// <summary>
    /// Мелкие порции (крит-brute по центу) не жужжат, иначе звук зацикливается.
    /// </summary>
    [DataField]
    public float DamageBuzzThreshold = 1f;

    [ViewVariables]
    public TimeSpan NextBuzz;

    [ViewVariables]
    public int BuzzCount;

    [DataField]
    public float CritEmoteMinDelay = 30f;

    [DataField]
    public float CritEmoteMaxDelay = 50f;

    /// <summary>Бип буп, Пинг, Тяжело кашлять, Предсмертный хрип.</summary>
    [DataField]
    public List<ProtoId<EmotePrototype>> CritEmotes = new()
    {
        "Buzz-Two",
        "Ping",
        "SevereCough",
        "DefaultDeathgasp",
    };

    [ViewVariables]
    public bool CritEmoteScheduled;

    [ViewVariables]
    public TimeSpan NextCritEmote;

    [ViewVariables]
    public string? LastCritEmote;

    /// <summary>Последний выпавший интервал до эмоции, секунды.</summary>
    [ViewVariables]
    public float LastCritEmoteDelay;
}

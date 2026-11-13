using Robust.Shared.Serialization;

namespace Content.Shared._Fish.Psychosis;

/// <summary>
/// Передаёт клиенту списки прототипов и звуков для галлюцинаций.
/// </summary>
[Serializable, NetSerializable]
public sealed class PsychosisConfigEvent : EntityEventArgs
{
    public List<string> MobPrototypes = new();
    public List<string> CorpsePrototypes = new();
    public List<string> ItemPrototypes = new();
    public List<string> Sounds = new();

    public PsychosisConfigEvent(List<string> mobPrototypes, List<string> corpsePrototypes, List<string> itemPrototypes, List<string> sounds)
    {
        MobPrototypes = mobPrototypes;
        CorpsePrototypes = corpsePrototypes;
        ItemPrototypes = itemPrototypes;
        Sounds = sounds;
    }
}
using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class CorporateLawUiState : BoundUserInterfaceState
{
    public readonly List<LawSection> Sections;
    public readonly bool Connected;

    public CorporateLawUiState(List<LawSection> sections, bool connected = true)
    {
        Sections = sections;
        Connected = connected;
    }
}

[Serializable, NetSerializable]
public sealed class LawSection
{
    public readonly string Title;
    public readonly Color? Color;
    public readonly List<LawEntry> Entries;

    public LawSection(string title, Color? color, List<LawEntry> entries)
    {
        Title = title;
        Color = color;
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class LawEntry
{
    public readonly string? Identifier;
    public readonly string Title;
    public readonly string Description;
    // Fish-start: базовый срок статьи для отображения в КПК СБ
    public readonly int BaseSentence;
    // Fish-end

    public LawEntry(string? identifier, string title, string description, int baseSentence = 0)
    {
        Identifier = identifier;
        Title = title;
        Description = description;
        BaseSentence = baseSentence; // Fish-edit: передаём базовый срок
    }
}

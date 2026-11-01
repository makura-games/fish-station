namespace Content.Shared._Fish.Xenobiology.Slimes;

/// <summary>
/// Таблица и бросок ребёнка из SS220 <c>mutation_table</c> / <c>Reproduce</c>.
/// Слот 0–3, бросок мутации 0–99. Повтор цвета в слотах даёт двойной вес.
/// </summary>
public static class FishSlimeMutation
{
    public const int EvolutionThreshold = 10;
    public const int DefaultMutationChance = 30;
    public const int MaxCores = 5;

    public static readonly string[] Colors =
    [
        "rainbow", "grey", "purple", "metal", "orange", "blue", "darkblue", "darkpurple",
        "yellow", "silver", "pink", "red", "gold", "green", "adamantine", "oil",
        "lightpink", "bluespace", "cerulean", "sepia", "black", "pyrite",
    ];

    private static readonly Dictionary<string, string> Pascal = new()
    {
        ["grey"] = "Grey",
        ["purple"] = "Purple",
        ["metal"] = "Metal",
        ["orange"] = "Orange",
        ["blue"] = "Blue",
        ["darkblue"] = "DarkBlue",
        ["darkpurple"] = "DarkPurple",
        ["yellow"] = "Yellow",
        ["silver"] = "Silver",
        ["pink"] = "Pink",
        ["red"] = "Red",
        ["gold"] = "Gold",
        ["green"] = "Green",
        ["adamantine"] = "Adamantine",
        ["oil"] = "Oil",
        ["lightpink"] = "LightPink",
        ["bluespace"] = "Bluespace",
        ["cerulean"] = "Cerulean",
        ["sepia"] = "Sepia",
        ["black"] = "Black",
        ["pyrite"] = "Pyrite",
        ["rainbow"] = "Rainbow",
    };

    /// <summary>
    /// Четыре слота как в DM. Цвета вне таблицы мутируют сами в себя.
    /// </summary>
    public static readonly Dictionary<string, string[]> Table = new()
    {
        ["grey"] = ["orange", "metal", "blue", "purple"],
        ["purple"] = ["darkpurple", "darkblue", "green", "green"],
        ["metal"] = ["silver", "yellow", "gold", "gold"],
        ["orange"] = ["darkpurple", "yellow", "red", "red"],
        ["blue"] = ["darkblue", "silver", "pink", "pink"],
        ["darkblue"] = ["purple", "blue", "cerulean", "cerulean"],
        ["darkpurple"] = ["purple", "orange", "sepia", "sepia"],
        ["yellow"] = ["metal", "orange", "bluespace", "bluespace"],
        ["silver"] = ["metal", "blue", "pyrite", "pyrite"],
        ["pink"] = ["pink", "pink", "lightpink", "lightpink"],
        ["red"] = ["red", "red", "oil", "oil"],
        ["gold"] = ["gold", "gold", "adamantine", "adamantine"],
        ["green"] = ["green", "green", "black", "black"],
    };

    public static string EntityId(string color, bool adult)
    {
        var name = Pascal.TryGetValue(color, out var pascal) ? pascal : "Grey";
        return adult ? $"FishSlime{name}AdultFish" : $"FishSlime{name}BabyFish";
    }

    public static string ExtractPrototypeId(string color)
    {
        var name = Pascal.TryGetValue(color, out var pascal) ? pascal : "Grey";
        return $"FishSlimeExtract{name}";
    }

    public static string[] Slots(string parent)
    {
        return Table.TryGetValue(parent, out var slots) ? slots : [parent, parent, parent, parent];
    }

    public static string RollChild(string parent, int chancePercent, int mutationRoll, int slot)
    {
        if (chancePercent >= 100)
            return "rainbow";

        var slots = Slots(parent);
        if (mutationRoll < chancePercent)
            return slots[Math.Clamp(slot, 0, 3)];

        return parent;
    }

    public static int ClampChance(int chance)
    {
        return Math.Clamp(chance, 0, 100);
    }
}

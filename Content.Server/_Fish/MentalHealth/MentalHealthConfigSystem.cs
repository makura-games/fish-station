using System.IO;
using System.Linq;
using System.Globalization;
using Content.Shared._Fish.Stress;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Server._Fish.MentalHealth;

/// <summary>
/// Загружает настройки системы психического здоровья.
/// </summary>
public sealed partial class MentalHealthConfigSystem : EntitySystem
{
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private ILogManager _log = default!;

    private ISawmill _sawmill = default!;
    public Dictionary<string, (float blood, float enemies)> StressResists = new();
    public List<string> ForbiddenWords = new();
    public List<string> HallucinationMobPrototypes = new();
    public List<string> HallucinationCorpsePrototypes = new();
    public List<string> HallucinationItemPrototypes = new();
    public List<string> HallucinationSounds = new();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("fish.mental_health");
        LoadConfig();
    }

    private void LoadConfig()
    {
        var path = new ResPath("/Config/_Fish/mental_health.yml");
        if (!_res.TryContentFileRead(path, out var stream))
        {
            _sawmill.Error("Mental health config not found!");
            return;
        }

        using var reader = new StreamReader(stream);
        var yaml = new YamlStream();
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        // Загрузка резистов
        if (root.Children.TryGetValue(new YamlScalarNode("stress_resists"), out var resistsNode))
        {
            var resistsMap = (YamlMappingNode)resistsNode;
            foreach (var entry in resistsMap.Children)
            {
                var roleId = ((YamlScalarNode)entry.Key).Value!;
                var values = (YamlMappingNode)entry.Value;
		        var blood = 0f;
		        var enemies = 0f;
		        if (values.Children.TryGetValue(new YamlScalarNode("blood"), out var bloodNode))
		            blood = float.Parse(((YamlScalarNode)bloodNode).Value!, CultureInfo.InvariantCulture);
		        if (values.Children.TryGetValue(new YamlScalarNode("enemies"), out var enemiesNode))
		            enemies = float.Parse(((YamlScalarNode)enemiesNode).Value!, CultureInfo.InvariantCulture);
		        StressResists[roleId] = (blood, enemies);
		    }
		}

		if (root.Children.TryGetValue(new YamlScalarNode("psychosis"), out var psychosisNode))
		{
		    var psychosisMap = (YamlMappingNode) psychosisNode;
		    HallucinationMobPrototypes = ReadStringList(psychosisMap, "hallucination_mob_prototypes");
		    HallucinationCorpsePrototypes = ReadStringList(psychosisMap, "hallucination_corpse_prototypes");
		    HallucinationItemPrototypes = ReadStringList(psychosisMap, "hallucination_item_prototypes");
		    HallucinationSounds = ReadStringList(psychosisMap, "hallucination_sounds");
		}

		var forbiddenWordsPath = new ResPath("/Config/_Fish/forbidden_words.yml");
		if (!_res.TryContentFileRead(forbiddenWordsPath, out var forbiddenWordsStream))
		{
		    _sawmill.Error("Forbidden words config not found!");
		    return;
		}

		using var forbiddenWordsReader = new StreamReader(forbiddenWordsStream);
		var forbiddenWordsYaml = new YamlStream();
		forbiddenWordsYaml.Load(forbiddenWordsReader);
		var forbiddenWordsRoot = (YamlMappingNode) forbiddenWordsYaml.Documents[0].RootNode;
		if (forbiddenWordsRoot.Children.TryGetValue(new YamlScalarNode("forbidden_words"), out var wordsNode))
		{
		    ForbiddenWords = ((YamlSequenceNode) wordsNode)
		        .Children
		        .Select(node => ((YamlScalarNode) node).Value!)
		        .ToList();
		}
    }

    private static List<string> ReadStringList(YamlMappingNode mapping, string key)
    {
		return mapping.Children.TryGetValue(new YamlScalarNode(key), out var node)
		    ? ((YamlSequenceNode) node).Children.Select(child => ((YamlScalarNode) child).Value!).ToList()
		    : new List<string>();
    }
}
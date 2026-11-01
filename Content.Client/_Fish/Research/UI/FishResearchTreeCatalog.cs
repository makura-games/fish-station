using System.Linq;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Fish.Research.UI;

/// <summary>
/// Cached, localized presentation data shared by the graph and the details pane.
/// </summary>
internal sealed class FishResearchTreeCatalog
{
    public readonly Dictionary<ProtoId<TechnologyPrototype>, FishResearchTechnology> Technologies = new();
    public readonly Dictionary<ProtoId<TechDisciplinePrototype>, TechDisciplinePrototype> Disciplines = new();
    public readonly Dictionary<ProtoId<TechDisciplinePrototype>, Texture> DisciplineIcons = new();

    public FishResearchTreeCatalog(
        IPrototypeManager prototype,
        ILocalizationManager loc,
        SpriteSystem sprite,
        SharedLatheSystem lathe,
        IEnumerable<ProtoId<TechDisciplinePrototype>> supported)
    {
        foreach (var id in supported)
        {
            if (!prototype.TryIndex(id, out var discipline))
                continue;

            Disciplines.Add(id, discipline);
            DisciplineIcons.Add(id, sprite.Frame0(discipline.Icon));
        }

        var recipes = new Dictionary<ProtoId<LatheRecipePrototype>, FishResearchRecipe>();
        foreach (var technology in prototype.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (technology.Hidden || !Disciplines.ContainsKey(technology.Discipline))
                continue;

            var unlocks = new List<FishResearchRecipe>();
            foreach (var recipeId in technology.RecipeUnlocks)
            {
                if (!prototype.TryIndex(recipeId, out var recipe))
                    continue;

                if (!recipes.TryGetValue(recipeId, out var presentation))
                {
                    var name = lathe.GetRecipeName(recipe);
                    if (string.IsNullOrWhiteSpace(name))
                        name = loc.GetString("fish-research-tree-unnamed-recipe");

                    // Fish-edit: пустой ResultReagents не поддерживает First() в описании станка.
                    var description = recipe.Description != null || recipe.Result != null || recipe.ResultReagents?.Count > 0
                        ? lathe.GetRecipeDescription(recipe)
                        : string.Empty;
                    Texture? icon = null;
                    if (recipe.Icon != null)
                        icon = sprite.Frame0(recipe.Icon);
                    else if (recipe.Result is { } result && prototype.TryIndex(result, out var entity))
                        icon = sprite.Frame0(entity);

                    presentation = new FishResearchRecipe(name, description, icon);
                    recipes.Add(recipeId, presentation);
                }

                unlocks.Add(presentation);
            }

            var title = loc.GetString(technology.Name);
            var key = GetDescriptionKey(technology);
            var purpose = loc.TryGetString(key, out var descriptionText)
                ? descriptionText
                : loc.GetString("fish-research-tree-description-fallback");
            Technologies.Add(technology.ID, new FishResearchTechnology(
                technology,
                title,
                purpose,
                loc.GetString("fish-research-tree-node-meta", ("tier", technology.Tier), ("cost", technology.Cost)),
                sprite.Frame0(technology.Icon),
                unlocks,
                string.Join('\n', unlocks.Select(recipe => recipe.Name).Prepend(title))));
        }
    }

    /// <summary>
    /// Normalizes legacy technology names for the fork's description keys.
    /// </summary>
    public static string GetDescriptionKey(TechnologyPrototype technology)
    {
        return $"fish-{technology.Name.ToString().Replace('_', '-').ToLowerInvariant()}-description";
    }
}

internal sealed record FishResearchRecipe(string Name, string Description, Texture? Icon);

internal sealed record FishResearchTechnology(
    TechnologyPrototype Prototype,
    string Name,
    string Description,
    string Metadata,
    Texture Icon,
    IReadOnlyList<FishResearchRecipe> Recipes,
    string SearchText);

internal enum FishResearchNodeState : byte
{
    Locked,
    Available,
    Researched,
}

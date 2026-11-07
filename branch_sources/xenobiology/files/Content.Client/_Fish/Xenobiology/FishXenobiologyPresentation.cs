using System.Globalization;
using System.Numerics;
using Content.Client.Stylesheets.Palette;
using Content.Shared._Fish.Xenobiology;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Fish.Xenobiology;

/// <summary>Shared presentation of specimen identity, status, portraits and bounded measurements.</summary>
internal static class FishXenobiologyPresentation
{
    public static StyleBoxFlat Panel(Color color) => new()
    {
        BackgroundColor = color,
        BorderColor = Palettes.Slate.Element,
        BorderThickness = new Thickness(1),
    };

    public static float Unit(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    public static string Number(float value) => float.IsFinite(value)
        ? value.ToString("0.#", CultureInfo.CurrentCulture) : "?";

    public static string Name(FishXenobiologySpecimen specimen, string breedName, ILocalizationManager loc)
    {
        var name = string.IsNullOrWhiteSpace(specimen.Name) ? breedName : specimen.Name;
        return string.IsNullOrWhiteSpace(specimen.Identifier) || name.Contains(specimen.Identifier, StringComparison.Ordinal)
            ? name
            : loc.GetString("fish-xeno-identity", ("name", name), ("identifier", specimen.Identifier));
    }

    public static bool NeedsAttention(FishXenobiologySpecimen specimen) => specimen.Dead || specimen.Hostile ||
        !specimen.Suspended && (specimen.Hunger < 25 || specimen.Temperature < specimen.MinimumTemperature ||
                              specimen.Temperature > specimen.MaximumTemperature);

    public static string Status(FishXenobiologySpecimen specimen, ILocalizationManager loc) => loc.GetString(
        specimen.Dead ? "fish-xeno-dead" : specimen.Suspended ? "fish-xeno-contained" :
        specimen.Hostile ? "fish-xeno-hostile" : specimen.Temperature < specimen.MinimumTemperature ? "fish-xeno-cold" :
        specimen.Temperature > specimen.MaximumTemperature ? "fish-xeno-hot" : specimen.Hunger < 25 ? "fish-xeno-hungry" :
        specimen.Adult ? "fish-xeno-adult" : "fish-xeno-baby");

    public static Color StatusColor(FishXenobiologySpecimen specimen) => specimen.Dead || specimen.Hostile
        ? Palettes.Red.Text : NeedsAttention(specimen) ? Palettes.Amber.Text : Palettes.Green.Text;

    public static void SetIcon(TextureRect icon, SpriteSystem sprite, FishXenobiologyBreedPrototype breed,
        bool adult = false, bool dead = false)
    {
        var path = adult ? "/Textures/_Fish/Xenobiology/Mobs/slimesAdult.rsi" : "/Textures/_Fish/Xenobiology/Mobs/slimesBaby.rsi";
        icon.Texture = sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath(path), dead ? "dead" : "base"));
        icon.Modulate = breed.Color;
    }

    public static void SetPortrait(EntityPrototypeView portrait, IEntityManager entity,
        FishXenobiologyBreedPrototype breed, FishXenobiologySpecimen specimen)
    {
        portrait.SetPrototype(breed.Mob);
        if (portrait.Entity is not { } preview)
            return;
        var stage = specimen.Adult
            ? specimen.Dead ? FishXenobiologyStage.DeadAdult : FishXenobiologyStage.Adult
            : specimen.Dead ? FishXenobiologyStage.DeadBaby : FishXenobiologyStage.Baby;
        entity.System<SharedAppearanceSystem>().SetData(preview.Owner, FishXenobiologyVisuals.Stage, stage);
        entity.System<SpriteSystem>().ForceUpdate(preview.Owner);
    }

    public static Vector2 Available(Vector2 viewport) => Vector2.Max(Vector2.One, viewport - new Vector2(24));
}

// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Silicons.Borgs;

public sealed partial class BorgSwitchableTypeSystem
{
    /// <summary>
    /// Обновляет иконку и имя борга в робоконсоли под выбранный тип.
    /// RSI берётся из <see cref="BorgTypePrototype.SpriteRsiPath"/> (Mk2-холст), а если его нет —
    /// из спекификатора транспондера, с фолбэком на общий Mobs/Silicon/chassis.rsi.
    /// </summary>
    private void UpdateTransponder(Entity<BorgSwitchableTypeComponent> ent, ProtoId<BorgTypePrototype> borgType)
    {
        if (!TryComp(ent, out BorgTransponderComponent? transponder))
            return;

        var prototype = Prototypes.Index(borgType);

        var transponderRsi = prototype.SpriteRsiPath
            ?? (transponder.Sprite as SpriteSpecifier.Rsi)?.RsiPath
            ?? new ResPath("Mobs/Silicon/chassis.rsi");

        _borgSystem.SetTransponderSprite(
            (ent.Owner, transponder),
            new SpriteSpecifier.Rsi(transponderRsi, prototype.SpriteBodyState));

        _borgSystem.SetTransponderName(
            (ent.Owner, transponder),
            Loc.GetString(prototype.GetLocKey("transponder")));
    }
}

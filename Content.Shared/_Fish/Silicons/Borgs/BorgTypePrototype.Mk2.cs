// Необходимо поскольку этот partial класс расширяет ванильный тип и должен сохранять исходный namespace несмотря на расположение файла в _Fish
#pragma warning disable IDE0130 // Namespace does not match folder structure
using System.Numerics;
using System.Text;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared.Silicons.Borgs;

public sealed partial class BorgTypePrototype : IInheritingPrototype
{
    // Поддержка parent: для borgType (Mk2-варианты поверх существующих специализаций).
    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<BorgTypePrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// <summary>
    /// Не показывать тип в меню выбора специализации.
    /// Используется для Mk2-вариантов, скрытых от ручного выбора.
    /// </summary>
    [DataField]
    public bool HideInMenu;

    // FTL-ключи borgType (name/desc/transponder) генерируются из ID прототипа.
    // Ванильные ID — lowercase (generic), Fish-префиксы — CamelCase (FishGenericMk2),
    // поэтому ID переводится в kebab-case: ключи вида borg-type-fish-generic-mk2-name.
    /// <summary>
    /// Строит FTL-ключ <c>borg-type-{kebab-case ID}-{suffix}</c> для name/desc/transponder.
    /// </summary>
    public string GetLocKey(string suffix) => $"borg-type-{ToKebabId(ID)}-{suffix}";

    /// <summary>
    /// CamelCase → kebab-case для построения FTL-ключей из ID прототипа:
    /// <c>FishGenericMk2</c> → <c>fish-generic-mk2</c>, <c>generic</c> → <c>generic</c>.
    /// </summary>
    public static string ToKebabId(string id)
    {
        var builder = new StringBuilder(id.Length + 8);

        foreach (var ch in id)
        {
            if (char.IsUpper(ch))
            {
                if (builder.Length > 0)
                    builder.Append('-');

                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    // Отдельный холст спрайта для типов, чей RSI не совпадает с RSI chassis сущности.
    // Нужен Mk2: у них корпус рисуется из _Lust/Mobs/Silicon/chassis.rsi, а живая сущность
    // осталась на Mobs/Silicon/chassis.rsi от родительского chassis. Без этого поля
    // смена borgType меняла бы только state и оставляла старый холст (а у security
    // state sec_mk2* на vanilla-RSI вообще отсутствует).
    /// <summary>
    /// RSI, из которого берутся все слои спрайта. <c>null</c> — RSI не трогаем.
    /// </summary>
    [DataField]
    public ResPath? SpriteRsiPath;

    /// <summary>
    /// Смещение слоёв спрайта в тайлах. <c>null</c> — смещение не трогаем.
    /// Для _Lust (холст 32x64 против vanilla 32x32) нужно 0,0.4 (= 0,0.5 × SpriteScale),
    /// иначе борг висит ниже: корневой Offset задаётся в мировых метрах и корневым
    /// Scale не масштабируется, а тело на холсте смещено на пол-холста.
    /// Вешается на корень SpriteComponent, а не на отдельные слои: так runtime-слои
    /// предметов (in-hand, надетая экипировка) наследуют сдвиг и едут вместе с телом.
    /// </summary>
    [DataField]
    public Vector2? SpriteOffset;

    /// <summary>
    /// Единый масштаб спрайта типа (по X и Y). <c>null</c> — масштаб сбрасывается в 1.
    /// Mk2: 0.8 — _Lust-холст крупнее vanilla-пропорций, тело визуально больше тайла.
    /// Масштабируется всё дерево слоёв, включая in-hand предметы и надетую одежду;
    /// физика/хитбоксы при этом не меняются.
    /// </summary>
    [DataField]
    public float? SpriteScale;

    /// <summary>
    /// Состояние спрайта тела в критическом/мёртвом состоянии. <c>null</c> — тело не меняется.
    /// У Mk2 используется wreck-кадр из _Lust RSI (например, robot_wreck).
    /// Переключение выполняет клиентский BorgSystem при смене MobStateVisuals.State;
    /// при возврате в Alive тело восстанавливается в <see cref="SpriteBodyState"/>.
    /// </summary>
    [DataField]
    public string? SpriteWreckState;

    /// <summary>
    /// Состояние спрайта тела во время отдыха (механика BorgRest). <c>null</c> — тело не меняется.
    /// У Mk2 используется rest-кадр из _Lust RSI (например, robot_rest).
    /// Переключение выполняет клиентский BorgSystem при BorgRestVisuals.Resting;
    /// при выходе из отдыха тело восстанавливается предыдущими обработчиками визуала.
    /// </summary>
    [DataField]
    public string? SpriteRestingState;
}

using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Fish.ClockworkCult.Prototypes;

/// <summary>
/// Прототип писания Clockwork Cult (уровень, стоимость, время чтения, эффект).
/// </summary>
[Prototype]
public sealed partial class FishClockworkScripturePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField]
    public LocId Description = string.Empty;

    [DataField]
    public LocId? UsageTip;

    [DataField]
    public FishClockworkScriptureTier Tier = FishClockworkScriptureTier.Driver;

    [DataField]
    public float PowerCost = 0f;

    /// <summary>
    /// Время чтения писания в секундах.
    /// </summary>
    [DataField]
    public float ChannelTime = 1f;

    [DataField]
    public int InvokersRequired = 1;

    [DataField]
    public bool Whispered;

    [DataField]
    public EntProtoId? SpawnPrototype;

    /// <summary>
    /// Идентификатор логики эффекта на сервере (kindle, abscond, spawn, armaments...).
    /// </summary>
    [DataField(required: true)]
    public string ScriptureId = string.Empty;

    [DataField]
    public int SortPriority;

    [DataField]
    public bool OnePerTile;
}


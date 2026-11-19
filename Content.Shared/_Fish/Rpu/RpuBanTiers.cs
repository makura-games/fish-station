namespace Content.Shared._Fish.Rpu;

/// <summary>
/// Порог автобана для игроков с наигранным временем меньше MaxHours часов.
/// </summary>
[DataDefinition]
public sealed partial class RpuBanTier
{
    /// <summary>Верхняя граница наигранных на должностях часов (не включительно).</summary>
    [DataField(required: true)]
    public float MaxHours;

    /// <summary>Карма, при которой выдаётся автобан.</summary>
    [DataField(required: true)]
    public float Threshold;

    /// <summary>Tier применяется только при rpu.ban_veterans=1 (для ветеранов).</summary>
    [DataField]
    public bool VeteransOnly;
}

/// <summary>
/// Усиление системы по количеству банов игрока.
/// </summary>
[DataDefinition]
public sealed partial class RpuBanHistoryTier
{
    /// <summary>Минимальное количество банов, включительно.</summary>
    [DataField(required: true)]
    public int MinBans;

    /// <summary>Множитель к штрафу.</summary>
    [DataField(required: true)]
    public float Multiplier;

    /// <summary>Насколько понижается порог автобана (по модулю кармы).</summary>
    [DataField]
    public float ThresholdShift;
}

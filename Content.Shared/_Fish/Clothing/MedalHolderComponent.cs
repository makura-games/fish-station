namespace Content.Shared._Fish.Clothing;

/// <summary>
/// Помечает одежду (форму), в которую можно вставить медаль через ItemSlots.
/// Слот по умолчанию — <see cref="SlotId"/>.
/// </summary>
[RegisterComponent]
public sealed partial class MedalHolderComponent : Component
{
    public const string DefaultSlotId = "medal";

    /// <summary>
    /// Id слота в <c>ItemSlotsComponent</c>, куда вставляется медаль.
    /// </summary>
    [DataField]
    public string SlotId = DefaultSlotId;
}

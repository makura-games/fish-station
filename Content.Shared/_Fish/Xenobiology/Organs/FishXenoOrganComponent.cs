using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Xenobiology.Organs;

public enum FishXenoOrganQuality : byte
{
    Unknown,
    Pristine,
    Damaged,
}

/// <summary>
/// Готовый ксено-орган после обработки слаймом.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishXenoOrganComponent : Component
{
    [DataField, AutoNetworkedField]
    public FishXenoOrganQuality Quality = FishXenoOrganQuality.Pristine;

    [DataField, AutoNetworkedField]
    public bool Identified;

    [DataField, AutoNetworkedField]
    public bool Analyzed;
}

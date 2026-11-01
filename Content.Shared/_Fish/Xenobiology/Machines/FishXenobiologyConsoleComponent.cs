using Robust.Shared.GameStates;
using Content.Shared._Fish.Xenobiology.Extracts;

namespace Content.Shared._Fish.Xenobiology.Machines;

/// <summary>
/// Консоль ксенобиологии: хранение слаймов, счётчик кубов, зелье для кормления.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishXenobiologyConsoleComponent : Component
{
    public const int MaxStoredSlimes = 6;
    public const int RecycleGrindRequired = 5;

    [DataField]
    public float Radius = 7f;

    [DataField, AutoNetworkedField]
    public List<EntityUid> StoredSlimes = new();

    [DataField, AutoNetworkedField]
    public int MonkeyCubeCount;

    [DataField, AutoNetworkedField]
    public FishSlimePotionKind? LoadedPotionKind;

    [DataField, AutoNetworkedField]
    public int RecycleProgress;

    [DataField, AutoNetworkedField]
    public EntityUid? LinkedRecycler;
}

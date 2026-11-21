using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Fish.Paper;

/// <summary>
/// Маркер степлера: бумага в активной руке может скрепляться с другой при степлере в другой руке.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishStaplerComponent : Component
{
    /// <summary>
    /// Звук успешного скрепления страниц.
    /// </summary>
    [DataField]
    public SoundSpecifier StapleSound = new SoundPathSpecifier("/Audio/_Sunrise/Items/Handling/paper_use.ogg");
}

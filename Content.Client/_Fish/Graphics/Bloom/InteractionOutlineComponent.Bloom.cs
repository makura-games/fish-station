using Robust.Client.Graphics;

namespace Content.Client.Interactable.Components;

public sealed partial class InteractionOutlineComponent
{
    /// <summary>
    /// Identifies the outline postshader, which preserves the source sprite for bloom sampling.
    /// </summary>
    internal ShaderInstance? FishOutlineShader => _shader;
}

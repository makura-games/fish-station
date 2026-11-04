using Robust.Client.Graphics;

namespace Content.Client.Interactable.Components;

public sealed partial class InteractionOutlineComponent
{
    /// <summary>
    /// Post-shader ID for the interaction outline, which preserves the source sprite for bloom sampling.
    /// </summary>
    internal const string FishOutlinePostShaderId = Content.Client.Graphics.ContentPostShaderIds.InteractionOutline;
}

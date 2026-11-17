using Robust.Client.Graphics;

namespace Content.Client.Interactable.Components;

public sealed partial class InteractionOutlineComponent
{
    // Позволяет термалам отличать обычную обводку от маскирующих постшейдеров.
    /// <summary>Post-shader ID for the selection outline without depending on other visual effects.</summary>
    internal const string FishVisionOutlinePostShaderId = Content.Client.Graphics.ContentPostShaderIds.InteractionOutline;
}

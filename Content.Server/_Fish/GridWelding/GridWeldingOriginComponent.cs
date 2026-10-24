namespace Content.Server._Fish.GridWelding;

/// <summary>
/// Remembers the original grid identity when the engine splits a structure into fragments.
/// </summary>
[RegisterComponent, Access(typeof(GridWeldingSystem))]
public sealed partial class GridWeldingOriginComponent : Component
{
    /// <summary>The original grid, retained when its fragments are welded back onto it.</summary>
    public EntityUid OriginalGrid;
}

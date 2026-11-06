using System;

namespace Content.Shared.Procedural;

public sealed partial class SalvageDifficultyPrototype
{
    /// <summary>
    /// How long an expedition of this difficulty lasts. Zero falls back to the salvage.expedition_duration CVar.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.Zero;
}

using Robust.Shared.Configuration;

namespace Content.Shared._Fish.Graphics;

/// <summary>Optional visual noise; does not alter device visibility or power.</summary>
[CVarDefs]
public static class FishVisionCVars
{
    /// <summary>Enables grain and scanline noise in night vision goggles.</summary>
    public static readonly CVarDef<bool> NightVisionNoise =
        CVarDef.Create("fish.night_vision_noise", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}

namespace Content.Shared._Fish.Melee;

/// <summary>
/// Marks a melee weapon whose throw-on-hit effect is resisted by active magnetic boots.
/// When the target has active magboots (MovedByPressureComponent.Enabled == false),
/// the throw is skipped but damage and other effects still apply.
/// </summary>
[RegisterComponent]
public sealed partial class ThrowOnHitMagbootsImmuneComponent : Component
{
}

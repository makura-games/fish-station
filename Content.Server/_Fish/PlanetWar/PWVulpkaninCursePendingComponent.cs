// Fish-edit - Vulpkanin PlanetWar Meme Mode
namespace Content.Server._Fish.PlanetWar;

[RegisterComponent]
public sealed partial class PWVulpkaninCursePendingComponent : Component
{
    [DataField("timer")]
    public float Timer = 5.0f;
}

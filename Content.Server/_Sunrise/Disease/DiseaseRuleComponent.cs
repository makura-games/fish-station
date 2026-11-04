// Fish-edit: To remove - функционал разумной болезни
namespace Content.Server._Sunrise.Disease;

[RegisterComponent, Access(typeof(DiseaseRoleSystem))]
public sealed partial class DiseaseRuleComponent : Component
{
    public List<(EntityUid, string)> DiseasesMinds = new();
}

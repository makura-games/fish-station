// Fish edit - Vulpkanin PlanetWar Meme Mode
using Content.Server.GameTicking.Rules;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared._Sunrise.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Server._Fish.PlanetWar;

public sealed partial class PlanetWarVulpkaninRuleSystem : GameRuleSystem<PlanetWarVulpkaninRuleComponent>
{
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private SunriseHumanoidProfileSystem _sunriseProfile = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Check if the Vulpkanin meme game rule is currently active
        var ruleQuery = EntityQueryEnumerator<PlanetWarVulpkaninRuleComponent>();
        bool ruleActive = ruleQuery.MoveNext(out _, out _);

        var query = EntityQueryEnumerator<PWVulpkaninCursePendingComponent>();
        while (query.MoveNext(out var uid, out var curse))
        {
            // If the meme rule is not active in this round, clean up the component and do nothing
            if (!ruleActive)
            {
                RemCompDeferred<PWVulpkaninCursePendingComponent>(uid);
                continue;
            }

            curse.Timer -= frameTime;
            if (curse.Timer > 0f)
                continue;

            // Timer expired: remove component and morph species to Vulpkanin
            RemCompDeferred<PWVulpkaninCursePendingComponent>(uid);

            if (!HasComp<HumanoidProfileComponent>(uid))
                continue;

            // Generate random Vulpkanin appearance (ears, snout, tail, markings, colors)
            var profile = HumanoidCharacterProfile.RandomWithSpecies("Vulpkanin");

            // Load profile while preserving the entity's custom role name
            var originalName = Name(uid);
            _humanoidProfile.ApplyProfileTo(uid, profile);
            _visualBody.ApplyProfileTo(uid, profile);
            _sunriseProfile.ApplyProfileTo(uid, profile);
            _metaData.SetEntityName(uid, originalName);
        }
    }
}

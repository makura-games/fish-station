using Content.Server.GameTicking.Events;
using Content.Server.GameTicking;
using Content.Server._Fish.MentalHealth;
using Content.Shared._Fish.Stress;
using Content.Shared._Fish.Neuro;
using Content.Shared._Fish.Psychosis;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.Server._Fish.MentalHealth;

public sealed class MentalHealthSpawnSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        var uid = ev.Mob;
        if (!TryComp<MindContainerComponent>(uid, out var mindContainer) ||
            mindContainer.Mind == null ||
            !HasComp<HumanoidProfileComponent>(uid))
            return;

        EnsureComp<StressComponent>(uid);
        EnsureComp<NeuroComponent>(uid);
        EnsureComp<PsychosisComponent>(uid);
    }
}
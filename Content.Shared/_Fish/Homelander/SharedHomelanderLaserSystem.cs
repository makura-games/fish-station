using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._Fish.Homelander;

public sealed partial class SharedHomelanderLaserSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HomelanderComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<HomelanderComponent> entity, ref ShotAttemptedEvent args)
    {
        if (!args.Used.Comp.UseKey && !args.Used.Comp.BurstActivated &&
            args.Used.Owner != entity.Comp.Laser && IsActive(entity))
            args.Cancel();
    }

    public bool IsActive(EntityUid? uid)
    {
        return uid is { } entity && HasComp<HomelanderComponent>(entity) &&
            _appearance.TryGetData<bool>(entity, HomelanderVisuals.LaserActive, out var active) && active;
    }
}

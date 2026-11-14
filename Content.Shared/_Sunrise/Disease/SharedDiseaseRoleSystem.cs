// Fish-edit: To remove - функционал разумной болезни
// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/space-station-14/blob/master/CLA.txt
using Content.Shared.Hands;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Pulling.Events;

namespace Content.Shared._Sunrise.Disease;

public abstract class SharedDiseaseRoleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Fish-start - Запрет взаимодействия болезни с физическим миром
        SubscribeLocalEvent<DiseaseRoleComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<DiseaseRoleComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<DiseaseRoleComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<DiseaseRoleComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<DiseaseRoleComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<DiseaseRoleComponent, StartPullAttemptEvent>(OnStartPullAttempt);
        SubscribeLocalEvent<DiseaseRoleComponent, PullAttemptEvent>(OnPullAttempt);
        // Fish-end
    }

    // Fish-start - Обработчики блокировки взаимодействия для сущности болезни
    private void OnInteractionAttempt(Entity<DiseaseRoleComponent> ent, ref InteractionAttemptEvent args)
    {
        if (args.Target != null && args.Target != ent.Owner)
            args.Cancelled = true;
    }

    private void OnUseAttempt(Entity<DiseaseRoleComponent> ent, ref UseAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnAttackAttempt(Entity<DiseaseRoleComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnDropAttempt(Entity<DiseaseRoleComponent> ent, ref DropAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnPickupAttempt(Entity<DiseaseRoleComponent> ent, ref PickupAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnStartPullAttempt(Entity<DiseaseRoleComponent> ent, ref StartPullAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnPullAttempt(Entity<DiseaseRoleComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }
    // Fish-end

    public float GetDiseaseProtectionCoefficient(EntityUid uid)
    {
        if (HasComp<DiseaseImmuneComponent>(uid))
            return 0f;

        if (TryComp<DiseaseTempImmuneComponent>(uid, out var tempImmune))
        {
            return Math.Clamp(1f - tempImmune.Prob, 0f, 1f);
        }

        return 1f;
    }

    public void OnInfect(InfectEvent ev, float probability = 0)
    {
        if (ev.Handled)
            return;

        if (!TryComp<DiseaseRoleComponent>(ev.Performer, out var comp)) return;
        if (HasComp<DiseaseImmuneComponent>(ev.Target)) return;
        if (HasComp<SickComponent>(ev.Target)) return;

        // Fish-start - Полная защита блокирует заражение, частичная учитывается шансом
        var protection = GetDiseaseProtectionCoefficient(ev.Target);
        if (protection <= 0.0f)
        {
            return;
        }
        // Fish-end

        ev.Handled = true;

        var comps = AddComp<SickComponent>(ev.Target);
        comps.owner = ev.Performer;
        Dirty(ev.Target, comps);

        comp.Infected.Add(ev.Target);
        Dirty(ev.Performer, comp);
    }
}

using Content.Shared._Fish.MentalHealth;
using Content.Shared._Fish.Neuro;
using Content.Shared._Fish.Stress;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Server._Fish.MentalHealth;

public sealed class MentalHealthExposureSystem : EntitySystem
{
    private static readonly TimeSpan DoseInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ToleranceRecoveryInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AddictionRecoveryInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan WithdrawalInterval = TimeSpan.FromSeconds(10);

    [Dependency] private readonly IGameTiming _timing = default!;

    public float RecordDose(EntityUid uid, string substanceId, float addictionPerDose)
    {
        var component = EnsureComp<MentalHealthExposureComponent>(uid);
        var now = _timing.CurTime;
        if (!component.Substances.TryGetValue(substanceId, out var state))
        {
            state = new MentalHealthSubstanceState
            {
                LastDose = now,
                LastToleranceDecay = now,
                LastAddictionDecay = now,
                Doses = 1,
                TolerancePenalty = -0.1f,
                Addiction = addictionPerDose,
            };
            component.Substances.Add(substanceId, state);
        }
        else
        {
            RecoverTolerance(state, now);
            if (now - state.LastDose >= DoseInterval)
            {
                state.Doses++;
                state.Addiction = Math.Clamp(state.Addiction + addictionPerDose, 0f, 100f);
                state.TolerancePenalty = state.Doses == 2 && state.TolerancePenalty < 0f
                    ? 0.02f
                    : Math.Min(0.15f, state.TolerancePenalty + 0.02f);
                state.LastDose = now;
                state.LastAddictionDecay = now;
                state.NextWithdrawal = TimeSpan.Zero;
            }
        }

        return 1f - state.TolerancePenalty;
    }

    public void ReduceAddiction(EntityUid uid, float amount)
    {
        if (amount <= 0f || !TryComp<MentalHealthExposureComponent>(uid, out var exposure))
            return;

        foreach (var state in exposure.Substances.Values)
        {
            state.Addiction = Math.Max(0f, state.Addiction - amount);
            if (state.Addiction <= 50f)
                state.NextWithdrawal = TimeSpan.Zero;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MentalHealthExposureComponent, StressComponent>();
        while (query.MoveNext(out var uid, out var exposure, out var stress))
        {
            var withdrawal = false;
            foreach (var state in exposure.Substances.Values)
            {
                RecoverTolerance(state, now);
                RecoverAddiction(state, now);

                if (state.Addiction <= 50f || now - state.LastDose < DoseInterval)
                {
                    state.NextWithdrawal = TimeSpan.Zero;
                    continue;
                }

                if (state.NextWithdrawal == TimeSpan.Zero)
                {
                    state.NextWithdrawal = now + WithdrawalInterval;
                    continue;
                }

                if (now < state.NextWithdrawal)
                    continue;

                state.NextWithdrawal = now + WithdrawalInterval;
                withdrawal = true;
            }

            if (!withdrawal)
                continue;

            stress.Current = Math.Clamp(stress.Current + 2f, 0f, MentalHealthConstants.StressMax);
            Dirty(uid, stress);

            if (!TryComp<NeuroComponent>(uid, out var neuro))
                continue;

            neuro.Current = Math.Max(0f, neuro.Current - 1f);
            Dirty(uid, neuro);
        }
    }

    private static void RecoverTolerance(MentalHealthSubstanceState state, TimeSpan now)
    {
        var intervals = (int) ((now - state.LastToleranceDecay).Ticks / ToleranceRecoveryInterval.Ticks);
        if (intervals <= 0)
            return;

        state.TolerancePenalty = state.TolerancePenalty < 0f
            ? Math.Min(0f, state.TolerancePenalty + intervals * 0.01f)
            : Math.Max(0f, state.TolerancePenalty - intervals * 0.01f);
        state.LastToleranceDecay += TimeSpan.FromTicks(ToleranceRecoveryInterval.Ticks * intervals);
    }

    private static void RecoverAddiction(MentalHealthSubstanceState state, TimeSpan now)
    {
        if (now - state.LastDose < DoseInterval)
            return;

        var intervals = (int) ((now - state.LastAddictionDecay).Ticks / AddictionRecoveryInterval.Ticks);
        if (intervals <= 0)
            return;

        state.Addiction = Math.Max(0f, state.Addiction - intervals);
        state.LastAddictionDecay += TimeSpan.FromTicks(AddictionRecoveryInterval.Ticks * intervals);
    }
}

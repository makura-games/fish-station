# Integration

## Component

`FishNpcBrain` on hostile AI package entities (melee → Assault, complex/ranged humanoid → Assault or Defense by role).

If a mob already inherits Assault via `MobNonHumanHostileBase` → Complex, a later `SimpleRanged` parent will **not** replace the component. Put an explicit `FishNpcBrain` Defense override on the ranged role base (`MobPirateBaseT2Ranged`, etc.).

## HTN

On startup: if `DisableHtn` and `HTNComponent` present → `SetHTNEnabled(false)`.
On shutdown of brain: optionally re-enable HTN (usually delete entity).

## Expedition

`ExpeditionMobSystem` still owns vision boost + proximity wake.
Brain only runs while `ActiveNPCComponent` is present.

Vision boost applies to HTN blackboard **and** `FishNpcBrain` radii (`ApplyBrainVisionBoost`, flag `ExpeditionVisionBoosted`).

## Tests

- Prototype has `FishNpcBrain` with expected doctrine.
- Mode enum / component spawn smoke.
- Optional: empty gun transitions toward Reload when inventory has mag (heavier).

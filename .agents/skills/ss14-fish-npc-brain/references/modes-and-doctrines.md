# Modes and doctrines

## Modes

| Mode | Intent |
|------|--------|
| Idle | No target, no last-seen |
| Hunt | Move toward best hostile / last-seen approach |
| EngageRanged | Shoot while managing distance |
| EngageMelee | CQC commit |
| Reload | Insert mag / ballistic ammo from inventory |
| Search | Sweep last-seen after LOS loss |
| Flee | Break contact when critical |

## Assault

- Boost Hunt / EngageMelee scores.
- `MeleeCommit` ≈ `AssaultMeleeCommitDistance`.
- Prefer closing PreferRangedDistance down over time.
- Flee only near `FleeHealthFraction` (low).

## Defense

- Boost EngageRanged / Reload / Flee.
- Hold between `DefenseHoldMin` and `DefenseHoldMax`.
- If enemy closer than HoldMin → backstep / kite (steer away).
- Search longer; Flee earlier (treat health threshold higher in scoring).

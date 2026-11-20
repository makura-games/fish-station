---
name: ss14-fish-npc-brain
description: Fish hostile NPC brain — mode utility + Assault/Defense combat doctrines instead of dumb HTN chase. Use when adding or changing FishNpcBrain, expedition hostiles, pirate/soviet AI packages, CQC/hold-distance behaviour, or migrating mobs off vanilla HTN.
---

# Fish NPC Brain (Assault / Defense)

## Goal

Hostile Fish NPCs should fight like trained operators: modes, memory, doctrine — not HTN twitch-chase.

## Reading order

1. This file (mental model + rules).
2. `references/modes-and-doctrines.md` — mode scores and Assault vs Defense.
3. `references/integration.md` — wiring, HTN disable, tests.
4. Upstream context only if needed: `ss14-npc-system-core` (steering/path/factions reuse).

## Source of truth

1. Code under `Content.Shared/_Fish/NPC/` and `Content.Server/_Fish/NPC/`.
2. Hostile AI packages in `Resources/Prototypes/_Sunrise/NPCs/PirateNPC/` (Fish-edit markers).
3. Do not treat vanilla HTN compounds as the brain for Fish hostiles once `FishNpcBrain` is present.

## Mental model

```
ActiveNPC + FishNpcBrain
  → evaluate Mode (hysteresis)
  → Assault | Defense doctrine tweaks scores
  → execute: Steer + Melee/Ranged combat comps + reload/search/flee
```

Keep engine neck-down: `NPCSteeringSystem`, pathfinding, `NpcFactionSystem`, sleep/wake.

## MUST

1. Opt-in via `FishNpcBrain` component; leave service NPCs on HTN.
2. When `DisableHtn: true`, call `HTNSystem.SetHTNEnabled(..., false)` on startup.
3. Mode changes only after `ModeHysteresis` unless forced (dead target, empty gun).
4. Persist `LastSeenCoordinates` on LOS/contact; `Search` uses them.
5. Assault: close distance, early melee commit, late flee.
6. Defense: hold ranged band, backstep if too close, earlier flee/reload.
7. New Fish AI code lives in `_Fish`, not vanilla `Content.Server/NPC` except minimal hooks (`Fish-start`/`Fish-edit`).

## MUST NOT

1. Rewrite `NPCSteeringSystem` / pathfinding for doctrine work.
2. Duplicate HTN trees as the primary hostile brain.
3. Subscribe a second `HTNComponent`+`MapInitEvent` (Robust allows one directed sub).
4. Thrash modes every tick without hysteresis.

## Patterns

1. Mode scores as floats; pick max with sticky bonus for current mode.
2. Combat via ensuring `NPCMeleeCombatComponent` / `NPCRangedCombatComponent`.
3. Movement via `NPCSteeringSystem.Register` to entity or last-seen coords.
4. Reload logic shared with `ReloadGunOperator` / inventory insert.

## Anti-patterns

1. “Just another SimpleHostileCompound with bigger VisionRadius.”
2. Plan jobs inside the Fish brain tick.
3. Always-awake whole expedition map without proximity policy.

## Quality check

1. Assault mob closes and commits melee; Defense holds band and falls back.
2. After LOS loss, Search moves to last-seen then Idle/Hunt.
3. Empty mag → Reload before Engage*.
4. HTN disabled while brain runs; Sleep/Wake still works.
5. Integration test covers doctrine defaults + mode enum wiring.

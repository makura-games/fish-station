# Fish Clockwork Cult (Ratvar)

1:1 mechanical port of tgstation Clockwork Cult (late 2017/2018 Reebe/Ark rework) for Fish Station.

## Architecture

| Layer | Path |
|-------|------|
| Shared | `Content.Shared/_Fish/ClockworkCult/` |
| Server | `Content.Server/_Fish/ClockworkCult/` |
| Client | `Content.Client/_Fish/ClockworkCult/` |
| Prototypes | `Resources/Prototypes/_Fish/ClockworkCult/` |
| Map | `Resources/Maps/_Fish/Nonstations/reebe.yml` |
| Audio | `Resources/Audio/_Fish/ClockworkCult/` |

## Mechanics correspondence (tg → Fish)

| Mechanic (tg) | Fish implementation | Status |
|---------------|---------------------|--------|
| Global clockwork power (W) | `FishClockworkPowerComponent` on gamerule + `FishClockworkPowerSystem` | Fully |
| Vitality pool | same power component / AdjustVitality | Fully |
| Herald 25% cost cut (`*0.75`) | `TryAdjustPower` when Heralded | Fully |
| Ratvar infinite power | `RatvarAwakened` short-circuit | Fully |
| Script unlock 50kW **or** Ark halfway **or** convert | power threshold + Ark prep halfway + conversion | Fully |
| Application unlock 100kW **or** convert | power threshold + conversion (`UnlockApplication`) | Fully |
| Servants of Ratvar antag | `FishClockworkCult` rule + AntagSelection + mind role | Fully |
| Clockwork slab + scripture UI | `FishClockworkSlab` BUI | Fully |
| Driver/Script/Application scriptures | `FishClockworkScripturePrototype` + handlers | Fully |
| Kindle / Manacles / Compromise charges | slab AfterInteract charges | Fully |
| Vanguard stun absorb + 25% backlash / >30s KO×2 | `FishClockworkVanguardSystem` | Fully |
| Abscond → Reebe | teleport to Reebe marker/Ark | Fully |
| Spatial Gateway (scripture) | 4s portal + Destination teleport | Partially |
| Integration Cog APC drain ~5W/s | `FishIntegrationCogSystem` | Fully |
| Replica Fabricator sheets→power / brass / floors / walls | `FishReplicaFabricatorSystem` (POWER_METAL etc., 5 brass @ 1250W, WallBrass / FloorBrassFilled) | Fully |
| Sigils Transgression / Submission / Vitality | `FishClockworkSigilSystem` | Fully |
| Transmission sigil | servant on sigil → +10 W/s | Partially (no structure power draw graph) |
| Ocular Warden / Mania Motor / Stargazer | `FishClockworkStructureSystem` | Fully (simplified ranges) |
| Obelisk broadcast (50W) + gateway (2000W, 5 uses / 10s) | Activate + Alternative verbs | Fully |
| Herald Beacon vote + war declare | vote + global announce + herald flag | Fully |
| Eminence Spire | elect + soft collision + Mass Recall action | Partially (not full ghost/spectate) |
| Clockwork Marauder (8000W) | ghost-takeover combat shell | Partially |
| Ark prep `min(35, 30+players/5)` | rule start | Fully |
| Ark +2.5 W/s generation | Ark Update | Fully |
| Ark grace 300s → defense → Ratvar 600s | `FishClockworkArkSystem` | Fully |
| Ark mass recall / scream on damage | implemented (+ Eminence action) | Fully |
| City of Cogs rifts on station | spawn on defense start | Fully |
| Brass traps (skewer/vent/sensor/lever/repeater) | `FishClockworkTrapSystem` | Partially |
| Clockwork Armaments CallArmor / CallSpear | InstantActions + cooldowns 180s / 30s | Fully |
| Judicial Visor delayed smite | AfterInteract + Timer | Fully |
| Wraith Spectacles true sight + eye damage | NV + Blunt DPS while worn; heal on unequip; skip if Heralded | Partially (Blunt, not organ/eye) |
| Reebe map (City of Cogs) | `reebe.yml` brass grid + markers | Partially |
| MobRatvar arrival | `MobRatvarSpawn` | Fully |
| Collective Mind / Hierophant | Sunrise CollectiveMind `FishClockworkCult` | Fully |
| AI faction hostility | FishClockworkCult + Fish-edit on NT/etc. | Fully |
| Secret weight | `FishSecret` weight 0.12 | Fully |

## Remaining honest gaps (not 1:1)

| Gap | Notes |
|-----|-------|
| Reebe layout | Brass shuttle, not full City of Cogs rooms/inductors |
| Marauder | Combat shell only — no full construct kit / abilities |
| Traps | Radius triggers; no DeviceLink / slab wiring; steam = Smoke+popup |
| Transmission | Flat +10 W/s; no per-structure power network |
| Eminence | Soft collision + recall; not true incorporeal spectate / free camera |
| Wraith | Blunt DPS stand-in; no progressive blindness organ damage |
| Spatial Gateway scripture | Fixed 4s / one dest; obelisk has 5 uses / 10s closer to tg |
| Armaments gear sprites | Placeholder security clothing RSI |

## Win / Loss

- Ark destroyed → cult failure + round end
- Ratvar arrival (gateway progress ≥ 600s after grace) → cult win

## Notes

- New code only under `_Fish` / Fish-prefixed prototypes.
- Map is a brass shuttle layout adapted from Sunrise rust_brass with Ark + spawn markers.
- Marauder scripture cost: **8000 W** (tg value).
- Brass create cost: **1250 W** for 5 sheets.

# The guidance effect - built

The wind of blue lines (`src/Streaks.cs`): how it looks, how it got there, how it is made. The plan
is [`../ROADMAP.md`](../ROADMAP.md).

Built the second way in `src/Streaks.cs`, then reworked twice after looking (2026-09-24). The
first version - a box emitter three metres behind the player, forty-five short streaks a
second when close, plus the wishbone's pings - was too much, too close and too loud. The
second - a ring ten metres around the player - was stiff and, on an ultrawide screen, all at
the edges of the view. Now it is two `ParticleSystem`s made in code that start *at* the player
and fly toward the target, world space, at a wind's pace with a little variation per particle,
swayed sideways by the noise module: the lines are trails drawn behind hidden particles, so
each line bends along its wavy path, one every second or two - two or three in the air at
once, each starting anywhere in the first five metres of the way - and a few small motes
ride along. The wishbone ping's material is copied. The pings are gone for
good; the only sound is none. The lines fly on the flat and keep low over the ground: every
tick each particle is given the vertical speed that carries it toward a metre or so above
the terrain under it (a terrain raycast per particle, a dozen at most; the noise sways only
sideways), so a hill carries them up and over instead of swallowing them (seen vanishing
into a slope, 2026-09-24; setting the position instead of the speed drew zigzags, same day);
rocks and trees are not sampled, so a boulder is still flown through. Once the target is nearer than a line flies in its life
each line gets exactly the life the way takes, so close by they run into the altar instead of
over it (seen sailing over it from fifteen metres, 2026-09-24). Within `ArrivalDistance`
(ten metres by default) `SE_Compass`
stops the wind - the player has arrived, and lines still in the air fly out and fade.
The lines are half transparent and taper to a point at both ends (`Blue` and `Taper` at the
top of the file, 2026-09-24); a line appears as its tip draws it out and disappears as its
tail runs up to the tip, because the trail outlives its particle instead of dying with it. `StreakEffect` is the config switch, the counts, sizes, sway and
start point are the `Layer` constants at the top of the file.

**The player:** three or four wavy lines of bright blue light leave the player toward the
target, a couple more when close, never a storm, and none once there. They sit in the middle
of the view. No sound. Nothing on the map.

**How:** `SE_Compass : StatusEffect`, created with `ScriptableObject.CreateInstance`, `name =
"OdinsCompass"` (the status effect is found by `name.GetStableHashCode()`, so the name is the
identity), added to `ObjectDB.m_StatusEffects` next to the items, and set as every tier's
`m_equipStatusEffect` - `Humanoid.UpdateEquipmentStatusEffects` adds it while a utility item
with it is worn and removes it when not, nothing to patch. Which tier is worn:
`Player.m_utilityItem.m_dropPrefab.name`.

The streaks - the `compass psystems` dump of 2026-09-24 settled it: no environment has a wind
streak system (the systems are mist, fog, rain, snow, ash, `WhirlWind` for Moder's fight and
interior dust), so the first way is out and the effect is built in code:

- **Reuse the game's wind.** The environments' particle systems are `EnvSetup.m_psystems` on
  `EnvMan.instance.m_environments` (`compass psystems` lists them by environment). If one of them
  is a wind streak system (the rain and snow ones are; a clear weather "wind" is the thing to
  look for - **verify** it exists), clone it, tint its start colour bright blue, parent it to the
  player, and turn its emitter so the streaks travel from behind the player toward the target
  (velocity over lifetime along the target direction, in world space).
- **Build one in code.** A `ParticleSystem` on a new `GameObject` under the player: a ring
  emitter around the player, world space simulation, velocity toward the target, long stretched
  billboards (stretched render mode) in bright blue, additive material taken from
  `vfx_WishbonePing`'s renderer so no material asset is needed. Rate scales with closeness.

Either way the direction is `target - player` flattened to XZ, and the effect stops when there
is no target. Sound: `SE_Finder`'s `m_pingEffectNear/Med/Far` on the wishbone's own status
effect were played with `SE_Finder`'s cadence and dropped after the first look - a compass
that pings every second is a nuisance. If a sound ever comes back it is a soft one on a
change (a new target found, the target switched), never a cadence.
`vfx_WishbonePing` is the fallback visual if the streaks disappoint.

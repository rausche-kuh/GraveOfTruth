# OdinsCompass

A craftable compass, worn in the utility slot like the wishbone, that points the way to the
nearest boss and, tier by tier, to each biome's dungeons and ore. See the root `CLAUDE.md` for
the shared build, the scripts and the environment.

**State:** everything in `ROADMAP.md` sections 1 to 5 is built - the eight compasses, the
seeking, the target choice, the wind lines, the words - and played on a local world:
crafting, wearing, the seeking and the wind (reworked five times on looking; the wishbone
pings were dropped) all seen. The 0.1.0 package is built (`dist/OdinsCompass-0.1.0.zip`,
changelog section `## 0.1.0`) but not yet uploaded; the next change opens a new
`## Unreleased`. What is still to check is `ROADMAP.md` section 6 (the dedicated server test
above all); what comes next is section 7: a menu to choose the target, and real icons -
`package/icon.png` and `assets/icons/compass.png` are generated placeholders.

| Path | What |
| --- | --- |
| `src/OdinsCompass.cs` | BepInEx entry point and settings: the seek interval, the cycle key, the arrival distance, and per tier the target groups and the recipe, both as config strings. |
| `src/Items.cs` | The eight compass prefabs cloned from `Wishbone` under an inactive root, their recipes from the config, and the postfixes that put them into every `ObjectDB` and into `ZNetScene`. `TierOf` reads a tier off an item. |
| `src/SE_Compass.cs` | The one status effect every tier applies while worn (`m_equipStatusEffect`). On the local player it runs the compass: owns the `Seeker` and the `Streaks`, handles the cycle key (`Player.Update` postfix) and the saved choice (`Player.m_customData`), and writes the icon text. |
| `src/Seeker.cs` | Finds the chosen group: vegvisir asks to the server for locations (the routed RPC directly, answers caught by the prefix on `Game.RPC_DiscoverLocationResponse`), a sliced `ZDOMan` walk for world objects; knows whether it has found, is still seeking, or the world has none. Asks rarely: a found target is kept until the player has walked a quarter of the way toward it. |
| `src/Streaks.cs` | The guidance effect: two `ParticleSystem`s built in code, a few wavy blue lines (trails behind noise-swayed particles) and a few motes, starting in the first few metres of the way and flying at the target low over the ground (each particle is steered toward a height over the terrain under it every tick), a little more the closer, and close by cut to end in it. The `Layer` constants at the top are the tuning. |
| `src/Targets.cs` | `TargetGroup`: the per tier `Targets` config parsed once into name + prefabs + tier; `ForTier` is what a compass offers. |
| `src/Translations.cs` | Hands `assets/translations.csv` to the game's localization on every language setup. |
| `src/Icons.cs` | PNGs beside the DLL as sprites, through `ImageConversion.LoadImage` found by reflection. |
| `src/Dev/CompassCommands.cs` | `compass locations | prefabs | items | psystems | find` — the checks the roadmap asks for; `items` reads the item database and works from the main menu. Debug builds only. |
| `assets/translations.csv` | Every word the mod shows, one row per `$oc_` token, English and German so far. |
| `assets/icons/` | Item icons, shipped beside the DLL (Gale flattens the folder on install, so look in `icons/` and then beside the DLL). |
| `ROADMAP.md` | The plan, feature by feature, with the game facts each one rests on and which of them still need checking. |
| `package/` | What Thunderstore gets: `manifest.json`, `icon.png`, `README.md` (the mod page), `CHANGELOG.md` (the changelog, see the root `CLAUDE.md`). |

## The rules that always apply

- **Client side, on any server.** Locations are found through the game's own vegvisir request
  (`Game.DiscoverClosestLocation` → the server's vanilla `RPC_DiscoverClosestLocation` →
  `RPC_DiscoverLocationResponse` back to the asker), which every server answers whether or not it
  has the mod. The mod's prefix on the response swallows only answers whose pin name starts with
  `OdinsCompass:`; a real vegvisir's answer must always pass through.
- **Never touch the map.** The compass gives a direction; no pins, no `showMap`, no forced look.
  A map ping is a possible option later, never the default.
- **Ask the server rarely.** Locations do not move; a found one is kept until the player has
  walked a quarter of the way toward it. `Game.DiscoverClosestLocation` logs a line per call, so
  the routed RPC is invoked directly. Anything on a cadence (asks, sounds) is suspect.
- **No sound, and the effect stays sparse and central.** The wishbone's pings were tried and
  taken out; a box of streaks through the player was too much; a ring ten metres out sat at
  the edges of an ultrawide screen and looked stiff. The wind is three or four wavy lines
  leaving the player toward the target, low over the ground, and none within
  `ArrivalDistance` of it - tune it down before tuning it up.
- **Never deploy over a running game.** Mono reads method bodies from the plugin DLL lazily;
  a DLL replaced under a running game throws `BadImageFormatException: Method has zero rva`
  with garbled names in the trace from the next status effect tick on. It looks like a mod bug
  and is not one - `deploy` warns, and the game is restarted before judging a build.
- **A missing wishbone is only news in a world.** The menu's item databases wake empty and are
  copies without it, so the clone step is silent; only `ZNetScene.Awake` warns, after trying
  both the scene's prefab and the item database's.
- **Ore is short range** because veins are vegetation ZDOs, not locations: the client can only
  search what it has loaded. Say so wherever ore is mentioned to the player.
- **Names live in the config, not the code.** Every prefab name in the defaults exists in the
  game (`compass locations` and `compass prefabs`, 2026-09-24); which copper and silver prefab
  the world actually places is the one open question, and the config can list both. A
  corrected name goes into the defaults in `src/OdinsCompass.cs` and the table in `ROADMAP.md`.
- **Keys go through `ZInput`**, never `UnityEngine.Input`: the game runs on the new input
  system. The cycle key is `N` because `C` toggles walking.
- **Nothing the player reads is a literal:** item names, descriptions, group names and messages
  are `$oc_` tokens in `assets/translations.csv`. Config descriptions stay English.
- **The items are made once and registered many times.** The game builds an `ObjectDB` for the
  main menu (`CopyOtherDB`, sharing the prefab's lists) and another for the game scene (`Awake`),
  and `ZNetScene` has its own prefab list; every registration checks what the list already holds.
  The clones live under an inactive `DontDestroyOnLoad` root so `ItemDrop.Awake` never runs on
  them — an active clone would register itself as an item lying in the world.
- Eight items, one status effect: a tier is its own prefab (`OdinsCompass1`..`8`), cloned from
  `Wishbone`, with its own `Recipe` that consumes the tier below. Never a quality upgrade — see
  `ROADMAP.md` section 1 for why the vanilla upgrade maths cannot do it.
- Patches are plain `[HarmonyPatch]` classes nested in the class they serve (`Items`,
  `Translations`, the dev commands in the plugin class); keep them small and
  null-guard everything (`Player.m_localPlayer` is frequently null, and on a dedicated server
  there is no `Player`, no `Minimap` and no `EnvMan` particle to speak of).

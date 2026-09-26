# OdinsCompass

A craftable compass, worn in the utility slot like the wishbone, that points the way to the
nearest boss and, tier by tier, to each biome's dungeons and ore. See the root `CLAUDE.md` for
the shared build, the scripts and the environment.

**State:** `ROADMAP.md` sections 1 to 5 are built and played on a local world. The 0.1.0 package
is built (changelog `## 0.1.0`) but not uploaded; the next change opens a new `## Unreleased`.
Still to check: roadmap section 6 (the dedicated server test above all). Next: section 7, a
target menu and real icons - `package/icon.png` and `assets/icons/compass.png` are placeholders.

## Docs

| File | Read it when |
| --- | --- |
| `ROADMAP.md` | Picking the next task: what is built, the verify list, next, later, bugs. |
| `docs/architecture.md` | Changing a source file: each file in full, every rule below with its reasons. |
| `docs/foundations.md` | Why it is client side (the vegvisir RPC) and why ore is short range. |
| `docs/items.md` | The tiers and recipes, cloning the wishbone, registration; the translations. |
| `docs/seeking.md` | Target groups per tier and their status, asking the server, the cycle key. |
| `docs/guidance.md` | The wind of blue lines: its history, its tuning, how it is built. |
| `docs/radial-menu.md` | Building the target menu on the game's radial menu. |

## Source map

| File | What |
| --- | --- |
| `src/OdinsCompass.cs` | Entry point and settings; per tier the `Targets` and `Recipe` config strings and their defaults. |
| `src/Items.cs` | The eight prefabs cloned from `Wishbone`, their recipes, registration in `ObjectDB` and `ZNetScene`; `TierOf`. |
| `src/SE_Compass.cs` | The worn status effect: owns `Seeker` and `Streaks`, the cycle key, the saved choice, the icon text. |
| `src/Seeker.cs` | Finds the chosen group: vegvisir asks for locations, a sliced `ZDOMan` walk for world objects. |
| `src/Streaks.cs` | The wind: two particle systems built in code; the `Layer` constants are the tuning. |
| `src/Targets.cs` | `TargetGroup`: the `Targets` config parsed once; `ForTier`. |
| `src/Translations.cs` | Feeds `assets/translations.csv` to the game's localization. |
| `src/Icons.cs` | PNGs beside the DLL as sprites. |
| `src/Dev/CompassCommands.cs` | `compass locations \| prefabs \| items \| psystems \| find` (Debug only). |
| `assets/` | `translations.csv` (every `$oc_` token, English and German) and `icons/` (Gale flattens the folder, so look in `icons/` and beside the DLL). |

## The rules that always apply

Reasons for each: `docs/architecture.md`.

- **Client side, on any server.** Locations come from the game's own vegvisir request; the
  prefix on `Game.RPC_DiscoverLocationResponse` swallows only answers whose pin name starts
  with `OdinsCompass:` - a real vegvisir's answer always passes through.
- **Never touch the map**: no pins, no `showMap`, no forced look.
- **Ask the server rarely**: a found target is kept until the player has walked a quarter of
  the way to it; the routed RPC is invoked directly (`Game.DiscoverClosestLocation` logs per call).
  Anything on a cadence is suspect.
- **No sound; the effect stays sparse and central.** Tune it down before tuning it up.
- **Never deploy over a running game**: Mono then throws `Method has zero rva` - not a mod bug.
- **Ore is short range** (veins are vegetation ZDOs); say so wherever ore meets the player.
- **Names live in the config**; a corrected name goes into the defaults in
  `src/OdinsCompass.cs` and the table in `docs/seeking.md`.
- **Keys go through `ZInput`**, never `UnityEngine.Input`. The cycle key is `N` (`C` walks).
- **Nothing the player reads is a literal**: `$oc_` tokens in `assets/translations.csv`;
  config descriptions stay English.
- **Items are made once, registered many times** (menu `ObjectDB`, game `ObjectDB`,
  `ZNetScene`), each registration checking the list; clones live under an inactive
  `DontDestroyOnLoad` root. A missing wishbone is only news in a world.
- Eight items, one status effect - never a quality upgrade (`docs/items.md`).
- Patches are small `[HarmonyPatch]` classes nested in the class they serve; null-guard
  everything (no `Player`, `Minimap` or `EnvMan` particles on a dedicated server).

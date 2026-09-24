# Odin's Tree

Bless a tree so nothing can fell it, and build your tree house on it.

> **Early build:** developed heavily with AI and barely play tested. Expect rough edges.

## What it does

- **Bless a tree** — hammer out, repair tool selected, left click a tree. Lightning strikes it
  and from then on nothing damages it. Looking at it says `Blessed by Odin`. Click again to
  lift the blessing.
- **Safe ground** — the ground within 3 m of a blessed trunk (`TerrainGuardRadius`) cannot be
  dug, raised, levelled or tilled, so the tree never sinks or lifts under your house.
- **Pick the tree** — right click an unblessed tree with the hammer to turn it into the next
  kind of its family (`Families` in the config adds more). A blessed tree never cycles.
- **Twerk it up** — tap crouch three times in a row beside a sapling or crop. It glows green
  while you keep it up, and after about fifteen seconds it is grown (`TwerkGrowSeconds`).

## With and without the mod

- The blessing, the new tree kind and the growth are stored the way the game stores any tree.
  Every player sees them, with or without the mod, and they stay when the mod is removed.
- The safe ground needs the mod. Install it on the dedicated server (or the hosting player's
  game) and it holds for every player, including those without it. Without the mod on the
  server, only players who have it are stopped from digging.
- The game gives a player without the mod no way to see that a tree is blessed. Nearly infinite
  health stays for vanilla clients.

## Install

A mod manager (Gale or r2modman), or drop the zip's contents into
`BepInEx/plugins/rauschekuh-OdinsTree/`. Requires the BepInEx pack for Valheim.

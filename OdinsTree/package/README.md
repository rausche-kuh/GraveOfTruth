# Odin's Tree

A tree house is only as safe as the tree under it. Odin's Tree lets you bless a tree so that
nothing can fell it — and the blessing stays when the mod is gone.

> **Early build:** this mod is developed heavily with the use of AI and has had little play
> testing so far. Expect rough edges.

## What it does

- **Bless a tree** — with the hammer's repair tool selected, left click a standing tree. It says
  `This tree is blessed by Odin`, and axes, trolls, falling logs and fire no longer bring it
  down. Left click again to lift the blessing. Hover a tree with the hammer out to see what a
  click will do.
- **Safe ground** — the ground within 3 m of a blessed trunk (`TerrainGuardRadius`) cannot be
  dug, raised, levelled or tilled. The hoe's ghost turns red, the pickaxe is refused.
- **Pick the tree** — right click a tree that is not blessed with the hammer to turn it into the
  next kind of its family: Birch1 → Birch2 → autumn birch, small beech → big beech, the three
  Yggdrasil shoots, and so on. Whatever a sapling can grow into counts as a family, and
  `Families` in the config adds more. A blessed tree never cycles, so the trunk under a house
  stays put.
- **Twerk it up** — dodge back and forth beside a sapling and every dodge pushes it closer to
  grown; about fifteen seconds of solid effort (`TwerkGrowSeconds`) makes it a tree. Each dodge
  costs its usual stamina. A sapling that cannot grow where it stands says why instead. Any
  planted crop within reach grows the same way.

## Without the mod

A blessed tree stays blessed for everyone, with or without the mod, and after you remove it:
the blessing is stored the way the game stores any tree's health. Only the safe ground needs the
mod — a player without it can still dig beside the trunk.

## Multiplayer

Client side; no server install. Blessing, cycling and growing work through the game's own world
data, so every other player sees the result whether they have the mod or not.

## Install

A mod manager (Gale or r2modman), or drop the zip's contents into
`BepInEx/plugins/rauschekuh-OdinsTree/`. Requires the BepInEx pack for Valheim.

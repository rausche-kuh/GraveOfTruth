# GraveOfTruth

A small Valheim mod that makes dying feel appropriately embarrassing.

- When you die, the obliterator's lightning comes down on your fresh grave, and once the thunder
  has rolled the loser jingle plays out of it, echoing off the hills — everyone nearby sees and
  hears it. The bolt is cosmetic and hurts nobody.
- Dying also brings a short thunderstorm down on your head.
- Coming back to loot the grave plays the jingle one more time, echo and all, with a gust of wind.

The jingle is broadcast to the server, so every player running the mod hears it from the grave;
players without it hear nothing. The weather is client side only — nobody else sees your storm.
Releases before 0.1.0 only had the tombstone sound.

## Install

Use a mod manager (Gale / r2modman) and install `GraveOfTruth` from Thunderstore, or drop the
contents of the release zip into `BepInEx/plugins/rauschekuh-GraveOfTruth/`.

Requires the BepInEx pack for Valheim.

## Build from source

```bash
./scripts/setup.sh              # once, and after every Valheim update
./scripts/deploy.sh GraveOfTruth
```

See [scripts/README.md](../scripts/README.md) for the full workflow on Windows and Linux.

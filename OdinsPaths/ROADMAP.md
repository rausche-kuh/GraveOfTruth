# Roadmap

## Up next

- **Stutter:** check a start-up growth still stutters; the log's slowest frame per road names
  the stage to fix (the writing is the likeliest).
- **Clearing:** a setting for it / all and every type?
- **Coarse terrain cache** (`docs/prior-art.md`), once a whole growth's times are known.
- **Release:** mod page, translations, icon, first Thunderstore upload.
- more natural roads with varying degrees of width / overgrowth of gras
- dialing in presets for shortest exploration / see or land focused exploration
- Connection of bases
- **Webbing** networks get addtional paths in between
- Swamp paths should be dirt
- sign post for the main network
- structure road ends: paint dirt without leveling - trying a wider area to mask not knowing the "stair" position
- A dock beside each harbour stone.
- paths around bases / structures that the path did not target
- always add mini paths to close by structures (houses)
- spawn houses at road forks

## To check in game

- **Names:** `$npc_haldor` / `hildir` / `bogwitch` in the messages are guesses.
- **Other:** leaving the world mid-search, `paths reset` on a copy, Black Forest clearing, a lay
  out of a base, junction heights, `paths bench` at a biome border, snow in the Deep North.

## Later

- Signposts where roads meet (a vanilla `Sign`, text = where it leads).
- Spurs to portals and to dungeons a player has entered.
- Wear: busy spurs widen or turn to stone, as AntTrails does.

# Bugs

None, nothing is released. Risks to watch:

- An exception in a growth leaves `Grower.Busy` set until the world is left.
- `paths undo` takes back only the last job, not cleared trees and rocks or links added to an
  older harbour; `paths reset confirm` takes everything, the player's digging too.
- A base is stored by its first ward; a base that moves over 150 m gets a second road.
- A compiler created while a client generates the same zone - verify no vanilla path does that.
- In a zone generated after its road, vegetation on the shoulder can float or sink a little.
- A harbour stone's height at a shore (`Ground.Height`) - verify.
- A rock without meshes counts as 2.5 m wide; a scattered MineRock5 may be cleared early.
- A player digging in a zone while the server rewrites it: last writer wins.

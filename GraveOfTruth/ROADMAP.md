# Roadmap

What comes next for GraveOfTruth. Every game fact below was written from memory of the game code,
without `decompiled/` at hand — each one is marked **verify** until it has been checked against
`decompiled/assembly_valheim/`, and what the check teaches goes into `CLAUDE.md`.

## Death stats, a death broadcast and a talking grave

The show gets words. Every death is counted, its cause is named, and everyone is told — in the
worst possible light, and in their own language.

### What the player gets

- **A broadcast on death**, to every modded client: `{name} is dead. Again. Death number {n}:
  stood too close to the fire.` — one line of mockery per cause, picked at random from a few per cause so
  it stays fresh. On screen via `MessageHud` (the centre message) and as a line in chat, so it
  can be read back.
- **Days survived** in the same line when it is worth mocking: `…after 0.3 days.` for a quick
  one, and a grudging `…after 41 days. Respect, briefly.` for a long run.
- **The grave's tooltip** gains the same stats: death number, cause, days survived before it. A
  grave keeps the stats of *its* death, so an old grave still tells its own story.

### What is available (verify)

| Stat | Where it comes from | Scope |
| --- | --- | --- |
| Total deaths | `PlayerProfile.m_playerStats[PlayerStatType.Deaths]`, raised by `Game.IncrementPlayerStat` in `Player.OnDeath`. Saved with the character. | per character, all worlds |
| Deaths per cause | `PlayerStatType.DeathBy…` (`DeathByFall`, `DeathByDrowning`, `DeathByBurning`, `DeathByFreezing`, `DeathByPoisoned`, `DeathBySmoke`, `DeathByTree`, `DeathByCart`, `DeathByStalagtite`, `DeathBySelf`, `DeathByEnemyHit`, …), one per `HitData.HitType`, picked by `Player.OnDeath` from `m_lastHit` | per character |
| Cause of this death | `Character.m_lastHit` (a `HitData`): `m_hitType` for the kind, `GetAttacker()` for who — its `m_name` is a `$enemy_…` token, so it localizes itself on every client | this death |
| Time since last death | `Player.m_timeSinceDeath`, the play time since the last death that the game already keeps (and saves) for its own "no skill loss right after a death" rule. Divided by `EnvMan.instance.m_dayLengthSec` it is in-game days. | per character |
| Day of death | `EnvMan.instance.GetDay()` — the world's day counter, as a fallback if `m_timeSinceDeath` does not survive a logout | per world |

So nothing has to be recorded by the mod itself for the first version: the game counts deaths
and causes already, it just never shows them. `OnDeath` must be read *after* the game has
incremented the stat and *before* it resets `m_timeSinceDeath` (verify the order; if the reset
comes first, read the timer in a prefix and hand it to the postfix via `__state`).

### How (plan)

- Extend the existing `Player.OnDeath` patch. Keep it inside the try/catch and keep it free of
  anything that could skip `Game.RequestRespawn` (see `CLAUDE.md`).
- Send **tokens and numbers, never sentences**: a new routed RPC (`GraveOfTruth_Obituary`) with
  the player name, death count, `HitType` as an int, the attacker's `m_name` token (or empty) and
  days survived. Every receiver builds the line in its own language with
  `Localization.instance.Localize("$got_death_fire", name, n, days)`. That is how a multilingual
  server gets each reader their own language, as asked.
- Players without the mod see nothing from the RPC. Optional (`ChatFallback`, off by default): the
  dying player's client also sends the line as an ordinary chat shout in *its* language, so
  unmodded players read it too — at the cost of modded players reading it twice (skip the shout
  on receivers that also got the RPC by matching the text, or accept it).
- The grave's stats go on the tombstone's ZDO under our own keys (`GraveOfTruth_deaths`,
  `…_cause`, `…_attacker`, `…_days`) — written by the dying client, which owns the fresh grave.
  A `TombStone.GetHoverText` postfix appends them. Unmodded clients ignore the keys, and removing
  the mod leaves an ordinary grave.
- Translations: add `assets/translations.csv` and a loader the way OdinsMissingPatch does it
  (`$got_` tokens, one column per language, see `OdinsMissingPatch/docs/translations.md`). Ship
  English and German first; the other languages OdinsMissingPatch ships are the target.
- Ordinals do not translate ("41st", "41.", "41-й"): phrase the lines around a plain number —
  "Death number 41", "zum 41. Mal" is written into the German string itself.

### Lines (drafts)

Per `HitType`, a few each, `$1` name, `$2` death count, `$3` attacker:

- **Burning** — "stood too close to the fire", "tried to warm up. Succeeded."
- **Fall** — "forgot Valheim has gravity", "took the fast way down"
- **Drowning** — "believed Vikings can breathe water", "went for a swim. Forever."
- **Freezing** — "forgot their cape", "is now a very realistic ice sculpture"
- **Tree** — "lost a fight with a tree. The tree is fine."
- **EnemyHit** — "was taught a lesson by a $3", with sharper lines for embarrassing attackers
  (neck, greyling, boar, deer): "was killed by a $3. A $3."
- **Self / Undefined** — "died. Nobody knows how. Nobody is surprised."
- Milestones override the cause line: first death, 10th, 50th, 100th.

### Open questions

- Is the count per character (the game's own stat, carries across worlds) what is wanted, or
  per world? Per character costs nothing; per world needs our own counter in the player's
  `m_customData` keyed by world.
- Death number on the grave's own name? Vanilla writes the owner's name into the grave's text;
  appending "(death 41)" there would even show for unmodded players, but it rewrites vanilla data.

# Bugs

None known.

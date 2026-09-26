# Roadmap

Odin's Tree makes a tree safe to build a tree house on. The four features from the first plan
(bless, guard the ground, cycle kinds, twerk-grow) are built in the simplest form that works;
what each one rests on is in
[`docs/mechanics.md`](docs/mechanics.md) and [`docs/terrain-guard.md`](docs/terrain-guard.md). This is what comes next, and what is known to be rough.

## 1. Polish the four features

- **Bless feedback** plays only for the blesser: other clients see nothing. A small RPC (or
  a short-lived ZDO key polled from `TreeBase.Awake`) would let everyone see the show.
  Lifting only plays the repair tool's place effect. The glow's light sits 2 m beside the
  trunk, where the boss stone's runes are.
- **The warden** ([`docs/terrain-guard.md`](docs/terrain-guard.md)) has not run on a dedicated server yet. To check there: the server log for
  `Terrain compiler could not find hmap` (a compiler instantiated before its zone), a vanilla
  client digging beside a blessed tree gets `Odin's tree guards this ground` and no change,
  digging 20 m away still works and shows up for everyone, and the ownership of the
  compiler stays with the server (no full-data resend every 2 s - watch the bandwidth in the
  F2 panel). A vanilla client still sees a green hoe ghost and pays stone and stamina for a
  refused dig; only the mod on their side could stop that. A vanilla client has no sign
  that a tree is blessed at all - the candidates were a `Sign` on the trunk (readable by
  everyone, but a removable wooden board) and a ward (32 m, blocks building); both rejected.
- **Hover hints** are literal `[LMB]` / `[RMB]`; use the game's key glyphs (`$KEY_...`, the way
  `Door.GetHoverText` does) so gamepads and rebinding work.
- **Cycling** spawns the next kind at the old tree's position, rotation and scale. Trees the
  sapling logic grows get a random yaw; ours keep the old one, which is right. A cycled tree
  has full health again (fresh ZDO) — fine for a tree.
- **Twerking** has no puff of leaves yet; `Plant.m_growEffect` is the full grow effect and too
  big. The green tint is the piece highlight's `MaterialMan` colour; a green light would show
  at night too.
- **Unhittable** only covers `TreeBase`. The weapon's own hit effect (the axe's sparks and
  sound) still plays on a blessed trunk, exactly as it does on the ancient root; making the
  swing pass through would mean moving the tree's colliders off the attack layers, which also
  carry building and walking.
- **Terrain guard** is checked in the ghost, on every `TerrainOp` this client spawns, and in
  `TerrainComp.RPC_ApplyOperation` on the terrain's owner, which answers the digger with the
  game's `ShowMessage` RPC.

## 2. Later

- A setting for who may bless: anyone, or only players with access to the ward over the tree.
- Translations with a `translations.csv` like OdinsMissingPatch's.
- Trees that are `Destructible` rather than `TreeBase` (`Beech_small1/2` may be) can be cycled
  but not blessed and carry no hover hint — verify which prefabs those are and decide whether a
  young tree should be blessable at all.

# Bugs

- Twerking claims the sapling's `ZNetView` but never checks the ward (`PrivateArea.CheckAccess`),
  unlike blessing and cycling: anyone can grow a sapling inside someone else's ward.

Watch for:

- `Families` in the config merges with the sapling families by shared name; a typo in the
  config silently makes a family of one, which is ignored.
- Lifting a blessing writes the prefab's `m_health`, not the world level scaled default the game
  would compute — a lifted tree on a world level above 0 is a little weaker than an untouched one.

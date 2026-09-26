# Floor shift

How the entrance and exit frames are lined up vertically and in depth (`Portal.Shift`, `Portal.Floor`). Read before changing the alignment or calibrating a new dungeon.

The two teleport triggers are not at the same height above their floors (about 30 cm apart in a
burial chamber), nor the same distance from the visible door faces. The shift is a translation
of the whole mapping in the exit frame (`Portal.Shift`, a vector), and `ientrance status` prints
every measurement behind it. `Portal.Floor` picks how the vertical part is measured;
`ientrance floor` cycles it:

- `Auto` (default): `Boxes` when both box bottoms sit within `BoxTolerance` (0.25 m,
  `ientrance tol`) of their probed floors, else `Probes`. A burial chamber's boxes stand on the
  floor and lined up nearly perfectly; a troll cave's did not and its probes did.
- `Boxes`: the bottom edge of the black box against the bottom edge of the white box.
- `Probes`: the highest floor hit at 0.1 - 0.6 m in front of each door, plus the per-location
  `Offsets` table measured by hand (Crypt2 - 4: y 0.16; the names are a guess to confirm with
  `status`). Probes alone gave 0.21 at a crypt where 0.36 looked right, hence the table.
- `Off`.

The depth part (`BoxDepth`) is the white box's face towards the dungeon against where the black
box's front face lands when turned about, in every mode but `Off`: the visible faces are the
ones the designers placed exactly, whether or not the bottoms are sunk. Before it existed the
crypt wanted `offset 0 0 0.1` and the troll cave `0 0 0.6` by hand; whether the boxes give those
numbers is unconfirmed.

# Next: choosing the target in a menu

The design notes for the menu entry in [`../ROADMAP.md`](../ROADMAP.md) section "Next".

`N` cycles blind through a tier's groups - a tier 5
compass has a dozen - and the only feedback is the status icon's text. Wanted: the key opens
a menu that shows every group the worn tier offers with its icon and name, the current one
marked, one click to pick. The game's radial menu (`Hud.instance.m_radialMenu`, a
`Valheim.UI.RadialBase`) is the natural fit: it is opened with an `IRadialConfig`
(`LocalizedName`, `Sprite`, `InitRadialConfig(radial)`), whose `InitRadialConfig` instantiates
elements and hands them to `radial.ConstructRadial(list)`; `EmoteGroupConfig` is the
simplest example (25 `EmoteElement`s from `RadialData.SO.EmoteElement`, one `Init` each).
A `RadialMenuElement` carries `Name`, `SubTitle`, `Description`, an `Icon` image and an
`Interact` func, so a mod's element is a `RadialMenuElement` subclass or a reused
`EmoteElement`/`ItemElement` prefab with the mod's sprite and text - **verify** which
element prefab in `RadialData.SO` can be instantiated with a custom sprite and name without
an item or emote behind it, and that `RadialBase.Open(config)` from a `Player.Update` postfix
on the cycle key opens it (`OpenRadialConfig` shows the game's own open path). `RadialBase`
and the elements are public, so no publicizer is needed. The `N` cycling stays as the
fallback (a gamepad has no spare key; the config can bind the menu to the radial's own
button with a modifier). The gamepad and the `Hud.InRadial()` input block come for free with
the game's radial. A plain panel of buttons (`Hud` prefab clones, the way the inventory
screen's tabs are made) is the fallback if the radial cannot be reused.

# The words on screen

Everything the mod puts in front of a player goes through the game's own localization, so the mod
speaks whatever language Valheim is set to. `assets/translations.csv` holds the words,
`src/Translations.cs` hands the file to the game, and a call site holds nothing but a token.

## Conventions

- **A call site passes a `$omp_` token, never a sentence.** Tokens are namespaced with the mod's
  initials so nothing can collide with the game's own words or another mod's.
- **Every token lives in `assets/translations.csv`**, which the build copies next to the DLL. The
  first column is the key (without the `$`), the second is English, and each further column is a
  language named exactly as Valheim names it (`German`, `Portuguese_Brazilian`, ...). Adding a
  language is adding a column and nothing else — no code change, no new file. A row whose key
  starts with `//` is a comment; a value holding a comma goes in `"quotes"`, and a quote inside a
  value is doubled (`""`).
- **Adding a string means adding a row.** A literal that reaches the screen is a bug; the check is
  `grep -rn '"[A-Z][a-z]* ' src/` turning up nothing but config text.
- **Config descriptions stay English.** They are bound at `Awake`, long before a language is
  known, and BepInEx writes them into the `.cfg` file as comments — a translated description would
  rewrite the player's config file on every language change. The config manager is English too.
- **Where a token is translated depends on where it lands:**
  - `UITooltip.m_topic` / `m_text` and `Player.Message` translate what they are given when they
    show it, so those take a bare token and follow a language change by themselves.
  - A label written straight onto a component does not (`NearbyChests.TextButton.SetLabel`
    localizes on the way in and compares translated, so the caller may hand it the same token
    every frame).
  - A radial menu's `IRadialConfig.LocalizedName` is written onto a `TMP_Text` unchanged, despite
    the name — `PowerPicker` translates it itself.
- **A number or an item name goes in as `$1`, `$2`**, so word order stays the translator's to
  choose: `Localization.Localize("$omp_took", moved.ToString())`. The substitution happens *after*
  the lookup, so anything that is itself a token (`$item_wood`) has to be translated before it is
  passed in — see `AddAll.HoverLine` and `QuickStack`'s favourite message.
- **Plurals are separate tokens** (`$omp_clear_favourite` / `$omp_clear_favourites`,
  `$omp_stacked_one_chest` / `$omp_stacked_chests`). The game has no plural rules, and a language
  that needs a third form can give both rows the same words.
- **A translated string that is cached rather than rebuilt every frame must be rebuilt when the
  language changes.** `Translations.Revision` counts the loads; `NearbyChests` compares it
  alongside the marks it described.

## Game facts

- `Localization` lives in **`assembly_guiutils.dll`**, not `assembly_valheim` — it is not in
  `decompiled/`, so read it with
  `ilspycmd -r lib -t Localization lib/assembly_guiutils.dll`.
- `Localization.SetupLanguage(string)` is the hook: the constructor calls it for English, startup
  calls it again for the player's language, and `SetLanguage` calls `Clear()` (wiping every
  translation and the lookup cache) and then it. A postfix on it therefore has to re-add the mod's
  words every time, which is exactly what `Translations.Load` does.
- `Localization.LoadCSV(TextAsset, language)` is public, and `AddWord` is private — the CSV is the
  way in. **A language the file has no column for loads nothing at all**, tokens included, so
  `Translations` checks the header and asks for English instead when a column is missing. Within a
  column that does exist, an empty cell falls back to column index 1, which is why English is
  second.
- `Localize` resolves `$word` up to the first of `` (){}[]+-!?/\&%,.:-=<>\n `` or the end of the
  string, so `_` is safe in a token and a value may hold anything. An unknown token shows as
  `[omp_...]` — that, in game, means the CSV did not load.
- `TextAsset` cannot be named from a `net472` build: its module is built against netstandard 2.1
  and the reference fails with CS1705, the same wall `PanelButtons.LoadPng` hits. `Translations`
  builds one and calls `LoadCSV` by reflection instead.
- Valheim ships 24 languages; the names are in `Localization.LocalizationConstants`, and
  `BCP47ToLanguage` in the same class maps a locale to one.

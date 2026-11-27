# Localization

PLOP! ships in eight languages: English (source), German, Ukrainian, Russian, French, Spanish, Italian and Brazilian Portuguese.

## Translation status — read before release

| Language | Code | Status |
|---|---|---|
| English | `en` | Source text |
| German | `de` | **Unverified.** Drafted by an AI model (Claude); not reviewed by a native speaker |
| Ukrainian | `uk` | **Unverified.** Same |
| Russian | `ru` | **Unverified.** Same |
| French | `fr` | **Unverified.** Same |
| Spanish | `es` | **Unverified.** Same (Spain/neutral Spanish) |
| Italian | `it` | **Unverified.** Same |
| Portuguese (Brazil) | `pt-BR` | **Unverified.** Same |

All seven translations need a native-speaker review before store release. Priorities for the reviewer:

- Tone: cozy and playful, with the informal "you" (du / tu / tú / tu / você) in de/fr/es/it/pt-BR and the polite plural in uk/ru.
- Length: every line must fit the button or card it's in.
- Plural forms.
- World and cosmetic names, which are creative translations rather than literal ones.
- The game name "PLOP!" and the creature name "Plop" are never translated.

## How it works

- **Key = English source text.**
  - `Loc.T("Undo")` returns the translation, or the English text when there is none, so a missing entry never shows a key.
  - `Loc.F("Level {0}", n)` formats in the language's culture.
  - `Loc.P("{0} moves", "{0} move", n)` picks the plural form:
    - English, de, es, it: *one* when n = 1, otherwise *other*.
    - fr and pt-BR: *one* for 0 and 1.
    - uk and ru: *one* / *few* / *many*, using the CLDR integer rules.
- **Tables** live in `Assets/Localization/Resources/Localization/<code>.txt`:
  - UTF-8, one `English<TAB>Translation` per line; `#` starts a comment; `\n` is a line break.
  - Plural entries list their forms separated by `|`.
  - `source.txt` lists every key; plural keys carry the English *one* form after a tab.
- **Edit the tables through the generator** `Tools/Localization/build_tables.py` (`python3 Tools/Localization/build_tables.py`).
  - It holds every row and checks each one: the right number of plural forms, the same `{n}` placeholders, no duplicate keys, and no glyphs the bundled font lacks.
  - A translator may also edit the `.txt` files directly. The test `LocalizationTests` checks that every table covers every source key.
- **Labels:**
  - `Loc.Set(text, english, args…)` binds a UI `Text` to its source through a `LocalizedText` component, which re-translates it when the language changes.
  - Long translations shrink to fit, down to 62 % of the design size, instead of being clipped.
  - `Loc.SetRaw(text, value)` sets text that must not be translated (numbers, percentages, "PLOP!").
- **Language choice:** Settings ▸ Language cycles through the eight languages.
  - The choice is saved under `plop.language`.
  - With no saved choice, the device language is used (any Portuguese → pt-BR), else English.
  - Tests and editor previews stay in English.
- Core (.NET) code stays English-only. Unity-side code translates the core's strings, e.g. the cosmetic unlock conditions in `CharacterStudio`.

## Fonts

- The bundled Nunito TTFs are a Latin subset: Basic Latin and Latin-1, 267 glyphs. That covers de/fr/es/it/pt-BR, except `œ`/`Œ`, which the tables avoid.
- **Cyrillic (uk, ru) is not in the bundled font.**
  - The fonts are dynamic Unity fonts, so missing glyphs are drawn with the OS fallback font (Roboto on Android, San Francisco on iOS). The text stays readable but doesn't match Nunito's rounded style.
  - Fix before release: bundle the full Nunito (SIL Open Font License 1.1, which includes Cyrillic). It needs the owner's go-ahead to download.

# Nikud · נִקּוּד

Add vowel points to a Hebrew word or sentence without sending the text off your computer. On Windows, select text in Word or another editor and use a keyboard shortcut to add nikud in place. On macOS, use the local browser interface.

| Platform | Download |
| --- | --- |
| Windows x64 | [Nikud-Offline-Windows.zip](https://github.com/dviradar5/Nikud/releases/latest/download/Nikud-Offline-Windows.zip) |
| macOS | [Nikud-Offline-macOS.zip](https://github.com/dviradar5/Nikud/releases/latest/download/Nikud-Offline-macOS.zip) |

These links download the latest release packages. [All releases](https://github.com/dviradar5/Nikud/releases)

## Windows

1. Download the Windows ZIP above. Right-click it and choose **Extract All**. Open **Nikud.exe** inside the extracted `nikud` folder. Keep the `NikudEngine` folder beside it.
2. Settings shows the active shortcut. The default is **Ctrl+Shift+J**. To change it, click **Vocalize selection** and press a combination that includes **Ctrl**, **Alt**, or **Win**.
3. Select an editable word or sentence and press the shortcut to add or remove nikud. If a reading needs review, choose it in the popup and select **Apply**. **Cancel** leaves the original selection as typed.

Closing Settings leaves Nikud in the notification area. Double-click **נִ**, sometimes under **^**, to reopen Settings. The tray menu also has **Open text editor** for typing or pasting text directly. Right-click the icon and choose **Exit** before replacing an older installation.

The shortcut uses standard Windows copy and paste. Keep the selection active while Nikud works. Some apps block simulated keystrokes, particularly when they run as administrator. If the shortcut fails, check the last status in Settings and try the built-in editor with `ספר`.

## macOS

1. Download the macOS ZIP above and double-click the ZIP in Finder to extract it.
2. Install [Python 3.12](https://www.python.org/downloads/). Open Terminal, type `cd ` with a trailing space, drag the extracted `nikud` folder into Terminal, then press Return.
3. Run:

```sh
bash macos/start.sh
```

The first run installs dependencies and requires an internet connection. Nikud opens a local browser tab. Paste the Hebrew text, select **Add or remove nikud**, then select **Copy result**. Keep Terminal open while you use it. Press **Ctrl+C** there to stop it. The same launcher works from a Git clone. [macOS setup guide](macos/)

## Add, remove, or redo nikud

- **Unpointed text** gets nikud.
- **Pointed text** loses its nikud when every Hebrew word in the selection has marks. Spaces, punctuation, and surrounding non-Hebrew text are preserved.
- **Mixed text** gets nikud on the unpointed words and keeps existing marks on the others.

To redo a single word on Windows, select it and press the shortcut once to remove its nikud. Select the resulting word again and press the same shortcut to add nikud or choose a reading. On macOS, paste the unpointed result back into the input box before running it again.

## Readings and spelling

Try `ספר` by itself. You can choose **סֵפֶר** (written work), **סַפָּר** (hairdresser), or **סָפַר** (counted), among other readings. In `קראתי ספר אתמול.`, the sentence provides context for **סֵפֶר**. The app suggests the contextual reading and displays available alternatives.

- **Spelling.** If standard pointed spelling requires adding or removing a letter, Nikud keeps the original spelling.
- **Dagesh.** Nakdimon predicts dagesh along with the vowels. Nikud also checks a missing dagesh in בג״ד כפ״ת after a sheva against the lexicon. It restores the dot when exactly one matching pointed form supports it and all other marks agree. Forms with a sheva and a following undotted letter, such as **מַלְכֵי**, can be valid.
- **Coverage.** The model can misread a word, and lexicon coverage varies with spelling and inflection. Review the suggestions when exact vocalization matters.

Nikud runs locally and requires no API key or account.

## Report a problem

On Windows, use **Report a bug** or **Report a word mistake** in Settings or in the tray menu. On macOS, both links are below the editor. Reports are public GitHub issues assigned to the maintainer. Use a sample sentence if context matters. Nikud leaves the text fields empty so you can choose what to share.

- For problems with launch, the shortcut, or the interface, include the app and steps to reproduce. [Report a bug](https://github.com/dviradar5/Nikud/issues/new?template=bug.yml)
- For wrong nikud, missing readings, or inaccurate definitions, include what Nikud showed and what you expected. [Report a word mistake](https://github.com/dviradar5/Nikud/issues/new?template=word.yml)

## Build and test

Install Python 3.12 and the dependencies in `offline/requirements.txt`, then run `python -m unittest discover -s offline`.

On Windows, build the tray executable from the repository root with `./windows/Build.ps1` in PowerShell. The script uses the Windows .NET Framework compiler. To bundle the worker, install PyInstaller into the Python environment and run the following from `offline`:

```sh
python -m PyInstaller --onedir --name NikudEngine --collect-data nakdimon --add-data "heb_voc.tsv:." vocalize.py
```

Place the resulting `dist/NikudEngine` folder beside `Nikud.exe`.

## Credits

- [Nakdimon](https://github.com/elazarg/nakdimon) by Elazar Gershuni and Yuval Pinter provides the Hebrew diacritization model. [Research paper](https://aclanthology.org/2022.findings-naacl.75/)
- Nikud bundles pointed word forms, lemmas, and grammatical tags from UniMorph Hebrew. The dataset credits Wiktionary contributors as its source and Omer Goldman as its annotator. UniMorph distributes the dataset under CC BY-SA 3.0. Short glosses for common ambiguous words are written for Nikud. [UniMorph Hebrew](https://github.com/unimorph/heb#hebrew-heb) · [Wiktionary](https://www.wiktionary.org/) · [Dataset license](https://creativecommons.org/licenses/by-sa/3.0/)
- [Shift Happens](https://github.com/Elaad-Adar/shift-happens) by Elaad Adar inspired the tray and keyboard shortcut workflow.

See [third-party attribution](offline/ATTRIBUTION.md) for data sources and license details.

# Nikud · נִקּוּד

Add vowel points to a Hebrew word or sentence without sending the text off your computer. On Windows, select text in Word or another editor and use a keyboard shortcut to add nikud in place. On macOS, use the local browser interface.

[Download the latest release](https://github.com/dviradar5/Nikud/releases/latest)

## Windows

1. Download **Nikud-Offline-Windows.zip** for Windows x64. Extract the entire ZIP and open **Nikud.exe**. Keep the `NikudEngine` folder beside it.
2. Settings shows the active shortcut. The default is **Ctrl+Shift+J**. To change it, click **Vocalize selection** and press a combination that includes **Ctrl**, **Alt**, or **Win**.
3. Select an editable word or sentence and press the shortcut. If a reading needs review, choose it in the popup and select **Apply**. **Cancel** leaves the original selection as typed.

Closing Settings leaves Nikud in the notification area. Double-click **נִ**, sometimes under **^**, to reopen Settings. The tray menu also has **Open text editor** for typing or pasting text directly. Right-click the icon and choose **Exit** before replacing an older installation.

The shortcut uses standard Windows copy and paste. Keep the selection active while Nikud works. Some apps block simulated keystrokes, particularly when they run as administrator. If the shortcut fails, check the last status in Settings and try the built-in editor with `ספר`.

## macOS

Starting from a Git clone or GitHub's **Code → Download ZIP**? See [macos/](macos/) for the source launcher. The release ZIP can be run with the commands below.

Download **Nikud-Offline-macOS.zip**, extract it, and install [Python 3.12](https://www.python.org/downloads/). In Terminal, change to the extracted `nikud` folder and run:

```sh
python3.12 -m venv .venv
.venv/bin/python -m pip install -r offline/requirements.txt
.venv/bin/python offline/app.py
```

The first two commands set up the engine once and require an internet connection. After that, run only the last command. Nikud opens a browser tab on your computer. Paste the Hebrew text, select **Add nikud**, then **Copy result**. Keep Terminal open while you use it. Press **Ctrl+C** there to stop it.

## Readings and spelling

Try `ספר` by itself. You can choose **סֵפֶר** (written work), **סַפָּר** (hairdresser), or **סָפַר** (counted), among other readings. In `קראתי ספר אתמול.`, the sentence provides context for **סֵפֶר**. The app suggests the contextual reading and displays available alternatives.

- **Whole text.** The app preserves your Hebrew letters, spacing, punctuation, and surrounding text. Words that already carry Hebrew marks stay as entered. If standard pointed spelling requires adding or removing a letter, Nikud keeps the original spelling.
- **Dagesh.** Nakdimon predicts dagesh along with the vowels. Nikud also checks a missing dagesh in בג״ד כפ״ת after a sheva against the lexicon. It restores the dot when exactly one matching pointed form supports it and all other marks agree. Forms with a sheva and a following undotted letter, such as **מַלְכֵי**, can be valid.
- **Coverage.** The model can misread a word, and lexicon coverage varies with spelling and inflection. Review the suggestions when exact vocalization matters.

Nikud runs locally and requires no API key or account.

## Build and test

Install Python 3.12 and the dependencies in `offline/requirements.txt`, then run `python -m unittest discover -s offline`.

On Windows, build the tray executable from the repository root with `./windows/Build.ps1` in PowerShell. The script uses the Windows .NET Framework compiler. To bundle the worker, install PyInstaller into the Python environment and run the following from `offline`:

```sh
python -m PyInstaller --onedir --name NikudEngine --collect-data nakdimon --add-data "heb_voc.tsv:." vocalize.py
```

Place the resulting `dist/NikudEngine` folder beside `Nikud.exe`.

## Credits

- [Nakdimon](https://github.com/elazarg/nakdimon) by Elazar Gershuni and Yuval Pinter provides the Hebrew diacritization model. [Research paper](https://aclanthology.org/2022.findings-naacl.75/)
- [UniMorph Hebrew](https://github.com/unimorph/heb) provides pointed word forms, lemmas, and grammatical tags. The data comes from Wiktionary and was annotated by Omer Goldman. Short glosses for common ambiguous words are written for Nikud.
- [Shift Happens](https://github.com/Elaad-Adar/shift-happens) by Elaad Adar inspired the tray and keyboard shortcut workflow.

See [third-party attribution](offline/ATTRIBUTION.md) for data sources and license details. Problems and suggestions are welcome in [GitHub Issues](https://github.com/dviradar5/Nikud/issues).

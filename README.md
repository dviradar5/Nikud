# Nikud

A Hebrew vocalizer for Windows and macOS. It runs [Nakdimon](https://github.com/elazarg/nakdimon) locally, with alternate readings from UniMorph and a small glossary. It requires no API key, subscription, or per-use payment. Your text stays on your computer.

Download the ZIP for your operating system from [GitHub Releases](https://github.com/dviradar5/Nikud/releases/latest).

## Start the app

### Windows

1. Exit any older Nikud from the tray. Extract the entire Windows ZIP, then double-click **Nikud.exe**. Keep the `NikudEngine` folder beside it. The model and its runtime are bundled for Windows x64.
2. **Nikud Settings** opens and shows your active shortcut. The default is **Ctrl+Shift+J**. Look for the **נִ** icon near the clock or under the **^** button to reopen Settings.
3. In another app, highlight a Hebrew word or sentence and press the shortcut shown in Settings. Nikud replaces the selection in place.
4. If multiple readings are available, choose the intended reading in the popup and select **Apply**. **Cancel** leaves the selection as typed.

To change the shortcut, click the **Vocalize selection** field and press a combination with **Ctrl**, **Alt**, or **Win**. You can enable **Start with Windows** in the same window. Closing Settings keeps Nikud in the tray. To quit, right-click its icon and choose **Exit**.

Keep the selection in place while Nikud processes it. The Windows shortcut works in editable fields that accept standard copy and paste. Nikud uses the Windows clipboard temporarily and restores common clipboard formats. Windows can block the shortcut from sending keys to apps running with elevated privileges.

If a shortcut fails, check the last status in **Nikud Settings** and try **Open text editor**. Enter `שלום` and select **Add nikud** to check the bundled engine independently. The text editor is also available from the tray menu.

### macOS

Install [Python 3.12](https://www.python.org/downloads/) and extract the macOS ZIP. Open Terminal, type `cd ` (with a trailing space), drag the extracted `nikud` folder into Terminal, and press Return. Run these once to install the local engine:

```sh
python3.12 -m venv .venv
.venv/bin/python -m pip install -r offline/requirements.txt
```

To open Nikud, run:

```sh
.venv/bin/python offline/app.py
```

The launcher opens your browser. Keep the terminal window open while using Nikud. Press Ctrl+C there to quit. If the browser doesn't open automatically, open the local address printed in the terminal.

macOS needs internet access for the one-time dependency installation. The app uses a local browser tab to paste, add nikud, and copy the result.

## Add nikud in the macOS browser tab

1. Paste or type Hebrew text and select **Add nikud**.
2. When context supports a reading, the app uses it. Click an underlined word to review alternatives.
3. Select **Copy result** and paste it wherever you need it.

Try `ספר` by itself for choices including סֵפֶר (book), סַפָּר (barber), and סָפַר (counted). Try `קראתי ספר אתמול.` for the reading סֵפֶר in context.

The result preserves the Hebrew letters, spacing, punctuation, and surrounding text. Already pointed words stay as entered. Nakdimon predicts one reading from context. Other readings are available when the spelling appears in the bundled lexicon or glossary, so coverage varies by word.

## Verify

Run `python -m unittest discover -s offline` after installing the offline requirements.

To rebuild the Windows executable, run `./windows/Build.ps1` in PowerShell from the project folder. To freeze the worker, install PyInstaller into the Python 3.12 environment and run the following from `offline`. Copy the resulting `dist/NikudEngine` directory beside `Nikud.exe`.

```
python -m PyInstaller --onedir --name NikudEngine --collect-data nakdimon --add-data "heb_voc.tsv:." vocalize.py
```

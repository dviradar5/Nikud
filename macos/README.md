# Nikud on macOS

Install [Python 3.12](https://www.python.org/downloads/), then run this from the repository root or the `nikud` folder extracted from the release ZIP:

```sh
bash macos/start.sh
```

The first run creates a local `.venv` and downloads the dependencies. Later runs open Nikud in your browser and process text offline. Keep Terminal open while you use Nikud. Press **Ctrl+C** there to stop it.

Paste Hebrew text into the editor and select **Add or remove nikud**. When every Hebrew word in the input already has marks, Nikud removes them. Select **Copy result** to use the output in another app.

The browser interface is in [index.html](../index.html). The local server, Hebrew vocalizer, and lexicon are in [offline/](../offline/).

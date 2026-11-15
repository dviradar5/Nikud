#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")/.."
python=.venv/bin/python
requirements=offline/requirements.txt
installed=.venv/nikud-requirements.txt

if [[ ! -x "$python" ]]; then
    if ! command -v python3.12 >/dev/null 2>&1; then
        printf '%s\n' 'Install Python 3.12 from https://www.python.org/downloads/ and run this again.' >&2
        exit 1
    fi
    python3.12 -m venv .venv
fi

if ! cmp -s "$requirements" "$installed"; then
    "$python" -m pip install -r "$requirements"
    cp "$requirements" "$installed"
fi

exec "$python" offline/app.py

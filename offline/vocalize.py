"""Serve one local niqqud request over UTF-8 stdin and stdout."""

import json
import re
import sys
import unicodedata
from collections import defaultdict
from functools import cache
from pathlib import Path

from onnxruntime import InferenceSession, SessionOptions
from nakdimon.config import MAIN_MODEL
from nakdimon.predict import predict


DATA = Path(__file__).resolve().parent
MARKS = r"\u05b0-\u05bd\u05bf\u05c1\u05c2\u05c7"
ACCENTS = r"\u0591-\u05af\u05c4\u05c5"
WORD = re.compile(rf"[א-ת](?:[א-ת{MARKS}{ACCENTS}]|['\"׳״](?=[א-ת]))*")
LETTER = re.compile(rf"[א-ת][{MARKS}]*")
REMOVE_MARKS = re.compile(rf"[{MARKS}{ACCENTS}]")
MEANINGS = {
    "ספר": {"סֵפֶר": "דבר שקוראים", "סַפָּר": "מי שמספר שיער", "סָפַר": "מָנָה", "סְפָר": "אזור גבול", "סַפֵּר": "ספר סיפור"},
    "בקר": {"בֹּקֶר": "תחילת היום", "בָּקָר": "פרות ושוורים", "בִּקֵּר": "ערך ביקור", "בַּקֵּר": "הוראה לבקר"},
    "עבר": {"עָבַר": "הלך ממקום למקום או חלף", "עָבָר": "זמן קודם", "עֵבֶר": "צד"},
    "דבר": {"דָּבָר": "עניין או חפץ", "דִּבֵּר": "אמר מילים", "דַּבֵּר": "הוראה לדבר", "דֶּבֶר": "מחלה"},
    "שם": {"שֵׁם": "כינוי של אדם או דבר", "שָׁם": "במקום ההוא", "שָׂם": "הניח"},
    "שלח": {"שָׁלַח": "העביר אל יעד", "שְׁלַח": "הוראה לשלוח"},
}


def plain(text):
    return REMOVE_MARKS.sub("", text)


def form_key(text):
    return unicodedata.normalize("NFC", text).replace("\u05c7", "\u05b8")


@cache
def model_session():
    options = SessionOptions()
    options.intra_op_num_threads = 2
    options.inter_op_num_threads = 1
    return InferenceSession(str(MAIN_MODEL), sess_options=options, providers=["CPUExecutionProvider"])


@cache
def lexicon():
    entries = defaultdict(dict)
    with (DATA / "heb_voc.tsv").open(encoding="utf-8") as source:
        for line in source:
            columns = line.rstrip("\n").split("\t")
            if len(columns) != 3:
                continue
            lemma, form, _ = columns
            if WORD.fullmatch(form):
                meaning = f"צורה של {lemma}" if form_key(lemma) != form_key(form) else ""
                entries[plain(form)].setdefault(form_key(form), (form, meaning))
    for word, forms in MEANINGS.items():
        for form, meaning in forms.items():
            entries[word][form_key(form)] = (form, meaning)
    return entries


def readings(text):
    words = WORD.findall(text)
    if not words:
        raise ValueError("Enter at least one Hebrew word.")
    unpointed = plain(text)
    predicted = LETTER.findall(predict(unpointed, model=model_session(), maxlen=len(unpointed) + 1))
    original = LETTER.findall(unpointed)
    if [item[0] for item in predicted] != original:
        raise ValueError("The vocalizer could not align the result. Try a shorter selection.")
    letters = iter(predicted)
    result = []
    dictionary = lexicon()
    for word in words:
        model = LETTER.sub(lambda _match: next(letters), plain(word))
        if re.search(rf"[{MARKS}{ACCENTS}]", word):
            options = [(word, "")]
            preferred = 0
        else:
            candidates = dictionary.get(word, {}).copy()
            model_key = form_key(model)
            if model_key in candidates or (model != word and (len(words) > 1 or word not in MEANINGS)) or not candidates:
                _, meaning = candidates.pop(model_key, (model, ""))
                options = [(model, meaning), *candidates.values()]
                preferred = 0
            else:
                options = list(candidates.values())
                preferred = -1
            if len(words) == 1 and len(options) > 1:
                preferred = -1
        result.append({
            "preferred": preferred,
            "readings": [{"text": form, "meaning": meaning} for form, meaning in options],
        })
    return result


def main():
    try:
        text = json.loads(sys.stdin.buffer.read(16000))["text"]
        if not isinstance(text, str) or not 0 < len(text) <= 1500:
            raise ValueError("Enter up to 1,500 characters of Hebrew text.")
        reply = {"words": readings(text)}
    except ValueError as error:
        reply = {"error": str(error)}
    sys.stdout.buffer.write(json.dumps(reply, ensure_ascii=False).encode("utf-8"))


if __name__ == "__main__":
    main()

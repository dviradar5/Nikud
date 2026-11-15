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
    "ספר": {
        "סֵפֶר": "יצירה כתובה",
        "סַפָּר": "מעצב שיער",
        "סָפַר": "מָנָה פריטים",
        "סְפָר": "אזור גבול",
        "סַפֵּר": "ציווי של סִפֵּר",
        "סִפֵּר": "תיאר במילים",
        "סְפֹר": "ציווי של סָפַר",
    },
    "בקר": {"בֹּקֶר": "ראשית היום", "בָּקָר": "פרות ושוורים", "בִּקֵּר": "ערך ביקור או מתח ביקורת", "בַּקֵּר": "ציווי של בִּקֵּר"},
    "עבר": {"עָבַר": "חלף או התקדם ממקום למקום", "עָבָר": "הזמן שקדם להווה", "עֵבֶר": "צד או כיוון"},
    "דבר": {"דָּבָר": "עניין או חפץ", "דִּבֵּר": "התבטא בדיבור", "דַּבֵּר": "ציווי של דִּבֵּר", "דֶּבֶר": "מחלה זיהומית"},
    "שם": {"שֵׁם": "כינוי של אדם או דבר", "שָׁם": "במקום ההוא", "שָׂם": "הניח"},
    "שלח": {"שָׁלַח": "העביר אל יעד", "שְׁלַח": "ציווי של שָׁלַח"},
    "שלום": {"שָׁלוֹם": "ברכת מפגש או מצב של פיוס"},
    "מכתב": {"מִכְתָּב": "מסר כתוב לנמען"},
}
PARTS_OF_SPEECH = {
    "N": "שם עצם", "V": "פועל", "ADJ": "שם תואר", "ADV": "תואר הפועל",
    "PRO": "כינוי", "ADP": "מילת יחס", "NUM": "שם מספר",
}
SHVA = "\u05b0"
DAGESH = "\u05bc"
BEGED_KEFET = "בגדכךפףת"


def plain(text):
    return REMOVE_MARKS.sub("", text)


def form_key(text):
    return unicodedata.normalize("NFC", text).replace("\u05c7", "\u05b8")


def after_shva_signature(form):
    form = form_key(form)
    previous_end = 0
    previous = ""
    chunks = []
    for match in LETTER.finditer(form):
        letter = match.group()
        separator = match.string[previous_end:match.start()]
        if not separator and SHVA in previous and letter[0] in BEGED_KEFET:
            letter = letter.replace(DAGESH, "")
        chunks.extend((separator, letter))
        previous = match.group()
        previous_end = match.end()
    return "".join(chunks) + form[previous_end:]


def restore_lexical_dagesh(model, candidates):
    key = form_key(model)
    if SHVA not in key or key in candidates:
        return model
    signature = after_shva_signature(key)
    matches = [form for candidate, (form, _) in candidates.items()
               if after_shva_signature(candidate) == signature]
    if len(matches) == 1:
        old = LETTER.findall(model)
        new = LETTER.findall(matches[0])
        if matches[0].count(DAGESH) > model.count(DAGESH) and all(
            DAGESH not in before or DAGESH in after for before, after in zip(old, new, strict=True)
        ):
            return matches[0]
    return model


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
            lemma, form, tags = columns
            if WORD.fullmatch(form):
                part = PARTS_OF_SPEECH.get(re.split(r"[.;]", tags, maxsplit=1)[0], "")
                meaning = f"נטייה של {lemma}" if form_key(lemma) != form_key(form) else part
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
        elif word == "אתמול":
            options = [("אֶתְמוֹל", "היום שלפני היום הנוכחי")]
            preferred = 0
        else:
            candidates = dictionary.get(word, {}).copy()
            model = restore_lexical_dagesh(model, candidates)
            model_key = form_key(model)
            if model_key in candidates or (model != word and len(words) > 1) or not candidates:
                _, meaning = candidates.pop(model_key, (model, ""))
                options = [(model, meaning), *candidates.values()]
                preferred = 0
            else:
                options = list(candidates.values())
                preferred = 0 if len(options) == 1 else -1
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

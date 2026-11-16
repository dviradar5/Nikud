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

from biblical_forms import FORMS as BIBLICAL_FORMS
from compounds import COMPOUNDS
from vocabulary import GLOSSES


DATA = Path(__file__).resolve().parent
MARKS = r"\u05b0-\u05bd\u05bf\u05c1\u05c2\u05c7"
ACCENTS = r"\u0591-\u05af\u05c4\u05c5"
WORD = re.compile(rf"[א-ת](?:[א-ת{MARKS}{ACCENTS}]|['\"׳״](?=[א-ת]))*")
LETTER = re.compile(rf"[א-ת][{MARKS}]*")
REMOVE_MARKS = re.compile(rf"[{MARKS}{ACCENTS}]")
COMPOUND_GAP = re.compile(r"(?:[^\S\r\n]+|[^\S\r\n]*[־-][^\S\r\n]*)")
MEANINGS = {
    "אשה": {"אִשָּׁה": "אדם בוגר ממין נקבה"},
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
    "אכל": {"אֹכֶל": "מזון"},
    "בית": {"בַּיִת": "מקום מגורים", "בֵּית": "צורת הנסמך של בַּיִת"},
    "בערב": {"בָּעֶרֶב": "בשעות הערב", "בְּעֶרֶב": "בערב של יום או אירוע מסוים"},
    "בחול": {"בַּחוֹל": "בחול מסוים או ביום חול", "בְּחוֹל": "בחול שאינו מיודע"},
    "בעיר": {"בָּעִיר": "בעיר מסוימת", "בְּעִיר": "בעיר שאינה מיודעת"},
    "הדף": {"הַדַּף": "עמוד או גיליון נייר מיודע", "הָדַף": "דחק לאחור"},
    "לגנה": {"לַגִּנָּה": "אל הגינה", "לְגִנָּה": "אל גינה", "לְגִנָּהּ": "אל הגינה שלה"},
}
PREDICATE_FORMS = {
    "יפה": ("יָפֶה", "יָפָה"),
    "רוצה": ("רוֹצֶה", "רוֹצָה"),
    "שותה": ("שׁוֹתֶה", "שׁוֹתָה"),
    "קונה": ("קוֹנֶה", "קוֹנָה"),
    "בוכה": ("בּוֹכֶה", "בּוֹכָה"),
    "עושה": ("עוֹשֶׂה", "עוֹשָׂה"),
}
SUBJECTS = {"את": 1, "היא": 1, "אתה": 0, "הוא": 0}
PREDICATE_PATTERN = re.compile(
    r"(?:^|[.!?׃;])\s*(?P<subject>את|אתה|היא|הוא)\s+(?:הכי\s+)?(?P<predicate>"
    + "|".join(PREDICATE_FORMS) + r")(?![א-ת])"
)
# Common spellings reviewed against the number tables and standard pointed forms.
REVIEWED_READINGS = {
    "אישה": {
        "אִישָּׁה": ("אדם בוגר ממין נקבה", "בת זוג נשואה"),
        "אִישָׁהּ": "בעלה",
    },
    "את": {
        "אֶת": "מילית לפני מושא ישיר מיודע",
        "אֵת": ("כלי חפירה", "מילית לפני מושא ישיר במקרא"),
        "אַתְּ": "כינוי גוף שני בנקבה יחידה",
    },
    "קורה": {
        "קוֹרֶה": "מתרחש",
        "קוֹרָה": ("קורת עץ או רכיב נושא במבנה", "מתרחשת", "כוסה בגג"),
        "קוֹרָהּ": "הקור שלה",
    },
    "הכי": {"הֲכִי": "ביותר או מילת שאלה במקרא", "הָכִי": "כך בארמית"},
    "ללכת": {"לָלֶכֶת": "שם הפועל של הָלַךְ"},
    "אפס": {"אֶפֶס": "המספר 0", "אָפַס": "תַּם או אזל", "אִפֵּס": "החזיר לערך אפס"},
    "אחת": {"אַחַת": "המספר 1 בנקבה"},
    "שתים": {"שְׁתַּיִם": "המספר 2 בנקבה", "שְׁתֵּים": "הצורה שבצירוף שתים עשרה"},
    "שתיים": {"שְׁתַּיִים": "המספר 2 בנקבה"},
    "שלוש": {"שָׁלוֹשׁ": "המספר 3 בנקבה", "שְׁלוֹשׁ": "צורת הנסמך של שָׁלוֹשׁ"},
    "שלושה": {"שְׁלוֹשָׁה": "המספר 3 בזכר"},
    "שלשה": {"שְׁלֹשָׁה": "המספר 3 בזכר"},
    "ארבע": {"אַרְבַּע": "המספר 4 בנקבה"},
    "ארבעה": {"אַרְבָּעָה": "המספר 4 בזכר"},
    "חמש": {
        "חָמֵשׁ": "המספר 5 בנקבה",
        "חֲמֵשׁ": "צורת הנסמך של חָמֵשׁ",
        "חִמֵּשׁ": "צייד בנשק",
        "חַמֵּשׁ": "ציווי של חִמֵּשׁ",
        "חֻמַּשׁ": "צוּיַּד בנשק",
    },
    "חמשה": {"חֲמִשָּׁה": "המספר 5 בזכר"},
    "חמישה": {"חֲמִישָּׁה": "המספר 5 בזכר"},
    "שש": {"שֵׁשׁ": "המספר 6 בנקבה", "שָׂשׂ": "שָׂמַח"},
    "שבע": {"שֶׁבַע": "המספר 7 בנקבה", "שְׁבַע": "צורת הנסמך של שֶׁבַע", "שָׂבֵעַ": "שאכל די צורכו", "שָׂבַע": "אכל די צורכו"},
    "שמונה": {"שְׁמוֹנֶה": "המספר 8 בנקבה", "שְׁמוֹנָה": "המספר 8 בזכר", "שֶׁמֻּנָּה": "שקיבל מינוי"},
    "תשע": {"תֵּשַׁע": "המספר 9 בנקבה", "תְּשַׁע": "צורת הנסמך של תֵּשַׁע"},
    "עשר": {"עֶשֶׂר": "המספר 10 בנקבה", "עָשָׂר": "הרכיב השני במספרים 11–19 בזכר"},
    "מאה": {"מֵאָה": "המספר 100"},
    "אלף": {"אֶלֶף": "המספר 1000", "אִלֵּף": "הרגיל או אימן", "אַלֵּף": "ציווי של אִלֵּף"},
    "אתמול": {"אֶתְמוֹל": "היום שלפני היום הנוכחי"},
    "מחר": {"מָחָר": "היום שלאחר היום הנוכחי"},
    "עכשיו": {"עַכְשָׁיו": "בעת הזאת"},
    "שלום": {"שָׁלוֹם": "ברכת מפגש או מצב של פיוס"},
    "תפוח": {"תַּפּוּחַ": "פרי עץ התפוח", "תָּפוּחַ": "שהתנפח"},
    "פתוח": {"פָּתוּחַ": "שאינו סגור"},
    "גבוה": {"גָּבוֹהַּ": "שגובהו רב"},
    "מכתב": {"מִכְתָּב": "מסר כתוב לנמען"},
    "כפתור": {"כַּפְתּוֹר": "אביזר רכיסה או לחצן"},
    "בוקר": {"בֹּקֶר": "ראשית היום", "בּוֹקֵר": "רועה בקר"},
    "אוכל": {"אֹכֶל": "מזון", "אוֹכֵל": "צורך מזון", "אוּכַל": "אהיה מסוגל"},
}
REVIEWED_CONTEXT = {
    ("אישה", "אחת"): "אִישָּׁה",
    ("חמש", "דקות"): "חָמֵשׁ",
    ("חמש", "הדקות"): "חֲמֵשׁ",
    ("חמש", "עשרה"): "חֲמֵשׁ",
    ("מספר", "גדול"): "מִסְפָּר",
    ("מספר", "קטן"): "מִסְפָּר",
    ("מספר", "ראשוני"): "מִסְפָּר",
    ("מספר", "הטלפון"): "מִסְפַּר",
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


def predicate_suggestions(text):
    """Use agreement in short clauses with an explicit personal pronoun."""
    suggestions = {}
    for clause in PREDICATE_PATTERN.finditer(text):
        subject = clause["subject"]
        chosen = PREDICATE_FORMS[clause["predicate"]][SUBJECTS[subject]]
        suggestions[clause.start("predicate")] = chosen
        if subject == "את":
            suggestions[clause.start("subject")] = "אַתְּ"
    return suggestions


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
                _, meanings = entries[plain(form)].setdefault(form_key(form), (form, {}))
                if meaning or not meanings:
                    if meaning:
                        meanings.pop("", None)
                    meanings[meaning] = None
    for word, forms in MEANINGS.items():
        for form, meaning in forms.items():
            entries[word][form_key(form)] = (form, {meaning: None})
    for word, forms in GLOSSES.items():
        for form, meanings in forms.items():
            labels = (meanings,) if isinstance(meanings, str) else meanings
            entries[word][form_key(form)] = (form, dict.fromkeys(labels))
    return entries


def expand_readings(groups):
    return [(form, meaning) for form, meanings in groups for meaning in meanings]


def letter_class(letter):
    # Keep matres and gutturals distinct. Dagesh-sensitive and final letters
    # also have different behavior from ordinary consonants.
    if letter in "אהחעויורםןץ":
        return letter
    if letter in BEGED_KEFET:
        return letter if letter in "ךף" else "ב"
    return "ש" if letter == "ש" else "צ"


@cache
def word_patterns():
    patterns = defaultdict(list)
    for spelling, readings_by_form in lexicon().items():
        if not re.fullmatch("[א-ת]{3,8}", spelling):
            continue
        signature = tuple(map(letter_class, spelling))
        for form, _ in readings_by_form.values():
            parts = tuple(LETTER.findall(form_key(form)))
            if len(parts) == len(spelling):
                patterns[signature].append((spelling, parts))
    return patterns


def inferred_options(word, model):
    """Offer spelling-preserving guesses, ranked by context and lexical similarity."""
    if not re.fullmatch("[א-ת]{3,8}", word):
        return []
    model_parts = LETTER.findall(form_key(model))
    alternatives = {}
    if word[0] in "הובכלמש" and len(word) > 3 and model_parts[0][1:]:
        for stem, _ in lexicon().get(word[1:], {}).values():
            candidate = model_parts[0] + stem
            if form_key(candidate) != form_key(model):
                alternatives[candidate] = (0, 0, candidate)
    for spelling, parts in word_patterns().get(tuple(map(letter_class, word)), ()):
        shared = sum(a == b for a, b in zip(word, spelling))
        if shared < len(word) - 2:
            continue
        endings = tuple(part[1:] for part in parts)
        candidate = "".join(letter + marks for letter, marks in zip(word, endings))
        if form_key(candidate) == form_key(model):
            continue
        distance = sum(part[1:] != marks for part, marks in zip(model_parts, endings))
        score = (1 + distance, -shared, candidate)
        alternatives[candidate] = min(alternatives.get(candidate, score), score)
    return [(form, "השערה לפי תבנית דומה") for form in sorted(alternatives, key=alternatives.get)[:2]]


def readings(text):
    spans = list(WORD.finditer(text))
    words = [match.group() for match in spans]
    if not words:
        raise ValueError("Enter at least one Hebrew word.")
    unpointed = plain(text)
    model_input = unpointed
    if len(words) > 1 and not unpointed.rstrip(" \t\r\n\"׳״”’)]").endswith((".", "!", "?", "׃", "…")):
        # The model can confuse a selection boundary with the middle of a word.
        model_input = unpointed.rstrip() + "."
    predicted = LETTER.findall(predict(model_input, model=model_session(), maxlen=len(model_input) + 1))
    original = LETTER.findall(unpointed)
    if [item[0] for item in predicted] != original:
        raise ValueError("The vocalizer could not align the result. Try a shorter selection.")
    letters = iter(predicted)
    result = []
    dictionary = lexicon()
    suggestions = predicate_suggestions(text)
    for index, word in enumerate(words):
        model = LETTER.sub(lambda _match: next(letters), plain(word))
        model = suggestions.get(spans[index].start(), model)
        if index + 1 < len(words):
            gap = text[spans[index].end():spans[index + 1].start()]
            if re.fullmatch(r"[\s־-]+", gap):
                model = REVIEWED_CONTEXT.get((word, plain(words[index + 1])), model)
        if re.search(rf"[{MARKS}{ACCENTS}]", word):
            options = [(word, "")]
            preferred = 0
        elif word in REVIEWED_READINGS or word in PREDICATE_FORMS:
            candidates = (REVIEWED_READINGS[word] if word in REVIEWED_READINGS else
                          dict(zip(PREDICATE_FORMS[word], ("צורת הזכר", "צורת הנקבה"), strict=True)))
            options = expand_readings((form, (meaning,) if isinstance(meaning, str) else meaning)
                                      for form, meaning in candidates.items())
            preferred = 0 if len(options) == 1 else -1
            if len(words) > 1:
                preferred = next((i for i, (form, _) in enumerate(options)
                                  if form_key(form) == form_key(model)), preferred)
                if preferred > 0:
                    options.insert(0, options.pop(preferred))
                    preferred = 0
        else:
            candidates = dictionary.get(word, {}).copy()
            if not candidates:
                candidates = {form_key(form): (form, ("צורה מקראית",))
                              for form in BIBLICAL_FORMS.get(word, ())}
            model = restore_lexical_dagesh(model, candidates)
            model_key = form_key(model)
            if word in GLOSSES and model_key not in candidates and len(words) > 1:
                # Accept a uniquely attested reading when only the final letter's
                # marks differ. This preserves legitimate final marks in the lexicon.
                letter_keys = LETTER.findall(model_key)
                stem = "".join(letter_keys[:-1])
                matches = [key for key in candidates if "".join(LETTER.findall(key)[:-1]) == stem]
                if len(matches) == 1:
                    model_key = matches[0]
                    model = candidates[model_key][0]
            allow_model_only = (word not in GLOSSES and model != word and len(words) > 1)
            if not candidates:
                options = [(model, "הצעת המודל")] + inferred_options(word, model)
                preferred = 0
            elif model_key in candidates or allow_model_only:
                _, meanings = candidates.pop(model_key, (model, ("",)))
                options = expand_readings([(model, meanings), *candidates.values()])
                preferred = 0
            else:
                options = expand_readings(candidates.values())
                preferred = 0 if len(candidates) == 1 else -1
            if len(words) == 1 and len(options) > 1:
                preferred = -1
        result.append({
            "preferred": preferred,
            "readings": [{"text": form, "meaning": meaning} for form, meaning in options],
        })
    previous_end = -1
    for index, (left, right) in enumerate(zip(spans, spans[1:])):
        if index == previous_end:
            continue
        entry = COMPOUNDS.get((left.group(), right.group()))
        if entry and COMPOUND_GAP.fullmatch(text[left.end():right.start()]):
            phrase, meaning = entry
            result[index]["compound"] = {"parts": phrase.split(), "meaning": meaning}
            previous_end = index + 1
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

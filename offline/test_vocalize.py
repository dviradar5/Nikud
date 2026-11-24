import unittest
from unittest.mock import patch

from app import for_browser
from biblical_forms import FORMS as BIBLICAL_FORMS
from compounds import COMPOUNDS
from vocabulary import GLOSSES
from vocalize import form_key, plain, readings, restore_lexical_dagesh


class VocalizeTests(unittest.TestCase):
    @patch("vocalize.model_session", side_effect=AssertionError("Model should not load"))
    @patch("vocalize.predict", side_effect=AssertionError("Prediction should not run"))
    def test_shomer_offers_all_three_readings_without_loading_the_model(self, _predict, _session):
        result = readings("שומר")[0]
        self.assertEqual(result["preferred"], -1)
        self.assertEqual({form_key(option["text"]) for option in result["readings"]},
                         {form_key(form) for form in ("שׁוֹמֵר", "שׁוּמָר", "שׁוּמֵר")})
        self.assertTrue(all(option["meaning"] for option in result["readings"]))

    def test_shomer_keeps_all_readings_in_sentence_context(self):
        for text, index in (("השומר שומר על השער", 1), ("קניתי שומר בשוק", 1),
                            ("למדתי על שומר העתיקה", 2)):
            with self.subTest(text=text):
                result = readings(text)[index]
                self.assertLessEqual({form_key(form) for form in ("שׁוֹמֵר", "שׁוּמָר", "שׁוּמֵר")},
                                     {form_key(option["text"]) for option in result["readings"]})

    def test_words_without_lexicon_entries_offer_only_the_model_reading(self):
        for word, expected in zip(readings("הילד החמוד"), ("הַיֶּלֶד", "הַחָמוּד"), strict=True):
            self.assertEqual(word["preferred"], 0)
            self.assertEqual(len(word["readings"]), 1)
            self.assertEqual(form_key(word["readings"][0]["text"]), form_key(expected))
            self.assertEqual(word["readings"][0]["meaning"], "הצעת המודל")

    def test_all_compounds_keep_spelling_and_appear_in_sentences(self):
        for spelling, (pointed, meaning) in COMPOUNDS.items():
            with self.subTest(spelling=spelling):
                self.assertEqual(tuple(plain(pointed).split()), spelling)
                result = readings("במשפט נכתב " + " ".join(spelling) + " במפורש.")
                self.assertEqual(result[2]["compound"], {"parts": pointed.split(), "meaning": meaning})
                self.assertNotIn("compound", result[3])

    def test_compounds_keep_individual_readings_and_pass_through_to_browser(self):
        house, school = for_browser("בית־ספר")
        self.assertEqual(house["compound"]["parts"], ["בֵּית", "סֵפֶר"])
        self.assertIn(form_key("בַּיִת"), {form_key(option["text"]) for option in house["choices"]})
        self.assertIn(form_key("סַפָּר"), {form_key(option["text"]) for option in school["choices"]})
        self.assertEqual(for_browser("זה בית ספרי")[1]["compound"]["parts"], ["בֵּית", "סִפְרִי"])
        self.assertEqual(for_browser("קניתי מוסר ים")[1]["compound"]["parts"], ["מוּסַר", "יָם"])

    def test_compounds_do_not_cross_punctuation_or_repoint_existing_marks(self):
        for text in ("בית, ספר", "בית\nספר", "בַּיִת ספר", "בֵּית סֵפֶר"):
            with self.subTest(text=text):
                self.assertNotIn("compound", for_browser(text)[0])

    def test_full_spelling_of_isha_keeps_both_readings_in_isolation(self):
        word = readings("אישה")[0]
        self.assertEqual(word["preferred"], -1)
        self.assertEqual({form_key(option["text"]) for option in word["readings"]},
                         {form_key("אִישָּׁה"), form_key("אִישָׁהּ")})
        self.assertEqual(len(word["readings"]), 3)

    def test_added_vocabulary_preserves_the_spelling(self):
        for entries in (GLOSSES, BIBLICAL_FORMS):
            for spelling, forms in entries.items():
                for form in forms:
                    self.assertEqual(plain(form), spelling)

    def test_audit_homographs_offer_the_missing_forms_and_glosses(self):
        for spelling, forms in (
            ("מספר", {"מִסְפָּר", "מְסַפֵּר"}),
            ("עצב", {"עֶצֶב", "עָצָב", "עִצֵּב"}),
            ("שר", {"שַׂר", "שָׁר"}),
            ("באר", {"בְּאֵר", "בֵּאֵר"}),
        ):
            with self.subTest(spelling=spelling):
                result = readings(spelling)[0]
                glosses = {form_key(option["text"]) for option in result["readings"] if option["meaning"]}
                self.assertLessEqual({form_key(form) for form in forms}, glosses)
                self.assertEqual(result["preferred"], -1)

    def test_audit_rare_word_and_same_pointing_with_distinct_glosses(self):
        dvir = readings("דביר")[0]
        self.assertEqual(dvir["readings"][dvir["preferred"]]["text"], "דְּבִיר")
        etzba = readings("אצבע")[0]
        self.assertEqual(etzba["preferred"], -1)
        self.assertEqual({option["text"] for option in etzba["readings"]}, {"אֶצְבַּע"})
        self.assertEqual(len({option["meaning"] for option in etzba["readings"]}), 2)

    def test_audit_number_and_storyteller_use_context(self):
        for text, expected in (("זה מספר גדול", "מִסְפָּר"), ("הוא מספר סיפור", "מְסַפֵּר")):
            with self.subTest(text=text):
                word = readings(text)[1]
                self.assertGreaterEqual(word["preferred"], 0)
                self.assertEqual(form_key(word["readings"][word["preferred"]]["text"]), form_key(expected))
                self.assertEqual(len({form_key(option["text"]) for option in word["readings"]}), 3)

    def test_biblical_fallback_offers_an_attested_reading(self):
        word = readings("רפאים")[0]
        self.assertIn({"text": "רְפָאִים", "meaning": "צורה מקראית"}, word["readings"])

    def test_isha_achat_suggests_woman_and_keeps_other_readings(self):
        word = readings("אישה אחת")[0]
        self.assertEqual(form_key(word["readings"][word["preferred"]]["text"]),
                         form_key("אִישָּׁה"))
        self.assertEqual(len(word["readings"]), 3)
    def test_homographs_keep_separate_meanings_in_the_chooser(self):
        word = readings("קורה")[0]
        self.assertEqual(word["preferred"], -1)
        beam_form = [option["meaning"] for option in word["readings"]
                     if form_key(option["text"]) == form_key("קוֹרָה")]
        self.assertIn("קורת עץ או רכיב נושא במבנה", beam_form)
        self.assertIn("מתרחשת", beam_form)
        self.assertIn({"text": "קוֹרָהּ", "meaning": "הקור שלה"}, word["readings"])
        contextual = readings("מה קורה?")[-1]
        self.assertGreaterEqual(contextual["preferred"], 0)
        self.assertEqual(form_key(contextual["readings"][contextual["preferred"]]["text"]),
                         form_key("קוֹרֶה"))

    def test_superlative_suggests_agreement_and_keeps_alternatives(self):
        words = readings("את הכי יפה")
        picked = [form_key(word["readings"][word["preferred"]]["text"]) for word in words]
        self.assertEqual(picked, [form_key(item) for item in ("אַתְּ", "הֲכִי", "יָפָה")])
        self.assertEqual({form_key(item["text"]) for item in words[0]["readings"]},
                         {form_key(item) for item in ("אַתְּ", "אֶת", "אֵת")})
        self.assertEqual({form_key(item["text"]) for item in words[2]["readings"]},
                         {form_key(item) for item in ("יָפָה", "יָפֶה")})

    def test_clause_rule_leaves_medial_direct_object_marker(self):
        word = readings("אני אוהב את הכי יפה")[2]
        self.assertEqual(form_key(word["readings"][word["preferred"]]["text"]), form_key("אֶת"))

    def test_sentence_boundary_preserves_the_text_without_a_period(self):
        words = for_browser("היא שרה שיר")
        self.assertEqual([word["original"] for word in words], ["היא", "שרה", "שיר"])
        self.assertEqual(form_key(words[-1]["choices"][words[-1]["selected"]]["text"]), form_key("שִׁיר"))

    def test_et_offers_three_distinct_readings(self):
        result = readings("את")[0]
        self.assertEqual(result["preferred"], -1)
        self.assertEqual({form_key(item["text"]) for item in result["readings"]},
                         {form_key(word) for word in ("אֶת", "אֵת", "אַתְּ")})
        self.assertEqual({item["meaning"] for item in result["readings"] if item["text"] == "אֵת"},
                         {"כלי חפירה", "מילית לפני מושא ישיר במקרא"})

    def test_et_uses_context_for_a_direct_object_or_feminine_pronoun(self):
        for text, index, expected in (("ראיתי את הכלב.", 1, "אֶת"),
                                      ("את יודעת את התשובה.", 0, "אַתְּ")):
            with self.subTest(text=text):
                word = readings(text)[index]
                self.assertGreaterEqual(word["preferred"], 0)
                self.assertEqual(form_key(word["readings"][word["preferred"]]["text"]), form_key(expected))

    def test_single_word_offers_readings_without_a_default(self):
        result = readings("ספר")[0]
        forms = {form_key(choice["text"]) for choice in result["readings"]}
        self.assertTrue({form_key(word) for word in ("סֵפֶר", "סַפָּר", "סָפַר")} <= forms)
        self.assertEqual(result["preferred"], -1)
        labels = {form_key(choice["text"]): choice["meaning"] for choice in result["readings"]}
        self.assertEqual(labels[form_key("סַפֵּר")], "ציווי של סִפֵּר")

    def test_recovers_known_dagesh_after_shva(self):
        dotted = "נִכְתַּב"
        dictionary = {form_key(dotted): (dotted, "פועל")}
        self.assertEqual(restore_lexical_dagesh("נִכְתַב", dictionary), dotted)
        self.assertEqual(restore_lexical_dagesh("נִכְתֹב", dictionary), "נִכְתֹב")

    def test_preserves_lexicon_form_without_dagesh(self):
        form = "מַלְכֵי"
        dictionary = {form_key(form): (form, "נטייה של מֶלֶךְ")}
        self.assertEqual(restore_lexical_dagesh(form, dictionary), form)
        self.assertEqual(restore_lexical_dagesh(form, {}), form)

    def test_isolated_dictionary_words_use_attested_readings(self):
        for source, expected in (("שלום", "שָׁלוֹם"), ("מכתב", "מִכְתָּב")):
            result = readings(source)[0]
            self.assertEqual(result["preferred"], 0)
            self.assertEqual([form_key(item["text"]) for item in result["readings"]], [form_key(expected)])

    def test_real_model_uses_sentence_context(self):
        book = readings("קראתי ספר אתמול.")[1]
        put = readings("הוא שם את הספר על השולחן.")[1]
        there = readings("יש שם בית גדול.")[1]
        self.assertEqual(form_key(book["readings"][book["preferred"]]["text"]), form_key("סֵפֶר"))
        self.assertEqual(form_key(put["readings"][put["preferred"]]["text"]), form_key("שָׂם"))
        self.assertEqual(form_key(there["readings"][there["preferred"]]["text"]), form_key("שָׁם"))

    def test_yesterday_has_no_final_sheva_with_or_without_punctuation(self):
        for text in ("אתמול", "קראתי ספר אתמול", "קראתי ספר אתמול."):
            with self.subTest(text=text):
                word = readings(text)[-1]
                self.assertEqual(word["preferred"], 0)
                self.assertEqual([form["text"] for form in word["readings"]], ["אֶתְמוֹל"])

    def test_five_offers_the_number_and_never_the_invalid_model_form(self):
        for text, expected in (("חמש", "חָמֵשׁ"), ("חמש דקות", "חָמֵשׁ"),
                               ("חמש הדקות", "חֲמֵשׁ")):
            with self.subTest(text=text):
                word = readings(text)[0]
                forms = [form_key(form["text"]) for form in word["readings"]]
                self.assertEqual(forms[0], form_key(expected))
                self.assertNotIn(form_key("חֲמֵשָׁ"), forms)
                self.assertEqual(word["preferred"], -1 if text == "חמש" else 0)

    def test_masculine_numbers_keep_final_he_unmarked(self):
        for text, expected in (
            ("שלושה", "שְׁלוֹשָׁה"), ("שלשה", "שְׁלֹשָׁה"),
            ("ארבעה", "אַרְבָּעָה"), ("חמישה", "חֲמִישָּׁה"), ("חמשה", "חֲמִשָּׁה"),
        ):
            for source in (text, text + " ילדים"):
                with self.subTest(source=source):
                    word = readings(source)[0]
                    self.assertEqual([form["text"] for form in word["readings"]], [expected])

    def test_browser_utf16_offsets_and_spacing(self):
        source = "📚 קראתי  ספר!\nABC 42"
        words = for_browser(source)
        self.assertEqual([word["start"] for word in words], [3, 10])
        self.assertEqual([word["original"] for word in words], ["קראתי", "ספר"])

    @patch("app.readings")
    def test_browser_removes_marks_from_an_already_pointed_selection(self, model):
        words = for_browser("📚 סֵפֶר, שָׁלוֹם!")
        self.assertEqual([word["start"] for word in words], [3, 10])
        self.assertEqual([word["choices"][0]["text"] for word in words], ["ספר", "שלום"])
        model.assert_not_called()

    def test_browser_keeps_existing_marks_in_mixed_text(self):
        first, second = for_browser("סֵפֶר שלום")
        self.assertEqual(first["choices"][0]["text"], "סֵפֶר")
        suggested = second["choices"][second["selected"]]["text"]
        self.assertEqual(plain(suggested), "שלום")
        self.assertNotEqual(suggested, "שלום")

    def test_mixed_input_and_already_pointed_word(self):
        words = readings("שלום, שָׁלוֹם. ABC 42")
        self.assertTrue(all(plain(item["text"]) == "שלום" for word in words for item in word["readings"]))
        self.assertEqual(words[1]["readings"], [{"text": "שָׁלוֹם", "meaning": ""}])

    @patch("vocalize.predict", return_value="אבג")
    def test_rejects_a_model_result_that_changes_letters(self, _predict):
        with self.assertRaisesRegex(ValueError, "align"):
            readings("שלום עולם")


if __name__ == "__main__":
    unittest.main()

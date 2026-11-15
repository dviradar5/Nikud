import unittest
from unittest.mock import patch

from app import for_browser
from vocalize import form_key, plain, readings, restore_lexical_dagesh


class VocalizeTests(unittest.TestCase):
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
            readings("שלום")


if __name__ == "__main__":
    unittest.main()

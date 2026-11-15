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

    def test_browser_utf16_offsets_and_spacing(self):
        source = "📚 קראתי  ספר!\nABC 42"
        words = for_browser(source)
        self.assertEqual([word["start"] for word in words], [3, 10])
        self.assertEqual([word["original"] for word in words], ["קראתי", "ספר"])

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

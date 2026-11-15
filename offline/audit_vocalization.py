"""Compare Nikud with pointed references, without treating a sample as a benchmark."""

from collections import Counter

from vocalize import WORD, form_key, plain, readings

WORDS = (
    "אֶפֶס", "אַחַת", "שְׁתַּיִם", "שָׁלוֹשׁ", "אַרְבַּע", "חָמֵשׁ",
    "שֵׁשׁ", "שֶׁבַע", "שְׁמוֹנֶה", "תֵּשַׁע", "עֶשֶׂר", "מֵאָה", "אֶלֶף",
    "חֲמִשָּׁה", "מִכְתָּב", "כַּפְתּוֹר", "פָּתוּחַ", "גָּבוֹהַּ",
    "תַּפּוּחַ", "רוּחַ", "שָׁלוֹם", "אֶתְמוֹל", "מָחָר", "עַכְשָׁיו",
    "סֵפֶר", "סַפָּר", "סָפַר", "שֵׁם", "שָׂם", "שָׂבֵעַ", "נִכְתַּב",
    "מַלְכֵי", "אֹכֶל", "אוֹכֵל", "בֹּקֶר", "בּוֹקֵר", "חַמֵּשׁ",
)
SENTENCES = (
    "קָרָאתִי סֵפֶר אֶתְמוֹל", "קָרָאתִי סֵפֶר אֶתְמוֹל.",
    "הוּא סָפַר אֶת הַכֶּסֶף.", "הוּא סִפֵּר לָנוּ עַל הַטִּיּוּל.",
    "הוּא שָׂם אֶת הַסֵּפֶר עַל הַשֻּׁלְחָן.", "יֵשׁ שָׁם בַּיִת גָּדוֹל.",
    "עוֹד חָמֵשׁ דַּקּוֹת.", "חֲמֵשׁ הַדַּקּוֹת הָאַחֲרוֹנוֹת.",
    "אֲנִי נִשְׁאַר עוֹד חָמֵשׁ דַּקּוֹת.", "קִבַּלְתִּי חָמֵשׁ.",
    "הוּא חִמֵּשׁ אֶת הַשּׁוֹמְרִים.", "קָנִיתִי חֲמִשָּׁה סְפָרִים.",
    "בַּבֹּקֶר אָכַלְתִּי תַּפּוּחַ.", "הַחַלּוֹן פָּתוּחַ, וְיֵשׁ רוּחַ.",
    "הַמִּכְתָּב נִכְתַּב בַּבֹּקֶר.", "הַכַּפְתּוֹר נִשְׁבַּר.",
    "מָחָר נֵלֵךְ לַיָּם.", "עַכְשָׁיו אֲנִי אוֹכֵל.",
    "הָאֹכֶל עַל הַשֻּׁלְחָן.", "Word: אֲנִי כּוֹתֵב 5 מִלִּים.",
    "אַתְּ הֲכִי יָפָה", "הוּא הֲכִי יָפֶה", "הִיא יָפָה", "אַתְּ רוֹצָה לָלֶכֶת",
    "הוּא רוֹצֶה לָלֶכֶת", "הִיא שׁוֹתָה מַיִם", "הוּא שׁוֹתֶה מַיִם",
    "אַתָּה קוֹנֶה סֵפֶר", "הִיא קוֹנָה סֵפֶר", "הִיא בּוֹכָה", "הוּא בּוֹכֶה",
    "הוּא עוֹשֶׂה סֵדֶר", "הִיא עוֹשָׂה סֵדֶר", "רָאִיתִי אֶת הַכֶּלֶב",
    "אַתְּ יוֹדַעַת אֶת הַתְּשׁוּבָה", "קָנִיתִי אֵת לַגִּנָּה",
    "הַדֶּרֶךְ קְצָרָה", "כְּבָר שֶׁבַע בָּעֶרֶב", "הוּא רָאָה אֶת הָאוֹר",
    "הִיא שָׁרָה שִׁיר", "הַשַּׂר נִכְנַס לַחֶדֶר", "הַיֶּלֶד שִׂחֵק בַּחוֹל",
    "זֶה קָרָה אֶתְמוֹל", "קָרַעְתִּי אֶת הַדַּף", "הוּא נִשְׁאַר בָּעִיר",
)
# Pointed Masoretic text without cantillation. Sources by chapter:
# https://www.mechon-mamre.org/i/t/t2803.htm
# https://www.mechon-mamre.org/i/t/t2810.htm
# https://www.mechon-mamre.org/i/t/t2815.htm
# https://www.mechon-mamre.org/i/t/t0101.htm
VERSES = {
    "Proverbs 3:2": "כִּי אֹרֶךְ יָמִים, וּשְׁנוֹת חַיִּים, וְשָׁלוֹם יוֹסִיפוּ לָךְ.",
    "Proverbs 3:5": "בְּטַח אֶל־יְהוָה, בְּכָל־לִבֶּךָ; וְאֶל־בִּינָתְךָ, אַל־תִּשָּׁעֵן.",
    "Proverbs 3:7": "אַל־תְּהִי חָכָם בְּעֵינֶיךָ; יְרָא אֶת־יְהוָה, וְסוּר מֵרָע.",
    "Proverbs 3:13": "אַשְׁרֵי אָדָם, מָצָא חָכְמָה; וְאָדָם, יָפִיק תְּבוּנָה.",
    "Proverbs 10:9": "הוֹלֵךְ בַּתֹּם, יֵלֶךְ בֶּטַח; וּמְעַקֵּשׁ דְּרָכָיו, יִוָּדֵעַ.",
    "Proverbs 10:12": "שִׂנְאָה, תְּעֹרֵר מְדָנִים; וְעַל כָּל־פְּשָׁעִים, תְּכַסֶּה אַהֲבָה.",
    "Proverbs 10:20": "כֶּסֶף נִבְחָר, לְשׁוֹן צַדִּיק; לֵב רְשָׁעִים כִּמְעָט.",
    "Proverbs 15:1": "מַעֲנֶה־רַּךְ, יָשִׁיב חֵמָה; וּדְבַר־עֶצֶב, יַעֲלֶה־אָף.",
    "Proverbs 15:4": "מַרְפֵּא לָשׁוֹן, עֵץ חַיִּים; וְסֶלֶף בָּהּ, שֶׁבֶר בְּרוּחַ.",
    "Proverbs 15:18": "אִישׁ חֵמָה, יְגָרֶה מָדוֹן; וְאֶרֶךְ אַפַּיִם, יַשְׁקִיט רִיב.",
    "Genesis 1:13": "וַיְהִי־עֶרֶב וַיְהִי־בֹקֶר, יוֹם שְׁלִישִׁי.",
    "Genesis 1:19": "וַיְהִי־עֶרֶב וַיְהִי־בֹקֶר, יוֹם רְבִיעִי.",
}


def check(name, examples, isolated=False):
    scores = Counter()
    for label, reference in examples:
        source = plain(reference)
        scores["cases"] += 1
        exact = True
        for expected, result in zip(WORD.findall(reference), readings(source), strict=True):
            options = [form_key(option["text"]) for option in result["readings"]]
            preferred = options[result["preferred"]] if result["preferred"] >= 0 else None
            target = form_key(expected)
            available = target in options
            matched = available if isolated else preferred == target
            scores["words"] += 1
            scores["available"] += available
            scores["preferred"] += preferred == target
            exact = exact and matched
            if not matched:
                shown = preferred or ", ".join(options)
                explanation = "reference available in chooser" if available else "reference missing"
                print(f"{label} | expected {target} | got {shown} | {explanation}")
        scores["exact"] += exact
    print(f"{name}: {scores['exact']}/{scores['cases']} exact cases, "
          f"{scores['preferred']}/{scores['words']} preferred tokens, "
          f"{scores['available']}/{scores['words']} tokens available")


if __name__ == "__main__":
    check("Isolated readings", ((plain(word), word) for word in WORDS), isolated=True)
    check("Everyday sentences", ((plain(text), text) for text in SENTENCES))
    check("Bible passages", VERSES.items())

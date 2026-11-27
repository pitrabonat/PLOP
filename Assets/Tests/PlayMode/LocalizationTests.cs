using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    /// <summary>Sprint 02 localization: complete tables, plural rules, live relabelling and the Settings language row.</summary>
    public sealed class LocalizationTests
    {
        private static readonly GameLanguage[] Translated =
        {
            GameLanguage.German, GameLanguage.Ukrainian, GameLanguage.Russian, GameLanguage.French,
            GameLanguage.Spanish, GameLanguage.Italian, GameLanguage.PortugueseBrazil
        };

        [TearDown]
        public void BackToEnglish() => Loc.Set(GameLanguage.English, save: false);

        private static Dictionary<string, string> SourceKeys()
        {
            var asset = Resources.Load<TextAsset>("Localization/source");
            Assert.IsNotNull(asset, "Resources/Localization/source.txt");
            var keys = new Dictionary<string, string>();
            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                keys[tab < 0 ? line : line.Substring(0, tab)] = tab < 0 ? null : line.Substring(tab + 1);
            }
            return keys;
        }

        private static int Placeholders(string s) => Regex.Matches(s, @"\{\d+\}").Count;

        [Test]
        public void EveryTableTranslatesEverySourceKeyWithMatchingPlaceholdersAndPluralForms()
        {
            var source = SourceKeys();
            Assert.Greater(source.Count, 150);
            foreach (var language in Translated)
            {
                var table = new Dictionary<string, string>();
                Loc.Parse(Resources.Load<TextAsset>("Localization/" + Loc.Code(language)).text, table);
                foreach (var pair in source)
                {
                    Assert.IsTrue(table.TryGetValue(pair.Key, out var translation), Loc.Code(language) + " lacks: " + pair.Key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(translation), Loc.Code(language) + " empty: " + pair.Key);
                    var forms = translation.Split('|');
                    bool plural = pair.Value != null;
                    int expected = !plural ? 1 : language == GameLanguage.Ukrainian || language == GameLanguage.Russian ? 3 : 2;
                    Assert.AreEqual(expected, forms.Length, Loc.Code(language) + " plural forms: " + pair.Key);
                    foreach (var form in forms)
                        Assert.AreEqual(Placeholders(pair.Key), Placeholders(form), Loc.Code(language) + " placeholders: " + form);
                }
            }
        }

        [Test]
        public void PluralRulesFollowCldrForEachLanguage()
        {
            int[] n = { 0, 1, 2, 4, 5, 11, 12, 14, 21, 22, 25, 101, 111 };
            int[] slavic = { 2, 0, 1, 1, 2, 2, 2, 2, 0, 1, 2, 0, 2 };
            for (int i = 0; i < n.Length; i++)
            {
                Assert.AreEqual(slavic[i], Loc.PluralIndex(GameLanguage.Russian, n[i]), "ru " + n[i]);
                Assert.AreEqual(slavic[i], Loc.PluralIndex(GameLanguage.Ukrainian, n[i]), "uk " + n[i]);
                Assert.AreEqual(n[i] == 1 ? 0 : 1, Loc.PluralIndex(GameLanguage.German, n[i]), "de " + n[i]);
                Assert.AreEqual(n[i] <= 1 ? 0 : 1, Loc.PluralIndex(GameLanguage.French, n[i]), "fr " + n[i]);
            }

            Assert.AreEqual("1 move", Loc.P("{0} moves", "{0} move", 1));
            Assert.AreEqual("3 moves", Loc.P("{0} moves", "{0} move", 3));
            Loc.Set(GameLanguage.Russian, save: false);
            Assert.AreEqual("1 ход", Loc.P("{0} moves", "{0} move", 1));
            Assert.AreEqual("3 хода", Loc.P("{0} moves", "{0} move", 3));
            Assert.AreEqual("5 ходов", Loc.P("{0} moves", "{0} move", 5));
            Assert.AreEqual("21 ход", Loc.P("{0} moves", "{0} move", 21));
            Loc.Set(GameLanguage.German, save: false);
            Assert.AreEqual("Zurück", Loc.T("Undo"));
            Assert.AreEqual("Level 7", Loc.F("Level {0}", 7));
            Assert.AreEqual("Not in any table", Loc.T("Not in any table"), "unknown text falls back to English");
        }

        [Test]
        public void DeviceLanguageIsUsedUntilThePlayerChooses()
        {
            Assert.AreEqual(GameLanguage.German, Loc.Resolve("", SystemLanguage.German));
            Assert.AreEqual(GameLanguage.PortugueseBrazil, Loc.Resolve(null, SystemLanguage.Portuguese));
            Assert.AreEqual(GameLanguage.English, Loc.Resolve("", SystemLanguage.Japanese));
            Assert.AreEqual(GameLanguage.Ukrainian, Loc.Resolve("uk", SystemLanguage.German), "a saved choice wins");
            Assert.AreEqual(GameLanguage.English, Loc.Resolve("xx", SystemLanguage.English));
        }

        [Test]
        public void BoundLabelsFollowTheLanguageAndRawTextDoesNot()
        {
            var root = new GameObject("Localized label test", typeof(RectTransform));
            try
            {
                var label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
                label.transform.SetParent(root.transform, false);
                label.fontSize = 40;
                Loc.SetPlural(label, "{0} moves", "{0} move", 4);
                Assert.AreEqual("4 moves", label.text);
                Assert.IsFalse(label.resizeTextForBestFit, "English keeps its approved layout");
                Loc.Set(GameLanguage.Ukrainian, save: false);
                Assert.AreEqual("4 ходи", label.text);
                Assert.IsTrue(label.resizeTextForBestFit, "translations shrink to fit instead of clipping");
                Assert.AreEqual(40, label.resizeTextMaxSize);
                Loc.SetRaw(label, "12 / 14");
                Loc.Set(GameLanguage.French, save: false);
                Assert.AreEqual("12 / 14", label.text, "raw text is left alone");
                Loc.Set(label, "Level {0}", 3);
                Assert.AreEqual("Niveau 3", label.text);
                Loc.Set(GameLanguage.English, save: false);
                Assert.AreEqual("Level 3", label.text);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator SettingsLanguageRowCyclesAndEveryShownLabelHasATranslation()
        {
            var root = new GameObject("Localization journey test");
            try
            {
                var controller = root.AddComponent<PlopGameController>();
                controller.RememberProgress = false;
                yield return null;
                Assert.AreEqual(GameLanguage.English, Loc.Current, "tests and previews start in English");
                var source = SourceKeys();
                string savedLanguage = PlayerPrefs.GetString(Loc.PreferenceKey, "<none>");

                controller.StartCampaign();
                yield return null;
                var hud = root.GetComponent<GameHud>();
                hud.ShowSettings();
                Assert.AreEqual("English", hud.LanguageLabel);
                var languageButton = root.GetComponentsInChildren<Button>(true).Single(b => b.name == "Language Button");
                languageButton.onClick.Invoke();
                Assert.AreEqual(GameLanguage.German, Loc.Current);
                Assert.AreEqual("Deutsch", hud.LanguageLabel);
                Assert.AreEqual(savedLanguage, PlayerPrefs.GetString(Loc.PreferenceKey, "<none>"),
                    "a test session never saves the language");
                var title = root.GetComponentsInChildren<Text>(true).Single(t => t.name == "Settings title");
                Assert.AreEqual("Einstellungen", title.text);

                // Every label that follows the language has a source key, so each table can translate it.
                var missing = root.GetComponentsInChildren<LocalizedText>(true)
                    .Select(l => l.Source)
                    .Where(s => !string.IsNullOrEmpty(s) && !source.ContainsKey(s))
                    .Distinct().ToList();
                Assert.IsEmpty(missing, "untranslatable labels: " + string.Join(" | ", missing));

                for (int i = 1; i < Loc.Count; i++) languageButton.onClick.Invoke();
                Assert.AreEqual(GameLanguage.English, Loc.Current, "eight presses cycle back to English");
                Assert.AreEqual("Settings", title.text);
                hud.CloseSettings();
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}

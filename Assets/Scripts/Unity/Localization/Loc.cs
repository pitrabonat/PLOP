using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>The eight shipped languages. Order is the Settings ▸ Language cycle.</summary>
    public enum GameLanguage { English, German, Ukrainian, Russian, French, Spanish, Italian, PortugueseBrazil }

    /// <summary>
    /// String localization keyed by the English source text. Tables live in <c>Resources/Localization/&lt;code&gt;.txt</c>
    /// as <c>English⇥Translation</c> lines (UTF-8, <c>#</c> comments, <c>\n</c> for line breaks), so a translator edits one
    /// plain file per language. Missing entries fall back to English, never to a key.
    /// Translation status per language is recorded in the table header and in Documentation/LOCALIZATION.md.
    /// </summary>
    public static class Loc
    {
        public const string PreferenceKey = "plop.language";
        private static readonly string[] Codes = { "en", "de", "uk", "ru", "fr", "es", "it", "pt-BR" };
        private static readonly string[] NativeNames =
            { "English", "Deutsch", "Українська", "Русский", "Français", "Español", "Italiano", "Português (Brasil)" };
        private static readonly string[] Cultures = { "en-US", "de-DE", "uk-UA", "ru-RU", "fr-FR", "es-ES", "it-IT", "pt-BR" };

        private static Dictionary<string, string> _table = new Dictionary<string, string>();
        private static bool _loaded;

        public static GameLanguage Current { get; private set; } = GameLanguage.English;
        public static event Action Changed;

        public static string Code(GameLanguage language) => Codes[(int)language];
        public static string NativeName(GameLanguage language) => NativeNames[(int)language];
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Cultures[(int)Current]);
        public static int Count => Codes.Length;

        /// <summary>Saved choice, else the device language (Portuguese → Brazilian Portuguese), else English.</summary>
        public static GameLanguage Resolve(string saved, SystemLanguage device)
        {
            if (!string.IsNullOrEmpty(saved))
                for (int i = 0; i < Codes.Length; i++)
                    if (Codes[i] == saved) return (GameLanguage)i;
            switch (device)
            {
                case SystemLanguage.German: return GameLanguage.German;
                case SystemLanguage.Ukrainian: return GameLanguage.Ukrainian;
                case SystemLanguage.Russian: return GameLanguage.Russian;
                case SystemLanguage.French: return GameLanguage.French;
                case SystemLanguage.Spanish: return GameLanguage.Spanish;
                case SystemLanguage.Italian: return GameLanguage.Italian;
                case SystemLanguage.Portuguese: return GameLanguage.PortugueseBrazil;
                default: return GameLanguage.English;
            }
        }

        /// <summary>Loads the saved/device language once (the real game; tests stay in English unless they set one).</summary>
        public static void InitFromPreferences()
        {
            Set(Resolve(PlayerPrefs.GetString(PreferenceKey, ""), Application.systemLanguage), save: false);
        }

        public static void Set(GameLanguage language, bool save)
        {
            if (save)
            {
                PlayerPrefs.SetString(PreferenceKey, Code(language));
                PlayerPrefs.Save();
            }
            if (_loaded && language == Current) return;
            Current = language;
            _table = Load(language);
            _loaded = true;
            Changed?.Invoke();
        }

        public static GameLanguage Next(GameLanguage language) => (GameLanguage)(((int)language + 1) % Codes.Length);

        /// <summary>Translation of an English source string (English when untranslated). Not for plural keys: use P.</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            if (!_loaded) Set(Current, save: false);
            return _table.TryGetValue(english, out var t) ? t : english;
        }

        /// <summary>Translated format string; arguments are formatted in the language's culture.</summary>
        public static string F(string englishFormat, params object[] args) =>
            string.Format(Culture, T(englishFormat), args);

        /// <summary>
        /// Plural-aware count text. <paramref name="englishOther"/> is the key ("{0} moves"), <paramref name="englishOne"/>
        /// the English singular ("{0} move"). Tables list the forms separated by '|': one|other, or one|few|many (uk, ru).
        /// </summary>
        public static string P(string englishOther, string englishOne, int n)
        {
            if (!_loaded) Set(Current, save: false);
            if (Current == GameLanguage.English || !_table.TryGetValue(englishOther, out var forms))
                return string.Format(Culture, n == 1 ? englishOne : englishOther, n);
            var split = forms.Split('|');
            return string.Format(Culture, split[Mathf.Min(PluralIndex(Current, n), split.Length - 1)], n);
        }

        /// <summary>CLDR cardinal plural category index for integers: 0 one, 1 few/other, 2 many.</summary>
        internal static int PluralIndex(GameLanguage language, int n)
        {
            n = Math.Abs(n);
            switch (language)
            {
                case GameLanguage.Ukrainian:
                case GameLanguage.Russian:
                    if (n % 10 == 1 && n % 100 != 11) return 0;
                    if (n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14)) return 1;
                    return 2;
                case GameLanguage.French:
                case GameLanguage.PortugueseBrazil:
                    return n <= 1 ? 0 : 1;
                default:
                    return n == 1 ? 0 : 1;
            }
        }

        /// <summary>Shows <paramref name="english"/> (formatted with <paramref name="args"/>) and keeps it translated.</summary>
        public static void Set(Text text, string english, params object[] args) => LocalizedText.Bind(text, english, args, null, 0);

        /// <summary>Plural count label that stays translated (see <see cref="P"/>).</summary>
        public static void SetPlural(Text text, string englishOther, string englishOne, int n) =>
            LocalizedText.Bind(text, englishOther, null, englishOne, n);

        /// <summary>Text that is never translated (numbers, the game name) or was already composed from translations.</summary>
        public static void SetRaw(Text text, string value)
        {
            if (text == null) return;
            LocalizedText.Unbind(text);
            text.text = value;
        }

        internal static Dictionary<string, string> Load(GameLanguage language)
        {
            var table = new Dictionary<string, string>();
            if (language == GameLanguage.English) return table;
            var asset = Resources.Load<TextAsset>("Localization/" + Code(language));
            if (asset == null) return table;
            Parse(asset.text, table);
            Resources.UnloadAsset(asset);
            return table;
        }

        internal static void Parse(string text, Dictionary<string, string> table)
        {
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab == line.Length - 1) continue;
                table[Unescape(line.Substring(0, tab))] = Unescape(line.Substring(tab + 1));
            }
        }

        private static string Unescape(string s) => s.Replace("\\n", "\n");

        /// <summary>Keys of a table (tests: every translated table covers the source list).</summary>
        internal static IReadOnlyCollection<string> Keys(GameLanguage language) => Load(language).Keys;
    }

    /// <summary>
    /// A label whose English source is translated now and again whenever the language changes. In translated languages
    /// long text shrinks to fit (down to 62 % of the design size) instead of being clipped; English keeps its approved layout.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        private Text _text;
        private string _source, _one;
        private object[] _args;
        private int _count, _designSize;
        private bool _designBestFit;

        public string Source => _source;

        internal static void Bind(Text text, string english, object[] args, string one, int count)
        {
            if (text == null) return;
            var loc = text.GetComponent<LocalizedText>();
            if (loc == null)
            {
                loc = text.gameObject.AddComponent<LocalizedText>();
                loc._designSize = text.fontSize;
                loc._designBestFit = text.resizeTextForBestFit;
            }
            loc._text = text;
            loc._source = english;
            loc._args = args != null && args.Length > 0 ? args : null;
            loc._one = one;
            loc._count = count;
            loc.Apply();
        }

        /// <summary>A label that now shows untranslated text stops following the language.</summary>
        public static void Unbind(Text text)
        {
            var loc = text != null ? text.GetComponent<LocalizedText>() : null;
            if (loc != null) loc._source = null;
        }

        private void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Loc.Changed -= Apply;

        private void Apply()
        {
            if (_text == null || _source == null) return;
            _text.text = _one != null ? Loc.P(_source, _one, _count)
                : _args != null ? Loc.F(_source, _args) : Loc.T(_source);
            bool fit = _designBestFit || Loc.Current != GameLanguage.English;
            _text.resizeTextForBestFit = fit;
            if (fit && !_designBestFit)
            {
                _text.resizeTextMaxSize = _designSize;
                _text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(_designSize * 0.62f));
            }
        }
    }
}

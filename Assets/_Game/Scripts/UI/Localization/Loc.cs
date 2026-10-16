using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BottleSort.UI
{
    public enum Language
    {
        Ru,
        En
    }

    // Строки лежат в Localization/Resources/loc_strings.txt: ключ, ru, en через табуляцию.
    // В тексте \n — перенос строки.
    public static class Loc
    {
        const string ResourceName = "loc_strings";

        static Dictionary<string, string[]> _table;
        static readonly HashSet<string> Reported = new HashSet<string>();

        public static event Action Changed;
        public static Language Current { get; private set; } = Language.Ru;

        public static Language SystemDefault
        {
            get
            {
                switch (Application.systemLanguage)
                {
                    case SystemLanguage.Russian:
                    case SystemLanguage.Ukrainian:
                    case SystemLanguage.Belarusian:
                        return Language.Ru;
                    default:
                        return Language.En;
                }
            }
        }

        public static void Set(Language language)
        {
            if (language == Current && _table != null)
                return;
            Current = language;
            Load();
            Changed?.Invoke();
        }

        public static string Get(string key)
        {
            Load();
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (_table.TryGetValue(key, out var row))
            {
                string value = row[(int)Current];
                return string.IsNullOrEmpty(value) ? row[0] : value;
            }

            if (Reported.Add(key))
                Debug.LogWarning($"[Loc] Missing key '{key}'");
            return key;
        }

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        // 1 250 по-русски, 1,250 по-английски.
        public static string Number(int value)
        {
            if (Current == Language.Ru)
                return value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '\u00A0');
            return value.ToString("#,0", CultureInfo.InvariantCulture);
        }

        static void Load()
        {
            if (_table != null)
                return;
            _table = new Dictionary<string, string[]>();
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
            {
                Debug.LogError($"[Loc] {ResourceName}.txt not found in Resources");
                return;
            }

            foreach (string rawLine in asset.text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                string[] cells = line.Split('\t');
                if (cells.Length < 2)
                    continue;
                var row = new string[2];
                for (int i = 0; i < row.Length; i++)
                    row[i] = i + 1 < cells.Length ? cells[i + 1].Replace("\\n", "\n") : string.Empty;
                _table[cells[0].Trim()] = row;
            }
        }
    }
}

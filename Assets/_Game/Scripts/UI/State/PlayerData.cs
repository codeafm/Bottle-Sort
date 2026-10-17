using System;
using System.Collections.Generic;
using BottleSort.Services;
using UnityEngine;

namespace BottleSort.UI
{
    // Всё, что сохраняется между запусками. Читает и пишет файл SaveService, правила начисления — Economy.
    [Serializable]
    public sealed class PlayerData
    {
        public int version = SaveService.FormatVersion;
        public int coins = EconomyRules.StartCoins;
        public int highestUnlocked = 1;
        public List<int> stars = new List<int>();
        public int undoCount;
        public int hintCount;
        public int extraBottleCount;
        public bool tutorialGiftTaken;
        public string bottleSkin = "classic";
        public string theme = "auto";
        public List<string> owned = new List<string> { "classic", "navy" };

        public bool musicOn = true;
        public bool soundsOn = true;
        public bool vibrationOn = true;
        public bool notificationsOn = true;
        public float musicVolume = 0.6f;
        public float soundVolume = 1f;
        public string language = "";

        // Даты в виде yyyy-MM-dd по местному времени.
        public string giftDate = "";
        public string calendarDate = "";
        public int calendarDay = 1;
        public string wheelDate = "";
        public int wheelSpins = 1;

        // «+100 монет за рекламу» в меню: сколько раз сегодня и когда последний раз (Unix-время, секунды).
        public string coinAdDate = "";
        public int coinAdCount;
        public long coinAdLastUnix;

        public int starChestsOpened;
        public List<int> chapterChests = new List<int>();
        public bool noAds;

        [NonSerialized] SaveService _storage;

        static PlayerData _current;
        static SaveService _defaultStorage;

        public static event Action Changed;

        public static PlayerData Current => _current ?? (_current = DefaultStorage.Load());
        public static string SavePath => DefaultStorage.Path;
        static SaveService DefaultStorage => _defaultStorage ?? (_defaultStorage = SaveService.CreateDefault());

        public bool IsFirstLaunch => highestUnlocked <= 1 && StarsOf(1) == 0;

        public int TotalStars
        {
            get
            {
                int sum = 0;
                foreach (int value in stars)
                    sum += value;
                return sum;
            }
        }

        public int StarsOf(int level) => level >= 1 && level <= stars.Count ? stars[level - 1] : 0;

        public void RecordWin(int level, int earnedStars)
        {
            while (stars.Count < level)
                stars.Add(0);
            stars[level - 1] = Mathf.Max(stars[level - 1], earnedStars);
            highestUnlocked = Mathf.Max(highestUnlocked, level + 1);
            Save();
        }

        public void AddCoins(int amount)
        {
            coins = Mathf.Max(0, coins + amount);
            Save();
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || coins < amount)
                return false;
            coins -= amount;
            Save();
            return true;
        }

        public bool Owns(string id) => owned.Contains(id);

        public void AttachStorage(SaveService storage) => _storage = storage;

        public void Save()
        {
            _storage?.Save(this);
            Changed?.Invoke();
        }

        // Перечитать файл при следующем обращении (после восстановления сохранения в тестах).
        public static void Reload()
        {
            _current = null;
            Changed?.Invoke();
        }

        // Начать с чистого листа: первый запуск, ноль монет. Для отладки и тестов.
        public static void DeleteSave()
        {
            DefaultStorage.Delete();
            Reload();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("BottleSort/Reset Player Data")]
        static void ResetForEditor() => DeleteSave();
#endif
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BottleSort.UI
{
    // Временное хранилище прогресса и настроек, пока нет сервиса сохранений.
    // Формат — JSON в persistentDataPath, как и задумано для services; поля можно будет перенести как есть.
    [Serializable]
    public sealed class PlayerData
    {
        const string FileName = "player.json";

        public int version = 1;
        public int coins;
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
        public string language = "";

        static PlayerData _current;

        public static event Action Changed;

        public static PlayerData Current => _current ?? (_current = Load());
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

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
            if (coins < amount)
                return false;
            coins -= amount;
            Save();
            return true;
        }

        public bool Owns(string id) => owned.Contains(id);

        public void Save()
        {
            try
            {
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(this));
                if (File.Exists(SavePath))
                    File.Delete(SavePath);
                File.Move(temp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerData] Save failed: {e.Message}");
            }
            Changed?.Invoke();
        }

        static PlayerData Load()
        {
            try
            {
                if (File.Exists(SavePath))
                    return JsonUtility.FromJson<PlayerData>(File.ReadAllText(SavePath)) ?? new PlayerData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerData] Load failed, starting fresh: {e.Message}");
            }
            return new PlayerData();
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
            if (File.Exists(SavePath))
                File.Delete(SavePath);
            Reload();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("BottleSort/Reset Player Data")]
        static void ResetForEditor() => DeleteSave();
#endif
    }
}

using System;
using System.IO;
using BottleSort.UI;
using UnityEngine;

namespace BottleSort.Services
{
    // Прогресс игрока в одном JSON-файле. Каждый Save пишет файл целиком: он маленький (меньше 10 КБ
    // даже на 2000 уровнях), а так после любого действия на диске уже актуальная версия.
    public sealed class SaveService
    {
        public const string DefaultFileName = "player.json";

        // 1 — временный формат ui. 2 — даты наград, колесо, сундуки, громкость.
        public const int FormatVersion = 2;

        public string Path { get; }

        public SaveService(string directory, string fileName = DefaultFileName)
        {
            Path = System.IO.Path.Combine(directory, fileName);
        }

        public static SaveService CreateDefault() => new SaveService(Application.persistentDataPath);

        public PlayerData Load()
        {
            PlayerData data = null;
            bool found = SaveStore.TryRead(Path, json => TryParse(json, out data), out _);
            if (!found)
                data = new PlayerData();
            Migrate(data);
            data.AttachStorage(this);
            return data;
        }

        public void Save(PlayerData data)
        {
            data.version = FormatVersion;
            try
            {
                SaveStore.WriteAtomic(Path, JsonUtility.ToJson(data));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Нет места или нет доступа — игра продолжается, следующая запись попробует снова.
                Debug.LogWarning($"[SaveService] Could not write {Path}: {e.Message}");
            }
        }

        public void Delete() => SaveStore.DeleteAll(Path);

        static bool TryParse(string json, out PlayerData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                var parsed = new PlayerData();
                JsonUtility.FromJsonOverwrite(json, parsed);
                data = parsed;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        static void Migrate(PlayerData data)
        {
            // Поля, которых не было в старом файле, остаются со значениями по умолчанию из PlayerData,
            // здесь чиним только то, что могло прийти битым или пустым.
            if (data.stars == null)
                data.stars = new System.Collections.Generic.List<int>();
            if (data.owned == null || data.owned.Count == 0)
                data.owned = new System.Collections.Generic.List<string> { "classic", "navy" };
            if (data.chapterChests == null)
                data.chapterChests = new System.Collections.Generic.List<int>();
            data.highestUnlocked = Mathf.Max(1, data.highestUnlocked);
            data.coins = Mathf.Max(0, data.coins);
            data.calendarDay = Mathf.Clamp(data.calendarDay, 1, 7);
            data.musicVolume = Mathf.Clamp01(data.musicVolume);
            data.soundVolume = Mathf.Clamp01(data.soundVolume);
            if (string.IsNullOrEmpty(data.bottleSkin))
                data.bottleSkin = "classic";
            if (string.IsNullOrEmpty(data.theme))
                data.theme = "auto";
            data.version = FormatVersion;
        }
    }
}

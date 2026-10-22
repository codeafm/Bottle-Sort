using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BottleSort.Core;
using UnityEngine;

namespace BottleSort.UI
{
    // Уровни 1–2000 берём из готовой базы (Resources/LevelDatabase), обучающие 1–3 — из HandmadeLevels,
    // а после 2000 строим генератором в фоне и заранее, пока игрок смотрит на экран победы.
    public static class LevelLoader
    {
        const string DatabaseResource = "LevelDatabase";

        static readonly Dictionary<int, Task<GeneratedLevel>> Cache = new Dictionary<int, Task<GeneratedLevel>>();
        static LevelDatabase _database;

        public static Task<GeneratedLevel> Get(int level)
        {
            if (Cache.TryGetValue(level, out var task) && !task.IsFaulted)
                return task;
            // Resources можно трогать только из главного потока, поэтому базу читаем здесь, до Task.Run.
            var database = Database;
            task = Task.Run(() => Build(level, database));
            Cache[level] = task;
            return task;
        }

        public static void Preload(int level) => Get(level);

        // Готовый уровень под номером вместо генератора: раскладки из макета для сравнения экранов, отладка.
        public static void Put(int level, GeneratedLevel generated) => Cache[level] = Task.FromResult(generated);

        static LevelDatabase Database
        {
            get
            {
                if (_database == null)
                {
                    var asset = Resources.Load<TextAsset>(DatabaseResource);
                    _database = asset != null ? LevelDatabase.Parse(asset.text) : new LevelDatabase();
                    if (asset == null)
                        Debug.LogWarning("[LevelLoader] No level database, every level will be generated on the fly.");
                }
                return _database;
            }
        }

        static GeneratedLevel Build(int level, LevelDatabase database)
        {
            if (HandmadeLevels.TryGet(level, out var handmade))
                return handmade;
            var stored = database.Get(level);
            if (stored != null)
                return stored;

            try
            {
                return LevelGenerator.Generate(LevelParams.SpecFor(level));
            }
            catch (Exception e)
            {
                // На лету проверки строже, чем есть время перебирать кандидатов; лучше упростить уровень, чем упасть.
                Debug.LogWarning($"[LevelLoader] Level {level}: {e.Message}. Retrying with a simpler spec.");
                var spec = LevelParams.SpecFor(level);
                spec.HiddenLayers = 0;
                spec.ShapePool.Clear();
                spec.Colors = Math.Max(3, spec.Colors - 1);
                return LevelGenerator.Generate(spec);
            }
        }
    }

    // Награда за рекламу. Handler подменяет Services.Ads при запуске; по умолчанию сразу «просмотрено».
    public static class RewardedAds
    {
        public static Action<string, Action<bool>> Handler = (placement, done) => done(true);
        // Загружен ли ролик прямо сейчас; если нет — Prepare запускает загрузку заново.
        public static Func<bool> IsReady = () => true;
        public static Action Prepare = () => { };

        // placement — что игрок получит за ролик, по-русски: под этим именем кнопка видна в аналитике.
        public static void Show(string placement, Action<bool> done) => Handler(placement, done);

        // Для кнопок, которые не должны молча ждать: ролик готов — true, иначе догружаем и false.
        public static bool EnsureReady()
        {
            if (IsReady())
                return true;
            Prepare();
            return false;
        }
    }
}

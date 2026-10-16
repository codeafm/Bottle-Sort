using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BottleSort.Core;
using UnityEngine;

namespace BottleSort.UI
{
    // Генератор с солвером может думать сотни миллисекунд, поэтому уровень строим в фоне
    // и заранее, пока игрок смотрит на экран победы.
    public static class LevelLoader
    {
        static readonly Dictionary<int, Task<GeneratedLevel>> Cache = new Dictionary<int, Task<GeneratedLevel>>();

        public static Task<GeneratedLevel> Get(int level)
        {
            if (Cache.TryGetValue(level, out var task) && !task.IsFaulted)
                return task;
            task = Task.Run(() => Build(level));
            Cache[level] = task;
            return task;
        }

        public static void Preload(int level) => Get(level);

        static GeneratedLevel Build(int level)
        {
            try
            {
                return LevelCatalog.Get(level, LevelPlan.SpecFor);
            }
            catch (Exception e)
            {
                // Временная таблица параметров может попросить невозможное; лучше упростить уровень, чем упасть.
                Debug.LogWarning($"[LevelLoader] Level {level}: {e.Message}. Retrying with a simpler spec.");
                var spec = LevelPlan.SpecFor(level);
                spec.HiddenLayers = 0;
                spec.ShapePool.Clear();
                spec.Colors = Math.Max(3, spec.Colors - 1);
                return LevelGenerator.Generate(spec);
            }
        }
    }

    // Награда за рекламу. Пока сервиса нет — сразу «просмотрено»; services подменит Handler.
    public static class RewardedAds
    {
        public static Action<Action<bool>> Handler = done => done(true);

        public static void Show(Action<bool> done) => Handler(done);
    }
}

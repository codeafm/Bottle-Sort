using System;
using System.Collections.Generic;
using BottleSort.Core;
using BottleSort.Gameplay;
using BottleSort.UI;
using UnityEngine;

namespace BottleSort.Services
{
    // События для Firebase Analytics. Что значит каждое событие и где его смотреть — Docs/ANALYTICS.md.
    // Пока Firebase поднимается, события копятся в очереди; не поднялся — очередь выбрасываем и молчим.
    // До Initialize (в тестах) все вызовы ничего не делают.
    public static class Analytics
    {
        public const string Coins = "Монеты";
        const int MaxQueued = 200;

        public static Func<IAnalyticsBackend> DeviceBackendFactory;

        static IAnalyticsBackend _backend;
        static bool _ready;
        static readonly Queue<KeyValuePair<string, AnalyticsParams>> Queued = new Queue<KeyValuePair<string, AnalyticsParams>>();
        static readonly Dictionary<string, string> SentUserProperties = new Dictionary<string, string>();

        static int _level;
        static LevelType _levelType;
        static float _levelStartedAt;

        public static void Initialize(MainThread thread)
        {
            IAnalyticsBackend backend = null;
#if !UNITY_EDITOR
            backend = DeviceBackendFactory?.Invoke();
#endif
            Use(backend ?? new EditorAnalyticsBackend());

            ScreenRouter.ScreenShown -= OnScreenShown;
            ScreenRouter.ScreenShown += OnScreenShown;
            PlayerData.Changed -= SyncUserProperties;
            PlayerData.Changed += SyncUserProperties;
            GameEvents.UndoUsed -= OnUndo;
            GameEvents.UndoUsed += OnUndo;
            GameEvents.HintShown -= OnHint;
            GameEvents.HintShown += OnHint;
            GameEvents.ExtraBottleAdded -= OnExtraBottle;
            GameEvents.ExtraBottleAdded += OnExtraBottle;
            GameEvents.RestartUsed -= OnRestart;
            GameEvents.RestartUsed += OnRestart;
        }

        // Отдельно от Initialize, чтобы тесты подставили свой бэкенд без подписок на экраны.
        public static void Use(IAnalyticsBackend backend)
        {
            _backend = backend;
            _ready = false;
            Queued.Clear();
            SentUserProperties.Clear();
            backend?.Start(ok =>
            {
                if (_backend != backend)
                    return;
                _ready = ok;
                if (!ok)
                {
                    Debug.LogWarning("[Analytics] Firebase is not available, events are dropped");
                    Queued.Clear();
                    return;
                }
                while (Queued.Count > 0)
                {
                    var item = Queued.Dequeue();
                    Send(item.Key, item.Value);
                }
                SyncUserProperties();
            });
        }

        public static void Log(string name, AnalyticsParams parameters = null)
        {
            if (_backend == null)
                return;
            parameters = parameters ?? new AnalyticsParams();
            if (_ready)
            {
                Send(name, parameters);
                return;
            }
            if (Queued.Count < MaxQueued)
                Queued.Enqueue(new KeyValuePair<string, AnalyticsParams>(name, parameters));
        }

        static void Send(string name, AnalyticsParams parameters)
        {
            try
            {
                _backend.Log(name, parameters);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] {name} failed: {e.Message}");
            }
        }

        // Уровни

        public static void LevelStarted(int level, LevelType type, int attempt)
        {
            _level = level;
            _levelType = type;
            _levelStartedAt = Time.realtimeSinceStartup;
            if (level == 1 && attempt == 1)
                Log("tutorial_begin");
            Log("level_start", LevelParams().Add("attempt", attempt));
        }

        public static void LevelWon(int level, int stars, int moves, int reward, bool tutorial)
        {
            Log("level_complete", LevelParams(level)
                .Add("stars", stars)
                .Add("moves", moves)
                .Add("seconds", SecondsOnLevel())
                .Add("reward", reward));
            if (reward > 0)
                CoinsEarned(reward, "Победа на уровне");
            if (tutorial && level == HandmadeLevels.TutorialCount)
                Log("tutorial_complete");
        }

        public static void LevelLost(int level, string reason, int moves) =>
            Log("level_fail", LevelParams(level)
                .Add("reason", reason)
                .Add("moves", moves)
                .Add("seconds", SecondsOnLevel()));

        static void OnRestart() => Log("level_restart", LevelParams());
        static void OnUndo() => Booster("Отмена хода");
        static void OnHint(Move move) => Booster("Подсказка");
        static void OnExtraBottle() => Booster("+Бутылка");

        static void Booster(string name) => Log("booster_use", LevelParams().Add("booster", name));

        static AnalyticsParams LevelParams(int level = 0)
        {
            if (level <= 0)
                level = _level;
            var type = level == _level ? _levelType : LevelPlan.TypeOf(level);
            return new AnalyticsParams()
                .Add("level", level)
                .Add("level_type", LevelTypeName(type));
        }

        static long SecondsOnLevel() => (long)Math.Max(0, Time.realtimeSinceStartup - _levelStartedAt);

        public static string LevelTypeName(LevelType type)
        {
            switch (type)
            {
                case LevelType.Special: return "Особый";
                case LevelType.BigFlask: return "Большая колба";
                case LevelType.Rescue: return "Спасение";
                case LevelType.Boss: return "Босс";
                case LevelType.ChapterBoss: return "Босс главы";
                default: return "Обычный";
            }
        }

        // Реклама

        public static void RewardedAd(string placement, string result) =>
            Log("rewarded_ad", new AnalyticsParams()
                .Add("placement", placement)
                .Add("result", result)
                .Add("level", _level));

        public static void InterstitialAd(string after, string result, int level) =>
            Log("interstitial_ad", new AnalyticsParams()
                .Add("after", after)
                .Add("result", result)
                .Add("level", level));

        // Стандартное событие Firebase: по нему консоль сама считает доход с рекламы.
        public static void AdImpression(AdImpression impression)
        {
            var parameters = new AnalyticsParams()
                .Add("ad_platform", "Yandex")
                .Add("ad_source", impression.Network)
                .Add("ad_format", impression.Format)
                .Add("ad_unit_name", impression.AdUnit)
                .Add("precision", impression.Precision);
            if (impression.HasRevenue)
                parameters.Add("value", impression.RevenueUsd).Add("currency", "USD");
            Log("ad_impression", parameters);
        }

        // Монеты и бустеры

        public static void CoinsEarned(int coins, string source) =>
            Rewarded(new RewardBundle(coins), source);

        public static void Rewarded(RewardBundle reward, string source)
        {
            if (reward.IsEmpty)
                return;
            var parameters = new AnalyticsParams()
                .Add("virtual_currency_name", Coins)
                .Add("value", reward.Coins)
                .Add("source", source ?? "Прочее");
            if (reward.Undo > 0) parameters.Add("undo", reward.Undo);
            if (reward.Hints > 0) parameters.Add("hints", reward.Hints);
            if (reward.Bottles > 0) parameters.Add("bottles", reward.Bottles);
            if (reward.Spins > 0) parameters.Add("spins", reward.Spins);
            Log("earn_virtual_currency", parameters);
        }

        public static void CoinsSpent(int coins, string item) =>
            Log("spend_virtual_currency", new AnalyticsParams()
                .Add("virtual_currency_name", Coins)
                .Add("value", coins)
                .Add("item_name", item));

        // Экраны и свойства игрока

        static void OnScreenShown(UiScreen screen, bool animated) =>
            Log("screen_view", new AnalyticsParams()
                .Add("screen_name", ScreenName(screen.Id))
                .Add("screen_class", screen.Id.ToString()));

        public static string ScreenName(ScreenId screen)
        {
            switch (screen)
            {
                case ScreenId.Splash: return "Заставка";
                case ScreenId.Menu: return "Главное меню";
                case ScreenId.Levels: return "Карта уровней";
                case ScreenId.Game: return "Игра";
                case ScreenId.Win: return "Победа";
                case ScreenId.Shop: return "Магазин";
                case ScreenId.Settings: return "Настройки";
                case ScreenId.Gift: return "Подарок";
                case ScreenId.Reward: return "Награда";
                case ScreenId.Calendar: return "Календарь";
                case ScreenId.Wheel: return "Колесо удачи";
                default: return screen.ToString();
            }
        }

        static void SyncUserProperties()
        {
            if (_backend == null || !_ready)
                return;
            var data = PlayerData.Current;
            SetUserProperty("max_level", data.highestUnlocked.ToString());
            SetUserProperty("no_ads", data.noAds ? "да" : "нет");
            SetUserProperty("coins_from_game", data.coinsFromGame.ToString());
            SetUserProperty("coins_from_ads", data.coinsFromAds.ToString());
            SetUserProperty("game_language", Loc.Current == Language.Ru ? "русский" : Loc.Current.ToString());
        }

        static void SetUserProperty(string name, string value)
        {
            if (SentUserProperties.TryGetValue(name, out var sent) && sent == value)
                return;
            SentUserProperties[name] = value;
            try
            {
                _backend.SetUserProperty(name, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] user property {name} failed: {e.Message}");
            }
        }
    }
}

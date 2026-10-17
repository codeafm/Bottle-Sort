using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BottleSort.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BottleSort.Editor.Levels
{
    // Собирает базу уровней 1–2000 с отбором по сложности, проверяет её и пишет отчёт.
    // Меню BottleSort/Levels/Build Database или batchmode:
    //   Unity -batchmode -projectPath . -executeMethod BottleSort.Editor.Levels.LevelDatabaseTool.BuildFromCommandLine [-levelsTo 2000]
    // С ключом -auditOnly (или меню Audit Database) база не пересобирается: только проверки, замеры и отчёт по готовой.
    public static class LevelDatabaseTool
    {
        public const string DatabasePath = "Assets/_Game/Levels/Resources/LevelDatabase.txt";
        public const int LevelCount = 2000;
        const string ReportPath = "Docs/LEVELS_REPORT.md";
        const string CsvPath = "Docs/levels_curve.csv";
        const string PngPath = "Docs/levels_curve.png";

        [MenuItem("BottleSort/Levels/Build Database")]
        static void BuildFromMenu() => ShowResult(Run(LevelCount, rebuild: true));

        [MenuItem("BottleSort/Levels/Audit Database")]
        static void AuditFromMenu() => ShowResult(Run(LevelCount, rebuild: false));

        static void ShowResult(int violations)
        {
            EditorUtility.DisplayDialog("Level database", violations == 0
                ? "Все уровни прошли проверки. Отчёт: " + ReportPath
                : $"Нарушений: {violations}. Подробности в {ReportPath}", "OK");
        }

        public static void BuildFromCommandLine()
        {
            int to = LevelCount;
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-levelsTo");
            if (at >= 0 && at + 1 < args.Length)
                to = int.Parse(args[at + 1]);

            bool rebuild = Array.IndexOf(args, "-auditOnly") < 0;
            int violations;
            try
            {
                violations = Run(to, rebuild);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.Exit(violations == 0 ? 0 : 1);
        }

        static int Run(int to, bool rebuild)
        {
            var clock = Stopwatch.StartNew();
            var failures = new List<LevelIssue>();
            List<GeneratedLevel> levels;
            double buildSeconds;
            if (rebuild)
            {
                int lastLogged = 0;
                levels = LevelAudit.Build(1, to, failures, done =>
                {
                    if (done / 100 > lastLogged / 100)
                    {
                        lastLogged = done;
                        Debug.Log($"[Levels] built {done}/{to}, {clock.Elapsed.TotalSeconds:F0} s");
                    }
                });
                buildSeconds = clock.Elapsed.TotalSeconds;
            }
            else
            {
                var database = LevelDatabase.Parse(File.ReadAllText(DatabasePath));
                levels = database.Levels.Where(n => n <= to).OrderBy(n => n).Select(n => database.Get(n)).ToList();
                buildSeconds = database.BuildSeconds;
            }

            var issues = LevelAudit.Validate(levels);
            issues.AddRange(failures);
            var timings = MeasureOnTheFly();

            if (rebuild)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
                File.WriteAllText(DatabasePath, LevelDatabase.Write(levels, LevelGenerator.CurrentGenVersion, buildSeconds));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(CsvPath, LevelAudit.Csv(levels));
            WriteCurve(levels, PngPath);
            File.WriteAllText(ReportPath, LevelReport.Markdown(levels, issues, timings, Notes(), buildSeconds));
            AssetDatabase.ImportAsset(DatabasePath);

            int violations = issues.Count(i => !i.Warning);
            Debug.Log($"[Levels] {levels.Count} levels, {violations} violations, {issues.Count - violations} warnings, " +
                      $"{clock.Elapsed.TotalMinutes:F1} min. Report: {ReportPath}");
            foreach (var issue in issues.Where(i => !i.Warning).Take(50))
                Debug.LogWarning("[Levels] " + issue);
            return violations;
        }

        // Уровни после 2000 строятся на устройстве без отбора по сложности (первый подходящий кандидат).
        // Меряем худшие случаи в одном потоке, как это будет в фоне на телефоне.
        static List<LevelReport.Timing> MeasureOnTheFly()
        {
            var cases = new (string label, int[] levels)[]
            {
                ("обычный, 2001+", new[] { 2001, 2002, 2003, 2004, 2006 }),
                ("особый (смесь форм, бывает 5 слоёв)", new[] { 2010, 2020, 2030, 2040, 2060 }),
                ("большая колба", new[] { 2005, 2015, 2035, 2045, 2055 }),
                ("спасение", new[] { 2028, 2078, 2128, 2178, 2228 }),
                ("босс «испытание цветом» (12 цветов)", new[] { 2025, 2125, 2225, 2325, 2425 }),
                ("босс «ловушка»", new[] { 2050, 2150, 2250, 2350, 2450 }),
                ("босс с лимитом ходов (нужны метрики)", new[] { 2075, 2175, 2275, 2375, 2475 }),
                ("босс главы (3 фишки, формы)", new[] { 2100, 2200, 2300, 2400, 2500 })
            };

            var timings = new List<LevelReport.Timing>();
            LevelGenerator.Generate(LevelParams.SpecFor(2001));
            foreach (var (label, numbers) in cases)
            {
                double worst = 0, total = 0;
                foreach (int number in numbers)
                {
                    var clock = Stopwatch.StartNew();
                    LevelGenerator.Generate(LevelParams.SpecFor(number));
                    double ms = clock.Elapsed.TotalMilliseconds;
                    worst = Math.Max(worst, ms);
                    total += ms;
                }
                timings.Add(new LevelReport.Timing { Label = label + " — среднее", Milliseconds = total / numbers.Length });
                timings.Add(new LevelReport.Timing { Label = label + " — худший", Milliseconds = worst });
            }
            return timings;
        }

        static string Notes() =>
            "Замер — в редакторе Unity (Mono, один поток). На телефоне 2020+ с IL2CPP ждём примерно ×1.5–3 медленнее. " +
            "Поэтому уровни 1–2000 не генерируются на устройстве, а берутся из готовой базы " +
            "`Assets/_Game/Levels/Resources/LevelDatabase.txt`: отбор по сложности требует метрик по 16 кандидатам, " +
            "это секунды на уровень. Уровни 2001+ строятся на лету в фоне (`LevelLoader`, `Task.Run`), " +
            "следующий уровень готовится заранее, пока игрок смотрит на экран победы, и кешируется.";

        static void WriteCurve(IReadOnlyList<GeneratedLevel> levels, string path)
        {
            const int width = 2000, height = 640, margin = 40;
            var texture = new Texture2D(width + margin * 2, height + margin * 2, TextureFormat.RGB24, false);
            var pixels = Enumerable.Repeat(new Color32(13, 20, 48, 255), texture.width * texture.height).ToArray();

            void Plot(int x, int y, Color32 color, int size = 1)
            {
                for (int dx = -size + 1; dx < size; dx++)
                for (int dy = -size + 1; dy < size; dy++)
                {
                    int px = x + dx, py = y + dy;
                    if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                        pixels[py * texture.width + px] = color;
                }
            }

            int X(int level) => margin + (int)((level - 1) / (double)Math.Max(1, levels.Count - 1) * width);
            int Y(double ds) => margin + (int)(Math.Max(0, Math.Min(100, ds)) / 100.0 * height);

            var grid = new Color32(40, 55, 95, 255);
            for (int ds = 0; ds <= 100; ds += 10)
                for (int x = margin; x <= margin + width; x++)
                    Plot(x, Y(ds), grid);
            for (int level = 1; level <= levels.Count; level += 100)
                for (int y = margin; y <= margin + height; y++)
                    Plot(X(level), y, grid);

            foreach (var level in levels.Where(l => l.Metrics != null))
            {
                Color32 color;
                switch (level.Spec.Tier)
                {
                    case LevelTier.Easy: color = new Color32(98, 196, 120, 255); break;
                    case LevelTier.Hard: color = new Color32(240, 150, 60, 255); break;
                    case LevelTier.Boss: color = new Color32(235, 70, 80, 255); break;
                    default: color = new Color32(90, 150, 240, 255); break;
                }
                Plot(X(level.Spec.Number), Y(level.Metrics.Difficulty), color, 2);
            }

            var withMetrics = levels.Where(l => l.Metrics != null).ToList();
            for (int i = 0; i < withMetrics.Count; i++)
            {
                int from = Math.Max(0, i - 25), to = Math.Min(withMetrics.Count - 1, i + 25);
                double average = 0;
                for (int j = from; j <= to; j++)
                    average += withMetrics[j].Metrics.Difficulty;
                average /= to - from + 1;
                Plot(X(withMetrics[i].Spec.Number), Y(average), new Color32(255, 255, 255, 255), 2);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

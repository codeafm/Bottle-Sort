using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BottleSort.Core
{
    // Docs/LEVELS_REPORT.md: сводка по диапазонам §3.1 и главам, кривая сложности, нарушения.
    public static class LevelReport
    {
        public sealed class Timing
        {
            public string Label;
            public double Milliseconds;
        }

        private static readonly int[] RangeStarts =
            { 1, 4, 11, 21, 31, 51, 76, 101, 151, 201, 251, 301, 351, 401, 451, 501, 701, 1001, 1101, 1501, 1801 };

        public static string Markdown(IReadOnlyList<GeneratedLevel> levels, IReadOnlyList<LevelIssue> issues,
            IReadOnlyList<Timing> deviceTimings, string extraNotes, double buildSeconds)
        {
            var violations = issues.Where(i => !i.Warning).ToList();
            var warnings = issues.Where(i => i.Warning).ToList();
            int failedLevels = violations.Select(i => i.Level).Distinct().Count();
            var md = new StringBuilder();

            md.AppendLine("# Проверка уровней 1–" + levels.Count);
            md.AppendLine();
            md.AppendLine($"Сгенерировано утилитой `BottleSort/Levels/Build Database` (genVersion {LevelGenerator.CurrentGenVersion})" +
                          (buildSeconds > 0 ? $", сборка заняла {buildSeconds / 60:F1} мин" : "") +
                          ". Проверки — `LevelAudit` по `LEVEL_DESIGN.md` §9.5.");
            md.AppendLine();
            md.AppendLine($"**Итог: {levels.Count - failedLevels} из {levels.Count} уровней прошли все проверки**, " +
                          $"нарушений {violations.Count}, предупреждений {warnings.Count}.");
            md.AppendLine();

            md.AppendLine("## Что проверяется");
            md.AppendLine("- решаемость: сохранённое решение проигрывается движком правил до победы, его длина = par;");
            md.AppendLine("- генератор нашёл кандидата, прошедшего все фильтры (§9.4.3: нет собранных и почти собранных бутылок, " +
                          "есть хороший стартовый ход, фишка участвует в решении, BW ≥ 0.6·RW для «?», жадный игрок укладывается в лимит ходов);");
            md.AppendLine("- тип уровня по расписанию §2.1, колба/фигурка/формы на месте, вместимость (4/5, колба 2U), не больше 14 бутылок;");
            md.AppendLine("- фишки не раньше своего уровня ввода, на уровне ввода фишка есть, число фишек по §3.2;");
            md.AppendLine("- уровни 1–30: RW ≥ 80 % (босс 25 — 60 %), OPT ≤ 20, нет тупика за первые 5 ходов (trap5);");
            md.AppendLine("- лимит ходов не строже порога 2★; нет дубликатов (с точностью до порядка бутылок и перестановки цветов) " +
                          "и почти дубликатов (> 30 % одинаковых бутылок с одним из 50 предыдущих);");
            md.AppendLine("- детерминизм: каждый 25-й уровень собран второй раз и совпал побайтно;");
            md.AppendLine("- предупреждения: средние уровни не проседают по окну 10, скачок DS между соседними обычными ≤ 25.");
            md.AppendLine();

            md.AppendLine("## Сводка по диапазонам (§3.1)");
            md.AppendLine();
            md.AppendLine("| Уровни | Шт. | Цвета ср. | 5 слоёв | Бутылок ср. | OPT ср. | Точный OPT | RW ср. | GW ср. | DS ср. | DS E / M / H / B | С «?» | С фишками | Чистые обычные |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int r = 0; r < RangeStarts.Length; r++)
            {
                int from = RangeStarts[r];
                int to = r + 1 < RangeStarts.Length ? RangeStarts[r + 1] - 1 : levels.Count;
                var slice = levels.Where(l => l.Spec.Number >= from && l.Spec.Number <= to).ToList();
                if (slice.Count > 0)
                    md.AppendLine(Row($"{from}–{to}", slice));
            }
            md.AppendLine();

            md.AppendLine("## По главам");
            md.AppendLine();
            md.AppendLine("| Глава | Уровни | DS ср. | DS мин–макс | DS босса главы | RW ср. | OPT ср. |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var chapter in levels.GroupBy(l => LevelSchedule.ChapterOf(l.Spec.Number)))
            {
                var list = chapter.Where(l => l.Metrics != null).ToList();
                var boss = list.LastOrDefault(l => l.Spec.Type == LevelType.ChapterBoss);
                md.AppendLine($"| {chapter.Key} | {chapter.First().Spec.Number}–{chapter.Last().Spec.Number} | " +
                              $"{list.Average(l => l.Metrics.Difficulty):F1} | {list.Min(l => l.Metrics.Difficulty):F0}–{list.Max(l => l.Metrics.Difficulty):F0} | " +
                              $"{(boss != null ? boss.Metrics.Difficulty.ToString("F0", CultureInfo.InvariantCulture) : "—")} | " +
                              $"{Pct(list.Average(l => l.Metrics.RandomWinRate))} | {chapter.Average(l => (double)l.Par):F1} |");
            }
            md.AppendLine();

            md.AppendLine("## Типы уровней");
            md.AppendLine();
            md.AppendLine("| Тип | Шт. | DS ср. | OPT ср. | RW ср. |");
            md.AppendLine("|---|---|---|---|---|");
            foreach (var type in levels.GroupBy(l => l.Spec.Type).OrderBy(g => g.Key))
                md.AppendLine($"| {type.Key} | {type.Count()} | {type.Average(l => l.Metrics?.Difficulty ?? 0):F1} | " +
                              $"{type.Average(l => (double)l.Par):F1} | {Pct(type.Average(l => l.Metrics?.RandomWinRate ?? 0))} |");
            md.AppendLine();

            md.AppendLine("## Кривая сложности");
            md.AppendLine();
            md.AppendLine("![DS по уровням](levels_curve.png)");
            md.AppendLine();
            md.AppendLine("Точки — DS каждого уровня (цвет по тиру: E зелёный, M синий, H оранжевый, B красный), линия — скользящее среднее по 50. " +
                          "Все числа по уровням — в `levels_curve.csv`.");
            md.AppendLine();
            md.AppendLine(CurveSummary(levels));
            md.AppendLine();

            if (deviceTimings != null && deviceTimings.Count > 0)
            {
                md.AppendLine("## Генерация на устройстве");
                md.AppendLine();
                md.AppendLine("| Случай | мс |");
                md.AppendLine("|---|---|");
                foreach (var timing in deviceTimings)
                    md.AppendLine($"| {timing.Label} | {timing.Milliseconds:F0} |");
                md.AppendLine();
            }

            if (!string.IsNullOrEmpty(extraNotes))
            {
                md.AppendLine(extraNotes.TrimEnd());
                md.AppendLine();
            }

            md.AppendLine(Decisions);

            md.AppendLine("## Нарушения");
            md.AppendLine();
            if (violations.Count == 0)
                md.AppendLine("Нет.");
            foreach (var issue in violations)
                md.AppendLine("- " + issue);
            md.AppendLine();

            md.AppendLine("## Предупреждения");
            md.AppendLine();
            if (warnings.Count == 0)
                md.AppendLine("Нет.");
            foreach (var group in warnings.GroupBy(w => w.Check))
            {
                md.AppendLine($"- **{group.Key}** — {group.Count()}: " +
                              string.Join(", ", group.Take(40).Select(w => "L" + w.Level)) + (group.Count() > 40 ? ", …" : ""));
            }
            return md.ToString();
        }

        // Где LEVEL_DESIGN.md противоречит себе или проверкам, и как это решено в LevelParams и генераторе.
        private const string Decisions = @"## Решения и отступления от LEVEL_DESIGN

- **Песочные часы (§5.9).** При двух пустых переворот ни разу не сократил решение (240 случайных раскладок), так что фильтр «OPT с переворотом короче хотя бы на 2» был невыполним. Часы ставятся с одной пустой бутылкой и не больше чем на 7 цветах: без переворота такие уровни обычно не решаются, то есть фишка действительно нужна. В комбо с другими фишками часы не ставятся: одна пустая плюс ещё фишка почти всегда нерешаемо.
- **Лимит ходов (§5.8).** Требование «жадный игрок укладывается в лимит в 40 % попыток» выполнимо только до 8 цветов: при 9 жадный игрок укладывается в 16 % случаев. Поэтому боссы с лимитом (L mod 100 = 75 с L501) — 4 слоя и не больше 8 цветов, сложность им даёт сам лимит. Обучающий L501: лимит par + 15, но не строже порога 2★ (§4: с лимитом любая победа — минимум две звезды).
- **Мягкая форма замка и тонировки** (третья пустая: L151, L251–253). Такой фишке разрешено не участвовать в оптимальном решении — она облегчает уровень, а не мешает.
- **5 слоёв.** В обычных уровнях выключены до решения заказчика (`LevelParams.FiveLayersInNormalLevels`, §10 п. 10). 5 слоёв есть в особых уровнях (с 351, половина), в колбах (с 451, половина) и у боссов (с 501, по доле диапазона). L351 по расписанию §2.1 — обычный уровень, поэтому 5 слоёв вводятся на обычном, а не на особом.
- **Боссы «испытание цветом»** ограничены 12 цветами и 14 бутылками: C_H + 1 на 1801+ дало бы 13 цветов. «Ловушка» с замком на пустой — только при 4 слоях и не больше 8 цветах; иначе лёд на пустой.
- **Доли фишек (§3.1–3.2).** Сначала решается, будет ли обычный уровень «чистым» (доля §3.2), иначе ставятся «?» с долей из таблицы §3.1 и 1–2 механики по весам. Итоговые доли — в колонках «С «?»» и «С фишками».
- **Тиры (§2.3).** Вместо калибровочной выборки из 2000 раскладок на конфигурацию берётся место по DS среди 16 кандидатов самого уровня, прошедших фильтры: E — 28 %, M — 50 %, H — 72 %, босс — 93 %, L2000 — 40 % (финал — праздник, а не стена, §5.13).
- **Пила против скачков.** §2.3 одновременно хочет H на 65–90 процентиле, E на 10–35 и скачок DS между соседними обычными уровнями не больше 25. В одной конфигурации E и H расходятся на 20–30 DS, поэтому скачки H ↔ E оставлены предупреждениями: это и есть пила.
- **Хороший стартовый ход (GOOD0).** На старте все бутылки полные, налить «на свой цвет» некуда. Хорошим считается и перенос отрезка из двух и больше слоёв в пустую.
- **trap5** считается по первым пяти позициям оптимального пути: есть ли ход в доказанный тупик. NARROW не считается — это сотни решений солвера на уровень, а в DS он не входит.
- **Спасения (§2.4).** Фигурка — бутылка с рецептом: принимает только следующий цвет рецепта и ничего не отдаёт; рецепт из U слоёв одного цвета, с L328 — 2U из двух цветов. Своего арта у фигурок пока нет, на поле рисуется classic (задача vfx-art).
- **Почти дубликаты.** Уровень, у которого больше 30 % бутылок совпали с одним из 50 предыдущих, пересобирается с другим seed по порядку номеров; номер пересборки хранится в базе (`rt`).
- **Не сделано:** тонированная колба (1201), цепочка замков (1301), колба во льду (1401) — для них нужны новые правила. В этих главах стоят обычные фишки по таблице.
";

        private static string Row(string label, List<GeneratedLevel> slice)
        {
            var withMetrics = slice.Where(l => l.Metrics != null).ToList();
            var normal = slice.Where(l => l.Spec.Type == LevelType.Normal && l.Spec.Number > 3).ToList();
            string TierDs(LevelTier tier)
            {
                var tierLevels = withMetrics.Where(l => l.Spec.Tier == tier).ToList();
                return tierLevels.Count == 0 ? "—" : tierLevels.Average(l => l.Metrics.Difficulty).ToString("F0", CultureInfo.InvariantCulture);
            }

            int withFeatures = slice.Count(l => LevelAudit.FeaturesIn(l).Any(f => f != LevelParams.FiveLayers));
            int hidden = slice.Count(l => LevelAudit.FeaturesIn(l).Contains(LevelParams.Hidden));
            int clean = normal.Count(l => LevelAudit.FeaturesIn(l).Count == 0);
            return $"| {label} | {slice.Count} | {slice.Average(l => (double)l.State.CountColors()):F1} | " +
                   $"{Pct(slice.Count(l => l.State.Unit == 5) / (double)slice.Count)} | {slice.Average(l => (double)l.State.BottleCount):F1} | " +
                   $"{slice.Average(l => (double)l.Par):F1} | {Pct(slice.Count(l => l.ParExact) / (double)slice.Count)} | " +
                   $"{Pct(withMetrics.Average(l => l.Metrics.RandomWinRate))} | {Pct(withMetrics.Average(l => l.Metrics.GreedyWinRate))} | " +
                   $"{withMetrics.Average(l => l.Metrics.Difficulty):F1} | {TierDs(LevelTier.Easy)} / {TierDs(LevelTier.Medium)} / " +
                   $"{TierDs(LevelTier.Hard)} / {TierDs(LevelTier.Boss)} | {Pct(hidden / (double)slice.Count)} | " +
                   $"{Pct(withFeatures / (double)slice.Count)} | {(normal.Count == 0 ? "—" : Pct(clean / (double)normal.Count))} |";
        }

        private static string CurveSummary(IReadOnlyList<GeneratedLevel> levels)
        {
            var text = new StringBuilder();
            var blocks = levels.Where(l => l.Metrics != null).GroupBy(l => (l.Spec.Number - 1) / 250).ToList();
            text.AppendLine("| Уровни | DS E | DS M | DS H | DS B | H − E |");
            text.AppendLine("|---|---|---|---|---|---|");
            foreach (var block in blocks)
            {
                double Avg(LevelTier tier)
                {
                    var list = block.Where(l => l.Spec.Tier == tier).ToList();
                    return list.Count == 0 ? double.NaN : list.Average(l => l.Metrics.Difficulty);
                }
                double e = Avg(LevelTier.Easy), m = Avg(LevelTier.Medium), h = Avg(LevelTier.Hard), b = Avg(LevelTier.Boss);
                text.AppendLine($"| {block.First().Spec.Number}–{block.Last().Spec.Number} | {e:F1} | {m:F1} | {h:F1} | {b:F1} | {h - e:F1} |");
            }
            return text.ToString();
        }

        private static string Pct(double value) => (value * 100).ToString("F0", CultureInfo.InvariantCulture) + " %";
    }
}

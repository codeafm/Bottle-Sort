using System.Globalization;
using System.Text.RegularExpressions;

namespace BottleSort.Services
{
    // Доход с одного показа из Yandex (ImpressionData.rawData). Пример:
    // {"currency":"RUB","revenueUSD":"0.00011","precision":"estimated","revenue":"0.0099",
    //  "adType":"banner","ad_unit_id":"R-M-...","network":{"name":"Yandex","adapter":"Yandex"}}
    // Числа бывают и строками, и числами, поэтому разбираем терпимо, без JsonUtility.
    public readonly struct AdImpression
    {
        public readonly string Format;
        public readonly string Network;
        public readonly string AdUnit;
        public readonly string Precision;
        public readonly double RevenueUsd;
        public readonly bool HasRevenue;

        AdImpression(string format, string network, string adUnit, string precision, double revenueUsd, bool hasRevenue)
        {
            Format = format;
            Network = network;
            AdUnit = adUnit;
            Precision = precision;
            RevenueUsd = revenueUsd;
            HasRevenue = hasRevenue;
        }

        public static AdImpression Parse(string format, string json)
        {
            json = json ?? "";
            string network = Match(json, "\"network\"\\s*:\\s*\\{[^}]*?\"name\"\\s*:\\s*\"([^\"]*)\"");
            string adUnit = Value(json, "ad_unit_id") ?? Value(json, "blockId");
            string precision = Value(json, "precision");

            bool hasRevenue = TryNumber(Value(json, "revenueUSD"), out double usd);
            if (!hasRevenue && Value(json, "currency") == "USD")
                hasRevenue = TryNumber(Value(json, "revenue"), out usd);

            return new AdImpression(format, string.IsNullOrEmpty(network) ? "Yandex" : network,
                adUnit, precision, hasRevenue ? usd : 0, hasRevenue);
        }

        // Первое значение ключа верхнего уровня или вложенного — для наших ключей это одно и то же.
        static string Value(string json, string key) =>
            Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"?([^\",}\\s]*)");

        static string Match(string json, string pattern)
        {
            var match = Regex.Match(json, pattern);
            return match.Success && match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : null;
        }

        static bool TryNumber(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0;
    }
}

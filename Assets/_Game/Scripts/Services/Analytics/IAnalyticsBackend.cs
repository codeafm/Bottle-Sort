using System;
using System.Collections.Generic;
using System.Text;

namespace BottleSort.Services
{
    // Куда уходят события. На устройстве — Firebase Analytics (Integrations/Firebase), в редакторе — лог.
    public interface IAnalyticsBackend
    {
        // ready(false) — Firebase не поднялся (нет google-services.json / GoogleService-Info.plist), события не шлём.
        void Start(Action<bool> ready);
        void Log(string name, AnalyticsParams parameters);
        void SetUserProperty(string name, string value);
    }

    // Параметры события. Имена — латиница (так требует Firebase), значения можно по-русски.
    public sealed class AnalyticsParams
    {
        readonly List<KeyValuePair<string, object>> _items = new List<KeyValuePair<string, object>>();

        public IReadOnlyList<KeyValuePair<string, object>> Items => _items;

        public AnalyticsParams Add(string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                _items.Add(new KeyValuePair<string, object>(name, value));
            return this;
        }

        public AnalyticsParams Add(string name, long value)
        {
            _items.Add(new KeyValuePair<string, object>(name, value));
            return this;
        }

        public AnalyticsParams Add(string name, double value)
        {
            _items.Add(new KeyValuePair<string, object>(name, value));
            return this;
        }

        public object this[string name]
        {
            get
            {
                foreach (var item in _items)
                    if (item.Key == name)
                        return item.Value;
                return null;
            }
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            foreach (var item in _items)
                text.Append(' ').Append(item.Key).Append('=').Append(item.Value);
            return text.ToString();
        }
    }

    public sealed class EditorAnalyticsBackend : IAnalyticsBackend
    {
        public void Start(Action<bool> ready)
        {
            UnityEngine.Debug.Log("[Analytics] Stub backend, events only go to the log");
            ready?.Invoke(true);
        }

        public void Log(string name, AnalyticsParams parameters) =>
            UnityEngine.Debug.Log($"[Analytics] {name}{parameters}");

        public void SetUserProperty(string name, string value) =>
            UnityEngine.Debug.Log($"[Analytics] user {name}={value}");
    }
}

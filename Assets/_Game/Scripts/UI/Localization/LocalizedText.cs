using TMPro;
using UnityEngine;

namespace BottleSort.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] string key;
        [SerializeField] bool upperCase;

        TMP_Text _text;
        object[] _args;

        public string Key => key;

        public void SetKey(string newKey, params object[] args)
        {
            key = newKey;
            _args = args != null && args.Length > 0 ? args : null;
            Refresh();
        }

        public void SetArgs(params object[] args)
        {
            _args = args;
            Refresh();
        }

        void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => Loc.Changed -= Refresh;

        void Refresh()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();
            if (string.IsNullOrEmpty(key))
                return;
            string value = _args != null ? Loc.Format(key, _args) : Loc.Get(key);
            _text.text = upperCase ? value.ToUpperInvariant() : value;
        }
    }
}

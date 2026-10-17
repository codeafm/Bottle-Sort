using System.Collections.Generic;
using BottleSort.Core;
using UnityEngine;

namespace BottleSort.UI
{
    // Раскладка уровня на поле прямо в редакторе, без Play: видно, где встанут бутылки и полки.
    // Бутылки здесь упрощённые, на uGUI; созданное не сохраняется в сцену и в игре не появляется.
    [ExecuteAlways, RequireComponent(typeof(BottleField))]
    public sealed class FieldPreview : MonoBehaviour
    {
        [SerializeField] BottleStubView bottle;
        [SerializeField] RectTransform shelf;
        [SerializeField, Range(3, 5)] int unit = 4;
        // Строка — бутылка, буквы — цвета снизу вверх: R B G Y P C O K L N. Пустая строка — пустая бутылка.
        [SerializeField] string[] bottles = { "RBGY", "YRPB", "GCRP", "BPCC", "PGYR", "CYBG", "", "" };

#if UNITY_EDITOR
        void OnEnable()
        {
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += Rebuild;
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += Rebuild;
        }

        void OnDisable()
        {
            if (!Application.isPlaying)
                GetComponent<BottleField>().ClearPreview();
        }

        void OnRectTransformDimensionsChange()
        {
            if (!Application.isPlaying)
                GetComponent<BottleField>().Refit();
        }

        public void Rebuild()
        {
            if (this == null || !isActiveAndEnabled || Application.isPlaying || bottle == null || shelf == null)
                return;
            var list = new List<Bottle>();
            foreach (string colors in bottles)
            {
                var item = new Bottle(unit);
                foreach (char letter in colors ?? "")
                {
                    // Опечатку в букве просто пропускаем, чтобы превью не ругалось на каждую правку.
                    if (item.Count >= unit || Palette.Letters.IndexOf(char.ToUpperInvariant(letter)) < 0)
                        continue;
                    item.Push(Palette.FromLetter(letter));
                }
                list.Add(item);
            }
            if (list.Count > 0)
                GetComponent<BottleField>().BuildPreview(new LevelState(unit, list), bottle, shelf);
        }
#endif
    }
}

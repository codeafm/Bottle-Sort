using UnityEngine;

namespace BottleSort.Gameplay
{
    // Фигурка спасения лежит отдельным ассетом в Resources: её текстуры крупные и нужны раз в 50 уровней,
    // поэтому грузятся только тогда, когда фигурка на поле, а не вместе со всем VfxArt.
    public sealed class FigureArt : ScriptableObject
    {
        public const string ResourceFolder = "Figures/";

        public BottleShapeArt shape;

        public static BottleShapeArt Load(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var figure = Resources.Load<FigureArt>(ResourceFolder + id);
            return figure != null ? figure.shape : null;
        }
    }
}

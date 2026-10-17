using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Размытый экран под попапом (backdrop-filter: blur в макете). При открытии один раз перерисовываем
    // экран камерой в текстуру в 4 раза меньше и ещё раз уменьшаем вдвое: билинейное сжатие в 8 раз
    // примерно и даёт blur 3 pt. Дальше это статичная картинка, каждый кадр ничего не пересчитывается.
    [RequireComponent(typeof(RawImage))]
    public sealed class BackdropBlur : MonoBehaviour
    {
        const int RenderScale = 4;

        RawImage _image;
        RenderTexture _blurred;

        RawImage Image => _image != null ? _image : _image = GetComponent<RawImage>();

        // Попап к этому моменту уже включён, поэтому на время снимка прячем его через hide.
        public void Grab(CanvasGroup hide)
        {
            var camera = Camera.main;
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            if (camera == null)
            {
                Image.enabled = false;
                return;
            }

            Image.enabled = false;
            float hiddenAlpha = hide.alpha;
            hide.alpha = 0f;

            var frame = ScreenGrab.Render(camera, RenderScale);
            ReleaseBlurred();
            _blurred = new RenderTexture(frame.width / 2, frame.height / 2, 0)
            {
                filterMode = FilterMode.Bilinear,
                name = "BackdropBlur"
            };
            Graphics.Blit(frame, _blurred);
            RenderTexture.ReleaseTemporary(frame);

            FitToScreen(canvas);
            Image.texture = _blurred;
            Image.enabled = true;
            hide.alpha = hiddenAlpha;
        }

        // Снимок — весь экран, а попап живёт внутри safe area, поэтому растягиваем картинку по всему канвасу.
        void FitToScreen(Canvas canvas)
        {
            var rect = (RectTransform)transform;
            var canvasRect = (RectTransform)canvas.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = canvasRect.rect.size;
            rect.position = canvasRect.TransformPoint(canvasRect.rect.center);
        }

        void ReleaseBlurred()
        {
            if (_blurred == null)
                return;
            Image.texture = null;
            _blurred.Release();
            Destroy(_blurred);
            _blurred = null;
        }

        void OnDisable()
        {
            ReleaseBlurred();
            Image.enabled = false;
        }
    }
}

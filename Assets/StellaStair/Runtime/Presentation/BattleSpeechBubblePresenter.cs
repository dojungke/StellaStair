using System.Collections.Generic;
using StellaStair.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StellaStair.Presentation
{
    public sealed class BattleSpeechBubblePresenter : MonoBehaviour
    {
        private const float VerticalOffset = 1.35f;
        private const float FadeDuration = 0.25f;

        private readonly List<Bubble> activeBubbles = new();
        private Canvas canvas;
        private RectTransform canvasRect;
        private Camera worldCamera;
        private Sprite whiteSprite;

        public void Show(TacticalUnit speaker, string text, float duration = 2.2f)
        {
            if (speaker == null || string.IsNullOrWhiteSpace(text))
                return;

            EnsureCanvas();
            if (canvasRect == null)
                return;

            RemoveBubbleFor(speaker);
            var bubble = CreateBubble(speaker, text.Trim(), Mathf.Max(0.6f, duration));
            activeBubbles.Add(bubble);
            UpdateBubblePosition(bubble);
        }

        private void LateUpdate()
        {
            for (var i = activeBubbles.Count - 1; i >= 0; i--)
            {
                var bubble = activeBubbles[i];
                if (bubble.Speaker == null || !bubble.Speaker.IsAlive)
                {
                    Destroy(bubble.Root.gameObject);
                    activeBubbles.RemoveAt(i);
                    continue;
                }

                bubble.Remaining -= Time.deltaTime;
                if (bubble.Remaining <= 0f)
                {
                    Destroy(bubble.Root.gameObject);
                    activeBubbles.RemoveAt(i);
                    continue;
                }

                var fadeStart = Mathf.Min(FadeDuration, bubble.Duration * 0.4f);
                bubble.Group.alpha = bubble.Remaining < fadeStart ? Mathf.Clamp01(bubble.Remaining / fadeStart) : 1f;
                UpdateBubblePosition(bubble);
            }
        }

        private Bubble CreateBubble(TacticalUnit speaker, string text, float duration)
        {
            var root = new GameObject($"Speech Bubble - {speaker.name}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvasRect, false);
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(230f, 76f);
            rect.pivot = new Vector2(0.5f, 0f);

            var background = root.GetComponent<Image>();
            background.sprite = GetWhiteSprite();
            background.color = new Color(1f, 1f, 1f, 0.94f);

            var labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rect, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 11f);
            labelRect.offsetMax = new Vector2(-14f, -10f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            label.fontSize = 18f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 18f;
            label.enableWordWrapping = true;
            label.raycastTarget = false;

            var tailObject = new GameObject("Tail", typeof(RectTransform), typeof(Image));
            tailObject.transform.SetParent(rect, false);
            var tailRect = tailObject.GetComponent<RectTransform>();
            tailRect.anchorMin = new Vector2(0.5f, 0f);
            tailRect.anchorMax = new Vector2(0.5f, 0f);
            tailRect.pivot = new Vector2(0.5f, 0.5f);
            tailRect.anchoredPosition = new Vector2(0f, -8f);
            tailRect.sizeDelta = new Vector2(18f, 18f);
            tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var tail = tailObject.GetComponent<Image>();
            tail.sprite = GetWhiteSprite();
            tail.color = background.color;
            tail.raycastTarget = false;

            return new Bubble
            {
                Speaker = speaker,
                Root = rect,
                Group = root.GetComponent<CanvasGroup>(),
                Duration = duration,
                Remaining = duration
            };
        }

        private void UpdateBubblePosition(Bubble bubble)
        {
            EnsureCanvas();
            worldCamera ??= Camera.main;
            if (canvasRect == null || worldCamera == null)
                return;

            var world = bubble.Speaker.GetPreviewStandingWorldPosition(bubble.Speaker.Position) + Vector3.up * VerticalOffset;
            var screen = worldCamera.WorldToScreenPoint(world);
            if (screen.z < 0f)
            {
                bubble.Group.alpha = 0f;
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var localPoint);
            bubble.Root.anchoredPosition = localPoint;
        }

        private void RemoveBubbleFor(TacticalUnit speaker)
        {
            for (var i = activeBubbles.Count - 1; i >= 0; i--)
            {
                if (activeBubbles[i].Speaker != speaker)
                    continue;
                Destroy(activeBubbles[i].Root.gameObject);
                activeBubbles.RemoveAt(i);
            }
        }

        private void EnsureCanvas()
        {
            if (canvas != null)
                return;

            var canvasObject = new GameObject("Battle Speech Bubble Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = canvasObject.GetComponent<RectTransform>();
        }

        private Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return whiteSprite;
        }

        private sealed class Bubble
        {
            public TacticalUnit Speaker;
            public RectTransform Root;
            public CanvasGroup Group;
            public float Duration;
            public float Remaining;
        }
    }
}
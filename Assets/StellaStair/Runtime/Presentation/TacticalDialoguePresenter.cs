using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StellaStair.Presentation
{
    public sealed class TacticalDialoguePresenter : MonoBehaviour
    {
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private Image leftPortraitImage;
        [SerializeField] private Image leftDarkOverlayImage;
        [SerializeField] private Image rightPortraitImage;
        [SerializeField] private Image rightDarkOverlayImage;
        [SerializeField] private TMP_Text speakerNameLabel;
        [SerializeField] private TMP_Text dialogueTextLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Color darkOverlayColor = new(0f, 0f, 0f, 0.5f);
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private bool autoFitPortraitSize = true;
        [SerializeField, Min(1f)] private float dialogueCharactersPerSecond = 45f;
        [SerializeField] private bool useTypewriterEffect = true;

        private bool advanceRequested;


        public GameObject DialogueRoot
        {
            get
            {
                ResolveReferences();
                return dialogueRoot;
            }
        }
        private void Awake()
        {
            ResolveReferences();
            Hide();
        }

        public IEnumerator Play(IReadOnlyList<TacticalDialogueDatabase.Line> lines, TacticalDialogueDatabase database)
        {
            if (lines == null || lines.Count == 0 || database == null)
                yield break;

            ResolveReferences();
            if (dialogueRoot == null)
                yield break;

            dialogueRoot.SetActive(true);
            RestoreVisibleScale(dialogueRoot.transform);
            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(RequestAdvance);
                nextButton.onClick.AddListener(RequestAdvance);
            }

            foreach (var line in lines)
            {
                ApplyLine(line, database);
                yield return null;
                yield return RevealDialogueText(line != null ? line.text ?? string.Empty : string.Empty);
                yield return null;
                advanceRequested = false;
                while (!ConsumeAdvanceRequest())
                    yield return null;
            }

            if (nextButton != null)
                nextButton.onClick.RemoveListener(RequestAdvance);
            Hide();
        }

        public void Hide()
        {
            if (dialogueRoot != null)
                dialogueRoot.SetActive(false);
        }

        private void RequestAdvance() => advanceRequested = true;
        private IEnumerator RevealDialogueText(string text)
        {
            if (dialogueTextLabel == null)
                yield break;

            if (!useTypewriterEffect || dialogueCharactersPerSecond <= 0f || string.IsNullOrEmpty(text))
            {
                dialogueTextLabel.text = text;
                advanceRequested = false;
                yield break;
            }

            dialogueTextLabel.text = string.Empty;
            advanceRequested = false;
            var delay = 1f / dialogueCharactersPerSecond;

            for (var i = 1; i <= text.Length; i++)
            {
                if (ConsumeAdvanceRequest())
                {
                    dialogueTextLabel.text = text;
                    advanceRequested = false;
                    yield break;
                }

                dialogueTextLabel.text = text.Substring(0, i);
                var elapsed = 0f;
                while (elapsed < delay)
                {
                    if (ConsumeAdvanceRequest())
                    {
                        dialogueTextLabel.text = text;
                        advanceRequested = false;
                        yield break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            advanceRequested = false;
        }

        private bool ConsumeAdvanceRequest()
        {
            if (advanceRequested)
            {
                advanceRequested = false;
                return true;
            }

            if (Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }

            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }
        private static void RestoreVisibleScale(Transform target)
        {
            if (target == null)
                return;
            var scale = target.localScale;
            if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f) || Mathf.Approximately(scale.z, 0f))
                target.localScale = Vector3.one;
        }

        private void ApplyLine(TacticalDialogueDatabase.Line line, TacticalDialogueDatabase database)
        {
            if (speakerNameLabel != null)
                speakerNameLabel.text = line != null ? line.speakerName ?? string.Empty : string.Empty;
            ApplyPortrait(leftPortraitImage, leftDarkOverlayImage, line?.leftCharacterId, line?.leftPortrait ?? TacticalDialoguePortraitMode.Empty, line?.leftDirection ?? TacticalDialoguePortraitDirection.Default, database);
            ApplyPortrait(rightPortraitImage, rightDarkOverlayImage, line?.rightCharacterId, line?.rightPortrait ?? TacticalDialoguePortraitMode.Empty, line?.rightDirection ?? TacticalDialoguePortraitDirection.Default, database);
        }

        private void ApplyPortrait(
            Image image, Image darkOverlay, string characterId, TacticalDialoguePortraitMode mode, TacticalDialoguePortraitDirection direction, TacticalDialogueDatabase database)
        {
            if (image == null)
                return;

            if (!database.TryGetPortrait(characterId, mode, out var sprite))
            {
                image.sprite = null;
                image.enabled = false;
                SetOverlayVisible(darkOverlay, false, null);
                return;
            }

            image.enabled = true;
            image.sprite = sprite;
            image.color = normalColor;
            var scale = image.rectTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction == TacticalDialoguePortraitDirection.Flipped ? -1f : 1f);
            image.rectTransform.localScale = scale;
            FitPortraitSize(image);
            MatchOverlayToPortrait(darkOverlay, image);
            SetOverlayVisible(darkOverlay, mode == TacticalDialoguePortraitMode.Dark, sprite);
        }

        private void ResolveReferences()
        {
            dialogueRoot ??= FindDialogueRoot();
            dialogueRoot ??= EnsureDefaultDialogueRootInScene();
            if (dialogueRoot == null)
                return;

            leftPortraitImage ??= FindImage(dialogueRoot.transform, true);
            rightPortraitImage ??= FindImage(dialogueRoot.transform, false);
            leftDarkOverlayImage ??= ResolveOverlay(leftPortraitImage);
            rightDarkOverlayImage ??= ResolveOverlay(rightPortraitImage);
            speakerNameLabel ??= FindText(dialogueRoot.transform, TextRole.Speaker) ?? FindText(null, TextRole.Speaker);
            dialogueTextLabel ??= FindText(dialogueRoot.transform, TextRole.Dialogue) ?? FindText(null, TextRole.Dialogue);
            nextButton ??= dialogueRoot.GetComponentInChildren<Button>(true);
        }

        private Image ResolveOverlay(Image portraitImage)
        {
            if (portraitImage == null)
                return null;

            var overlay = FindExistingOverlay(portraitImage);
            if (overlay == null)
                overlay = CreateOverlay(portraitImage);

            ConfigureOverlay(overlay, portraitImage);
            return overlay;
        }

        private Image CreateOverlay(Image portraitImage)
        {
            var overlayObject = new GameObject($"{portraitImage.name} Dark Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.transform.SetParent(portraitImage.transform, false);

            var rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            return overlayObject.GetComponent<Image>();
        }

        private Image FindExistingOverlay(Image portraitImage)
        {
            foreach (var image in portraitImage.GetComponentsInChildren<Image>(true))
            {
                if (image == null || image == portraitImage)
                    continue;
                if (IsOverlayName(image.name))
                    return image;
            }

            if (portraitImage.transform.parent == null)
                return null;

            foreach (var image in portraitImage.transform.parent.GetComponentsInChildren<Image>(true))
            {
                if (image == null || image == portraitImage)
                    continue;
                if (IsOverlayName(image.name) && IsNearPortrait(image.transform, portraitImage.transform))
                    return image;
            }

            return null;
        }

        private static bool IsNearPortrait(Transform candidate, Transform portrait)
        {
            return candidate.parent == portrait || candidate.parent == portrait.parent;
        }

        private static bool IsOverlayName(string name)
        {
            var normalized = Normalize(name);
            return normalized.Contains("shadow") || normalized.Contains("dark") ||
                   normalized.Contains("dim") || normalized.Contains("overlay");
        }

        private void ConfigureOverlay(Image overlay, Image portraitImage)
        {
            if (overlay == null)
                return;

            overlay.color = darkOverlayColor;
            overlay.raycastTarget = false;
            MatchOverlayToPortrait(overlay, portraitImage);
            overlay.enabled = false;
            overlay.gameObject.SetActive(false);
            overlay.transform.SetAsLastSibling();
        }

        private void SetOverlayVisible(Image overlay, bool visible, Sprite sprite)
        {
            if (overlay == null)
                return;

            overlay.sprite = visible ? sprite : null;
            overlay.preserveAspect = true;
            overlay.color = darkOverlayColor;
            overlay.enabled = visible;
            overlay.gameObject.SetActive(visible);
            if (visible)
                overlay.transform.SetAsLastSibling();
        }

        private void FitPortraitSize(Image image)
        {
            if (!autoFitPortraitSize || image == null || image.sprite == null)
                return;

            image.preserveAspect = true;
            var rect = image.rectTransform;
            var spriteRect = image.sprite.rect;
            if (spriteRect.width <= 0f || spriteRect.height <= 0f)
                return;

            var width = rect.rect.width;
            if (width <= 0f)
                width = Mathf.Abs(rect.sizeDelta.x);
            if (width <= 0f)
                return;

            var height = width * spriteRect.height / spriteRect.width;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private static void MatchOverlayToPortrait(Image overlay, Image portraitImage)
        {
            if (overlay == null || portraitImage == null)
                return;

            var overlayRect = overlay.rectTransform;
            var portraitRect = portraitImage.rectTransform;
            if (overlay.transform.parent == portraitImage.transform)
            {
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                overlayRect.pivot = new Vector2(0.5f, 0.5f);
                overlayRect.localRotation = Quaternion.identity;
                overlayRect.localScale = Vector3.one;
                return;
            }

            overlayRect.anchorMin = portraitRect.anchorMin;
            overlayRect.anchorMax = portraitRect.anchorMax;
            overlayRect.pivot = portraitRect.pivot;
            overlayRect.anchoredPosition = portraitRect.anchoredPosition;
            overlayRect.sizeDelta = portraitRect.sizeDelta;
            overlayRect.localRotation = portraitRect.localRotation;
            overlayRect.localScale = portraitRect.localScale;
        }
        private enum TextRole { Speaker, Dialogue }


        public static GameObject EnsureDefaultDialogueRootInScene()
        {
            var existing = FindDialogueRoot();
            if (existing != null)
                return existing;

            var canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                var canvasObject = new GameObject("Dialogue Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var root = new GameObject("Dialogue Root", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var panel = new GameObject("Dialogue Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0f);
            panelRect.anchorMax = new Vector2(0.92f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(0f, 210f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            CreatePortrait(root.transform, "Left Portrait", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(210f, 260f), new Vector2(170f, 70f));
            CreatePortrait(root.transform, "Right Portrait", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(210f, 260f), new Vector2(-170f, 70f));
            CreateText(panel.transform, "Speaker Name", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(-80f, 46f), 30f, TextAlignmentOptions.Left);
            CreateText(panel.transform, "Dialogue Text", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(-80f, -70f), 28f, TextAlignmentOptions.TopLeft);

            var button = new GameObject("Next Button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(panel.transform, false);
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-24f, 22f);
            buttonRect.sizeDelta = new Vector2(140f, 54f);
            button.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
            CreateText(button.transform, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24f, TextAlignmentOptions.Center).text = "NEXT";

            root.SetActive(false);
            return root;
        }

        private static Image CreatePortrait(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
        {
            var portrait = new GameObject(name, typeof(RectTransform), typeof(Image));
            portrait.transform.SetParent(parent, false);
            var rect = portrait.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = portrait.GetComponent<Image>();
            image.enabled = false;
            image.preserveAspect = true;
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, float size, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var label = textObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.text = string.Empty;
            return label;
        }
        private static GameObject FindDialogueRoot()
        {
            foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rect != null && IsDialogueRootName(rect.name))
                    return rect.gameObject;
            }

            var left = FindNamedPortraitTransform(true);
            var right = FindNamedPortraitTransform(false);
            var common = FindCommonAncestor(left, right);
            return common != null ? common.gameObject : null;
        }

        private static Transform FindNamedPortraitTransform(bool left)
        {
            foreach (var image in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (image == null || IsOverlayName(image.name))
                    continue;
                var normalized = Normalize(image.name);
                if (left && (normalized.Contains("left") || normalized.Contains("main")))
                    return image.transform;
                if (!left && (normalized.Contains("right") || normalized.Contains("sub")))
                    return image.transform;
            }
            return null;
        }

        private static Transform FindCommonAncestor(Transform first, Transform second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;

            var ancestors = new HashSet<Transform>();
            for (var current = first; current != null; current = current.parent)
                ancestors.Add(current);
            for (var current = second; current != null; current = current.parent)
                if (ancestors.Contains(current))
                    return current;
            return first.parent;
        }

        private static Image FindImage(Transform root, bool left)
        {
            Image fallback = null;
            Image bestByPosition = null;
            var bestScore = float.NegativeInfinity;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image == null || IsOverlayName(image.name) || image.GetComponentInChildren<TMP_Text>(true) != null)
                    continue;
                fallback ??= image;
                var normalized = Normalize(image.name);
                if (left && (normalized.Contains("left") || normalized.Contains("main") || image.name.Contains("왼")))
                    return image;
                if (!left && (normalized.Contains("right") || normalized.Contains("sub") || image.name.Contains("오른")))
                    return image;

                var rect = image.rectTransform;
                var centerX = rect.TransformPoint(rect.rect.center).x;
                var score = left ? -centerX : centerX;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestByPosition = image;
                }
            }
            return bestByPosition != null ? bestByPosition : fallback;
        }

        private static TMP_Text FindText(Transform root, TextRole role)
        {
            TMP_Text fallback = null;
            var texts = root != null
                ? root.GetComponentsInChildren<TMP_Text>(true)
                : FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in texts)
            {
                if (text == null)
                    continue;
                var normalized = Normalize(text.name);
                if (role == TextRole.Speaker)
                {
                    fallback ??= text;
                    if (normalized.Contains("name") || normalized.Contains("speaker") || text.name.Contains("\uC774\uB984"))
                        return text;
                    continue;
                }

                if (normalized.Contains("name") || normalized.Contains("speaker") || text.name.Contains("\uC774\uB984"))
                    continue;
                if (normalized.Contains("dialogue") || normalized.Contains("dialog") ||
                    normalized.Contains("line") || text.name.Contains("\uB300\uC0AC"))
                    return text;
                if (normalized.Contains("text"))
                    fallback ??= text;
            }
            return fallback;
        }

        private static bool IsDialogueRootName(string value)
        {
            var normalized = Normalize(value);
            return normalized == "dialogueui" || normalized == "dialogueuicanvas" ||
                   normalized == "dialogueroot" || normalized == "dialoguecanvas" ||
                   normalized == "eventui" || normalized == "eventcanvas" ||
                   normalized == "conversationui" || normalized == "talkui" ||
                   value.Contains("대화UI") || value.Contains("대화 UI") || value.Contains("대화캔버스");
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }    }
}
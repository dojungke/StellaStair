using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace StellaStair.Presentation
{
    public sealed class LevelUpUpgradePresenter : MonoBehaviour
    {
        private const int ChoiceCount = 3;

        private readonly Queue<TacticalUnit> pendingUnits = new();
        private readonly List<Button> optionButtons = new();
        private readonly List<TMP_Text> optionLabels = new();
        private readonly List<TMP_Text> optionDescriptionLabels = new();
        private readonly List<TacticalUnit.LevelUpUpgradeOption> visibleOptions = new();

        private DeploymentManager deployment;
        private Canvas canvas;
        [SerializeField] private GameObject overlay;
        [SerializeField] private GameObject panel;
        private TMP_Text titleLabel;
        private TMP_Text unitLabel;
        private TMP_Text hintLabel;
        private TacticalUnit currentUnit;
        private bool lockHeld;

        public event System.Action<TacticalUnit> UpgradeSelected;

        public bool HasPendingSelection => lockHeld || currentUnit != null || pendingUnits.Count > 0;

        public void Configure(DeploymentManager manager)
        {
            deployment = manager;
        }

        public void BindSceneUi(GameObject sceneOverlay, GameObject scenePanel = null)
        {
            overlay = sceneOverlay;
            panel = scenePanel;
            titleLabel = null;
            unitLabel = null;
            hintLabel = null;
            optionButtons.Clear();
            optionLabels.Clear();
            TryBindAssignedUi();
        }

        public void EnsureUiExistsInScene()
        {
            EnsureUi();
            if (overlay != null)
                overlay.SetActive(false);
        }

        public void Enqueue(TacticalUnit unit)
        {
            if (unit == null || !unit.IsAlive || unit.GetLevelUpUpgradeChoices(ChoiceCount).Count == 0)
                return;

            pendingUnits.Enqueue(unit);
            if (currentUnit == null)
                ShowNext();
        }

        private void Update()
        {
            if (overlay == null || !overlay.activeSelf || visibleOptions.Count == 0)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (WasChoicePressed(keyboard, 0))
                SelectVisibleOption(0);
            else if (WasChoicePressed(keyboard, 1))
                SelectVisibleOption(1);
            else if (WasChoicePressed(keyboard, 2))
                SelectVisibleOption(2);
        }

        private static bool WasChoicePressed(Keyboard keyboard, int index)
        {
            return index switch
            {
                0 => keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
                1 => keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
                2 => keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame,
                _ => false
            };
        }

        private void ShowNext()
        {
            currentUnit = null;
            while (pendingUnits.Count > 0 && currentUnit == null)
            {
                var candidate = pendingUnits.Dequeue();
                if (candidate != null && candidate.IsAlive && candidate.GetLevelUpUpgradeChoices(ChoiceCount).Count > 0)
                    currentUnit = candidate;
            }

            if (currentUnit == null)
            {
                HidePanel();
                return;
            }

            EnsureUi();
            visibleOptions.Clear();
            visibleOptions.AddRange(currentUnit.GetLevelUpUpgradeChoices(ChoiceCount));
            if (visibleOptions.Count == 0)
            {
                currentUnit = null;
                ShowNext();
                return;
            }

            if (!lockHeld)
            {
                deployment?.PushInteractionLock();
                lockHeld = true;
            }

            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            UpdateHeader();
            UpdateButtons();
        }

        private void UpdateHeader()
        {
            var displayName = currentUnit.Definition != null
                ? currentUnit.Definition.DisplayName
                : currentUnit.name;
            titleLabel.text = "레벨업 강화 선택";
            unitLabel.text = $"{displayName}  Lv.{currentUnit.CurrentLevel}   EXP {currentUnit.CurrentExperience}/{currentUnit.ExperienceToNextLevel}";
            hintLabel.text = "강화 1개를 선택하세요. 클릭 또는 숫자키 1 / 2 / 3";
        }

        private void UpdateButtons()
        {
            for (var i = 0; i < optionButtons.Count; i++)
            {
                var button = optionButtons[i];
                var active = i < visibleOptions.Count;
                button.gameObject.SetActive(active);
                button.onClick.RemoveAllListeners();
                if (!active)
                    continue;

                var optionIndex = i;
                var option = visibleOptions[i];
                optionLabels[i].text = $"<size=82%><color=#FFD86B>{i + 1}</color></size>  {option.Title}";
                optionDescriptionLabels[i].text = option.Description;
                button.onClick.AddListener(() => SelectVisibleOption(optionIndex));
            }

            if (EventSystem.current != null && visibleOptions.Count > 0)
                EventSystem.current.SetSelectedGameObject(optionButtons[0].gameObject);
        }

        private void SelectVisibleOption(int index)
        {
            if (index < 0 || index >= visibleOptions.Count)
                return;
            Select(visibleOptions[index]);
        }

        private void Select(TacticalUnit.LevelUpUpgradeOption option)
        {
            var upgradedUnit = currentUnit;
            if (upgradedUnit != null && upgradedUnit.IsAlive)
            {
                upgradedUnit.ApplyLevelUpUpgrade(option.Type);
                UpgradeSelected?.Invoke(upgradedUnit);
            }
            currentUnit = null;
            ShowNext();
        }

        private void HidePanel()
        {
            visibleOptions.Clear();
            if (overlay != null)
                overlay.SetActive(false);
            if (lockHeld)
            {
                deployment?.PopInteractionLock();
                lockHeld = false;
            }
        }

        private void OnDisable()
        {
            if (lockHeld)
            {
                deployment?.PopInteractionLock();
                lockHeld = false;
            }
        }

        private void OnDestroy()
        {
            if (lockHeld)
            {
                deployment?.PopInteractionLock();
                lockHeld = false;
            }
        }

        private void EnsureUi()
        {
            if (IsUiBound())
                return;

            if (TryBindAssignedUi())
                return;

            if (TryBindExistingUi())
                return;

            canvas = FindOverlayCanvas();
            if (canvas == null)
                canvas = CreateOverlayCanvas();
            else if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();
            CreateOverlay();
            CreatePanel();
            overlay.SetActive(false);
        }

        private bool IsUiBound()
        {
            return overlay != null && panel != null && canvas != null &&
                titleLabel != null && unitLabel != null && hintLabel != null &&
                optionButtons.Count >= ChoiceCount && optionLabels.Count >= ChoiceCount &&
                optionDescriptionLabels.Count >= ChoiceCount;
        }

        private bool TryBindAssignedUi()
        {
            if (overlay == null)
                return false;
            var rect = overlay.GetComponent<RectTransform>();
            if (rect == null)
                return false;
            return TryBindOverlay(rect, panel != null ? panel.transform : null);
        }

        private bool TryBindExistingUi()
        {
            foreach (var rect in FindObjectsOfType<RectTransform>(true))
            {
                if (rect == null || rect.name != "Level Up Upgrade Overlay")
                    continue;
                if (TryBindOverlay(rect, null))
                    return true;
            }
            return false;
        }

        private bool TryBindOverlay(RectTransform overlayRect, Transform explicitPanel)
        {
            overlay = overlayRect.gameObject;
            canvas = overlayRect.GetComponentInParent<Canvas>(true);
            panel = explicitPanel != null
                ? explicitPanel.gameObject
                : FindChildByName(overlayRect, "Level Up Upgrade Panel")?.gameObject;
            if (panel == null)
                return ClearBoundUi();

            titleLabel = FindChildText(panel.transform, "Title");
            unitLabel = FindChildText(panel.transform, "Unit Info");
            hintLabel = FindChildText(panel.transform, "Hint");
            optionButtons.Clear();
            optionLabels.Clear();
            optionDescriptionLabels.Clear();
            foreach (var button in overlayRect.GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                    continue;
                var titleText = FindOptionTitleText(button.transform);
                var descriptionText = FindOptionDescriptionText(button.transform, titleText);
                if (titleText == null || descriptionText == null)
                    continue;
                optionButtons.Add(button);
                optionLabels.Add(titleText);
                optionDescriptionLabels.Add(descriptionText);
            }
            SortOptionButtonsByX();
            if (titleLabel == null || unitLabel == null || hintLabel == null)
                BindFallbackHeaderTexts(panel.transform);

            if (canvas != null && titleLabel != null && unitLabel != null &&
                hintLabel != null && optionButtons.Count >= ChoiceCount &&
                optionLabels.Count >= ChoiceCount &&
                optionDescriptionLabels.Count >= ChoiceCount)
            {
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                EnsureEventSystem();
                overlay.SetActive(false);
                return true;
            }

            return ClearBoundUi();
        }

        private static TMP_Text FindOptionTitleText(Transform buttonRoot)
        {
            var title = FindChildText(buttonRoot, "Title");
            if (title != null)
                return title;
            var label = FindChildText(buttonRoot, "Label");
            if (label != null)
                return label;
            foreach (var text in buttonRoot.GetComponentsInChildren<TMP_Text>(true))
                if (text != null && !string.Equals(text.name, "Description", System.StringComparison.OrdinalIgnoreCase))
                    return text;
            return null;
        }

        private static TMP_Text FindOptionDescriptionText(Transform buttonRoot, TMP_Text titleText)
        {
            var description = FindChildText(buttonRoot, "Description");
            if (description != null)
                return description;
            foreach (var text in buttonRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text != null && text != titleText)
                    return text;
            }
            return null;
        }
        private void BindFallbackHeaderTexts(Transform textRoot)
        {
            var texts = new List<TMP_Text>();
            foreach (var text in textRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text != null)
                    texts.Add(text);
            }
            if (texts.Count == 0)
                return;
            titleLabel ??= texts[0];
            unitLabel ??= texts.Count > 1 ? texts[1] : texts[0];
            hintLabel ??= texts.Count > 2 ? texts[2] : texts[texts.Count - 1];
        }
        private void SortOptionButtonsByX()
        {
            for (var i = 0; i < optionButtons.Count - 1; i++)
            {
                for (var j = i + 1; j < optionButtons.Count; j++)
                {
                    if (optionButtons[i].transform.position.x <= optionButtons[j].transform.position.x)
                        continue;
                    (optionButtons[i], optionButtons[j]) = (optionButtons[j], optionButtons[i]);
                    (optionLabels[i], optionLabels[j]) = (optionLabels[j], optionLabels[i]);
                    (optionDescriptionLabels[i], optionDescriptionLabels[j]) = (optionDescriptionLabels[j], optionDescriptionLabels[i]);
                }
            }
        }

        private bool ClearBoundUi()
        {
            overlay = null;
            panel = null;
            canvas = null;
            titleLabel = null;
            unitLabel = null;
            hintLabel = null;
            optionButtons.Clear();
            optionLabels.Clear();
            optionDescriptionLabels.Clear();
            return false;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child != null && child.name == childName)
                    return child;
            return null;
        }

        private static TMP_Text FindChildText(Transform root, string childName)
        {
            var child = FindChildByName(root, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Canvas FindOverlayCanvas()
        {
            foreach (var candidate in FindObjectsOfType<Canvas>(true))
                if (candidate != null && candidate.renderMode != RenderMode.WorldSpace)
                    return candidate;
            return null;
        }

        private static Canvas CreateOverlayCanvas()
        {
            var canvasObject = new GameObject("Level Up Canvas");
            var createdCanvas = canvasObject.AddComponent<Canvas>();
            createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            createdCanvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
            return createdCanvas;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private void CreateOverlay()
        {
            overlay = new GameObject("Level Up Upgrade Overlay", typeof(RectTransform));
            overlay.transform.SetParent(canvas.transform, false);
            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = overlay.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.58f);
            image.raycastTarget = true;
        }

        private void CreatePanel()
        {
            panel = new GameObject("Level Up Upgrade Panel", typeof(RectTransform));
            panel.transform.SetParent(overlay.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(640f, 440f);
            rect.anchoredPosition = Vector2.zero;

            var image = panel.AddComponent<Image>();
            image.color = new Color(0.055f, 0.065f, 0.09f, 0.97f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleLabel = CreateLabel("Title", panel.transform, 32, TextAlignmentOptions.Center);
            titleLabel.color = new Color(1f, 0.86f, 0.32f, 1f);
            AddPreferredHeight(titleLabel.gameObject, 44f);

            unitLabel = CreateLabel("Unit Info", panel.transform, 19, TextAlignmentOptions.Center);
            unitLabel.color = new Color(0.82f, 0.9f, 1f, 1f);
            AddPreferredHeight(unitLabel.gameObject, 28f);

            for (var i = 0; i < ChoiceCount; i++)
                optionButtons.Add(CreateOptionButton(panel.transform, optionLabels, optionDescriptionLabels));

            hintLabel = CreateLabel("Hint", panel.transform, 16, TextAlignmentOptions.Center);
            hintLabel.color = new Color(0.72f, 0.76f, 0.84f, 1f);
            AddPreferredHeight(hintLabel.gameObject, 28f);
        }

        private static TMP_Text CreateLabel(
            string objectName, Transform parent, int fontSize, TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.enableWordWrapping = true;
            return label;
        }

        private static Button CreateOptionButton(
            Transform parent, List<TMP_Text> labels, List<TMP_Text> descriptionLabels)
        {
            var buttonObject = new GameObject("Upgrade Option Button", typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.19f, 0.28f, 0.98f);
            var button = buttonObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.15f, 0.19f, 0.28f, 0.98f);
            colors.highlightedColor = new Color(0.28f, 0.36f, 0.55f, 1f);
            colors.selectedColor = new Color(0.25f, 0.33f, 0.5f, 1f);
            colors.pressedColor = new Color(0.1f, 0.13f, 0.2f, 1f);
            button.colors = colors;

            AddPreferredHeight(buttonObject, 86f);

            var label = CreateLabel("Label", buttonObject.transform, 20, TextAlignmentOptions.MidlineLeft);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(20f, -4f);
            labelRect.offsetMax = new Vector2(-20f, -8f);
            labels.Add(label);

            var description = CreateLabel("Description", buttonObject.transform, 14, TextAlignmentOptions.TopLeft);
            description.color = new Color(0.82f, 0.87f, 0.95f, 1f);
            var descriptionRect = description.GetComponent<RectTransform>();
            descriptionRect.anchorMin = new Vector2(0f, 0f);
            descriptionRect.anchorMax = new Vector2(1f, 0.5f);
            descriptionRect.offsetMin = new Vector2(20f, 8f);
            descriptionRect.offsetMax = new Vector2(-20f, 2f);
            descriptionLabels.Add(description);
            return button;
        }
        private static void AddPreferredHeight(GameObject target, float height)
        {
            var layout = target.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }
    }
}
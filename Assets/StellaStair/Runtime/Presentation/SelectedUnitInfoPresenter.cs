using System.Collections.Generic;
using StellaStair.Grid;
using StellaStair.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StellaStair.Presentation
{
    public sealed class SelectedUnitInfoPresenter : MonoBehaviour
    {
        private Canvas canvas;
        private GameObject panel;
        private Image portrait;
        private TMP_Text nameLabel;
        private TMP_Text levelLabel;
        private TMP_Text experienceLabel;
        private Button upgradesButton;
        private GameObject upgradesPopup;
        private TMP_Text upgradesLabel;
        private TacticalUnit trackedUnit;

        public bool BindSceneUi(GameObject scenePanel)
        {
            if (scenePanel == null) return false;
            var scenePortrait = FindChild(scenePanel.transform, "Portrait")?.GetComponent<Image>();
            var sceneName = FindChild(scenePanel.transform, "Name")?.GetComponent<TMP_Text>();
            var sceneLevel = FindChild(scenePanel.transform, "Level")?.GetComponent<TMP_Text>();
            var sceneExperience = FindChild(scenePanel.transform, "Experience")?.GetComponent<TMP_Text>();
            if (scenePortrait == null || sceneName == null || sceneLevel == null || sceneExperience == null) return false;
            panel = scenePanel;
            canvas = scenePanel.GetComponentInParent<Canvas>(true);
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            portrait = scenePortrait;
            nameLabel = sceneName;
            levelLabel = sceneLevel;
            experienceLabel = sceneExperience;
            upgradesButton = FindChild(scenePanel.transform, "Upgrades Button")?.GetComponent<Button>();
            upgradesPopup = FindChild(scenePanel.transform, "Upgrade History")?.gameObject;
            upgradesLabel = FindChild(scenePanel.transform, "Upgrade History Text")?.GetComponent<TMP_Text>();
            if (upgradesButton == null || upgradesPopup == null || upgradesLabel == null)
                CreateUpgradeHistoryUi(scenePanel.transform);
            BindUpgradeButton();
            return canvas != null;
        }

        private bool TryBindSceneUi()
        {
            foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
                if (rect != null && rect.name == "Selected Unit Info" && BindSceneUi(rect.gameObject)) return true;
            return false;
        }

        public void ShowWoodTile(TacticalBoard board, GridPosition position)
        {
            if (nameLabel == null || levelLabel == null || experienceLabel == null || portrait == null) TryBindSceneUi();
            if (nameLabel == null || levelLabel == null || experienceLabel == null || portrait == null) EnsureUi();
            if (nameLabel == null || levelLabel == null || experienceLabel == null || portrait == null) return;
            Unsubscribe();
            trackedUnit = null;
            var data = board != null ? board.WoodObjectData : null;
            nameLabel.text = data != null ? data.DisplayName : "나무 타일";
            levelLabel.text = data != null && !string.IsNullOrWhiteSpace(data.FunctionDescription) ? data.FunctionDescription : "파괴 가능";
            var health = board != null ? board.GetWoodHealth(position) : 0;
            var maxHealth = board != null ? board.WoodMaxHealth : 0;
            experienceLabel.text = data != null && !string.IsNullOrWhiteSpace(data.Description) ? data.Description : $"체력 {health} / {maxHealth}";
            portrait.sprite = data != null ? data.Sprite : null;
            portrait.enabled = portrait.sprite != null;
            if (panel != null) panel.SetActive(true);
            HideUpgradeHistory();
        }

        public void Show(TacticalUnit unit)
        {
            if (unit == null || !unit.IsAlive) { Hide(); return; }
            EnsureUi();
            Unsubscribe();
            trackedUnit = unit;
            trackedUnit.ExperienceChanged += OnExperienceChanged;
            trackedUnit.Died += OnUnitDied;
            Refresh();
            panel.SetActive(true);
            HideUpgradeHistory();
        }

        public void Hide()
        {
            Unsubscribe();
            trackedUnit = null;
            if (panel != null) panel.SetActive(false);
            HideUpgradeHistory();
        }

        private void BindUpgradeButton()
        {
            if (upgradesButton == null) return;
            upgradesButton.onClick.RemoveListener(ToggleUpgradeHistory);
            upgradesButton.onClick.AddListener(ToggleUpgradeHistory);
        }

        private void ToggleUpgradeHistory()
        {
            if (upgradesPopup == null || upgradesLabel == null || trackedUnit == null) return;
            if (upgradesPopup.activeSelf) { HideUpgradeHistory(); return; }
            upgradesLabel.text = BuildUpgradeHistory(trackedUnit);
            upgradesPopup.SetActive(true);
        }

        private void HideUpgradeHistory()
        {
            if (upgradesPopup != null) upgradesPopup.SetActive(false);
        }

        private static string BuildUpgradeHistory(TacticalUnit unit)
        {
            var p = unit.CaptureProgress();
            var lines = new List<string>();
            AddRepeated(lines, "최대 체력 +1", p.HealthUpgradeCount);
            AddRepeated(lines, "기본 공격 피해량 +1", p.AttackUpgradeCount);
            AddRepeated(lines, "이동 범위 +1", p.MovementUpgradeCount);
            if (p.HasThrustAttack) lines.Add("찌르기 해금");
            AddRepeated(lines, "찌르기 첫 칸 피해량 +1", p.ThrustFrontDamageBonus);
            AddRepeated(lines, "찌르기 두 번째 칸 피해량 +1", p.ThrustBackDamageBonus);
            if (p.ThrustHasKnockback) lines.Add("찌르기 밀치기");
            if (p.HasGuardianPassive) lines.Add("수호자 패시브");
            if (p.HasCouragePassive) lines.Add("용기 패시브");
            if (p.HasPiercingArrowAttack) lines.Add("관통 화살 해금");
            AddRepeated(lines, "관통 화살 피해량 +1", p.PiercingArrowDamageBonus);
            if (p.HasBowStrikeAttack) lines.Add("활격 해금");
            AddRepeated(lines, "활격 피해량 +1", p.BowStrikeDamageBonus);
            if (p.HasHarpoonAttack) lines.Add("작살 해금");
            if (p.HasAgilityPassive) lines.Add("민첩 패시브");
            if (p.HasCoverPassive) lines.Add("엄폐 패시브");
            if (p.HasFireballAttack) lines.Add("화염구 해금");
            AddRepeated(lines, "화염구 피해량 +1", p.FireballDamageBonus);
            AddRepeated(lines, "화염구 쿨타임 -1", p.FireballCooldownReduction);
            if (p.HasIceSpikeAttack) lines.Add("얼음 쐐기 해금");
            AddRepeated(lines, "얼음 쐐기 쿨타임 -1", p.IceSpikeCooldownReduction);
            if (p.HasNatureFragranceAttack) lines.Add("자연의 향기 해금");
            AddRepeated(lines, "자연의 향기 쿨타임 -1", p.NatureFragranceCooldownReduction);
            AddRepeated(lines, "자연의 향기 치유량 +1", p.NatureFragranceHealBonus);
            if (p.HasArcaneAccelerationPassive) lines.Add("패시브: 마도 증속");
            return lines.Count == 0 ? "아직 얻은 강화가 없습니다." : string.Join("\n", lines);
        }

        private static void AddRepeated(List<string> lines, string text, int count)
        {
            for (var i = 0; i < count; i++) lines.Add(text);
        }

        private void OnExperienceChanged(TacticalUnit unit, int current, int required) => Refresh();
        private void OnUnitDied(TacticalUnit unit) => Hide();

        private void Refresh()
        {
            if (trackedUnit == null) return;
            var definition = trackedUnit.Definition;
            var isObject = trackedUnit.IsObjective || trackedUnit.IsCrate;
            nameLabel.text = isObject && !string.IsNullOrWhiteSpace(trackedUnit.ObjectiveDisplayName) ? trackedUnit.ObjectiveDisplayName : definition != null ? definition.DisplayName : trackedUnit.name;
            levelLabel.text = trackedUnit.IsObjective ? (string.IsNullOrWhiteSpace(trackedUnit.ObjectFunctionDescription) ? "목표" : trackedUnit.ObjectFunctionDescription) : trackedUnit.IsCrate ? (string.IsNullOrWhiteSpace(trackedUnit.ObjectFunctionDescription) ? (trackedUnit.IsExplosiveCrate ? "밀치기 가능 · 폭발" : "밀치기 가능") : trackedUnit.ObjectFunctionDescription) : $"레벨 {trackedUnit.CurrentLevel}";
            experienceLabel.text = isObject ? (string.IsNullOrWhiteSpace(trackedUnit.ObjectiveDescription) ? "설명 없음" : trackedUnit.ObjectiveDescription) : trackedUnit.Team == UnitTeam.Enemy ? definition != null && !string.IsNullOrWhiteSpace(definition.Description) ? definition.Description : "설명 없음" : $"다음 레벨까지 {trackedUnit.ExperienceToNextLevel - trackedUnit.CurrentExperience} EXP";
            portrait.sprite = definition != null ? definition.UnitSprite : null;
            if (portrait.sprite == null)
            {
                var renderer = trackedUnit.GetComponentInChildren<SpriteRenderer>(true);
                portrait.sprite = renderer != null ? renderer.sprite : null;
            }
            portrait.enabled = portrait.sprite != null;
        }

        private void EnsureUi()
        {
            if (canvas != null && panel != null) return;
            var canvasObject = new GameObject("Selected Unit Info Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            panel = new GameObject("Selected Unit Info", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.zero; panelRect.pivot = Vector2.zero;
            panelRect.anchoredPosition = new Vector2(28f, 28f); panelRect.sizeDelta = new Vector2(360f, 120f);
            panel.transform.localScale = Vector3.one * 2f;
            panel.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 0.94f);
            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(panel.transform, false);
            var portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, .5f); portraitRect.anchorMax = new Vector2(0f, .5f); portraitRect.pivot = new Vector2(0f, .5f);
            portraitRect.anchoredPosition = new Vector2(16f, 0f); portraitRect.sizeDelta = new Vector2(100f, 100f);
            portrait = portraitObject.GetComponent<Image>(); portrait.preserveAspect = true;
            nameLabel = CreateLabel("Name", panel.transform, 24, new Vector2(140f, -28f));
            levelLabel = CreateLabel("Level", panel.transform, 19, new Vector2(140f, 4f));
            experienceLabel = CreateLabel("Experience", panel.transform, 17, new Vector2(140f, 36f));
            CreateUpgradeHistoryUi(panel.transform);
            panel.SetActive(false);
        }

        private void CreateUpgradeHistoryUi(Transform parent)
        {
            var buttonObject = new GameObject("Upgrades Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.one; buttonRect.anchorMax = Vector2.one; buttonRect.pivot = Vector2.one;
            buttonRect.anchoredPosition = new Vector2(-8f, -8f); buttonRect.sizeDelta = new Vector2(26f, 26f);
            var buttonTextObject = new GameObject("Text", typeof(RectTransform));
            buttonTextObject.transform.SetParent(buttonObject.transform, false);
            var buttonText = buttonTextObject.AddComponent<TextMeshProUGUI>();
            SetDefaultFont(buttonText); buttonText.fontSize = 20; buttonText.color = Color.black; buttonText.text = "+"; buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.rectTransform.anchorMin = Vector2.zero; buttonText.rectTransform.anchorMax = Vector2.one; buttonText.rectTransform.sizeDelta = Vector2.zero;
            upgradesButton = buttonObject.GetComponent<Button>();
            var popup = new GameObject("Upgrade History", typeof(RectTransform), typeof(Image));
            popup.transform.SetParent(parent, false);
            var popupRect = popup.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0f, 1f); popupRect.anchorMax = Vector2.one; popupRect.pivot = new Vector2(0f, 0f); popupRect.anchoredPosition = new Vector2(0f, 8f); popupRect.sizeDelta = new Vector2(0f, 210f);
            popup.GetComponent<Image>().color = new Color(.025f, .035f, .05f, .98f);
            upgradesPopup = popup;
            var textObject = new GameObject("Upgrade History Text", typeof(RectTransform));
            textObject.transform.SetParent(popup.transform, false);
            upgradesLabel = textObject.AddComponent<TextMeshProUGUI>();
            SetDefaultFont(upgradesLabel); upgradesLabel.fontSize = 15; upgradesLabel.color = Color.white; upgradesLabel.enableWordWrapping = true; upgradesLabel.alignment = TextAlignmentOptions.TopLeft;
            upgradesLabel.rectTransform.anchorMin = Vector2.zero; upgradesLabel.rectTransform.anchorMax = Vector2.one; upgradesLabel.rectTransform.offsetMin = new Vector2(12f, 10f); upgradesLabel.rectTransform.offsetMax = new Vector2(-12f, -10f);
            popup.SetActive(false);
            BindUpgradeButton();
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child != null && child.name == childName) return child;
            return null;
        }

        private static void SetDefaultFont(TMP_Text label)
        {
            if (label == null) return;
            try
            {
                var font = TMP_Settings.defaultFontAsset;
                if (font != null) label.font = font;
            }
            catch (System.NullReferenceException)
            {
                // TMP settings may not exist yet in a freshly created runtime UI.
            }
        }

        private static TMP_Text CreateLabel(string objectName, Transform parent, int fontSize, Vector2 position)
        {
            var objectRoot = new GameObject(objectName, typeof(RectTransform)); objectRoot.transform.SetParent(parent, false);
            var rect = objectRoot.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0f, .5f); rect.anchorMax = new Vector2(1f, .5f); rect.pivot = new Vector2(0f, .5f); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(-156f, 30f);
            var label = objectRoot.AddComponent<TextMeshProUGUI>(); SetDefaultFont(label); label.fontSize = fontSize; label.color = Color.white; label.enableWordWrapping = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        private void Unsubscribe()
        {
            if (trackedUnit == null) return;
            trackedUnit.ExperienceChanged -= OnExperienceChanged;
            trackedUnit.Died -= OnUnitDied;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
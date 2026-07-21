using System;
using System.Collections.Generic;
using StellaStair.Grid;
using StellaStair.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StellaStair.Town
{
    public sealed class TownHubPresenter : MonoBehaviour
    {
        private readonly List<TacticalMapData> stages = new();
        private readonly List<string> partyKeys = new();
        private readonly List<TownItemData> catalog = new();
        private GameObject root;
        private TMP_Text goldLabel;
        private TMP_Text innStatusLabel;
        private Transform shopContent;
        private Transform guildContent;
        private Transform innContent;
        private Transform innPartyTabs;
        private Transform innEquipmentTabs;
        private Image innPortrait;
        private Button armorSlotButton;
        private Button weaponSlotButton;
        private TMP_Text armorSlotLabel;
        private TMP_Text weaponSlotLabel;
        private GameObject buildingSelection;
        private GameObject shopPanel;
        private GameObject guildPanel;
        private GameObject innPanel;
        private Image guildInteriorBackground;
        private Image guildDialogueBackground;
        private string selectedPartyKey;
        private EquipmentSlot selectedEquipmentSlot = EquipmentSlot.Weapon;
        private Action<TacticalMapData> stageSelected;
        private Action guildOpened;

        public GameObject TownRoot => root;

        public void EnsureUiExistsInScene()
        {
            EnsureUi();
            if (root != null) root.SetActive(false);
        }

        public void Show(
            IReadOnlyList<TacticalMapData> availableStages,
            IReadOnlyList<TacticalUnit> party,
            Action<TacticalMapData> onStageSelected,
            Action onGuildOpened = null)
        {
            EnsureUi();
            stages.Clear();
            if (availableStages != null)
                foreach (var stage in availableStages)
                    if (stage != null) stages.Add(stage);

            partyKeys.Clear();
            AddUnlockedPartyKeys();
            if (party != null)
                foreach (var unit in party)
                    if (unit != null && !string.IsNullOrWhiteSpace(unit.ProgressKey) &&
                        TownProgressState.IsPartyMemberUnlocked(unit.ProgressKey) && !partyKeys.Contains(unit.ProgressKey))
                        partyKeys.Add(unit.ProgressKey);
            selectedPartyKey = partyKeys.Count > 0 ? partyKeys[0] : "Knight";
            stageSelected = onStageSelected;
            guildOpened = onGuildOpened;

            catalog.Clear();
            var assets = Resources.LoadAll<TownItemData>("TownItems");
            if (assets != null && assets.Length > 0) catalog.AddRange(assets);
            else catalog.AddRange(TownProgressState.GetDefaultCatalog());

            root.SetActive(true);
            RefreshAll();
            ShowTownOverview();
        }

        public void RefreshUnlockedParty()
        {
            AddUnlockedPartyKeys();
            if (string.IsNullOrWhiteSpace(selectedPartyKey) && partyKeys.Count > 0)
                selectedPartyKey = partyKeys[0];
            if (root != null && root.activeSelf)
                RefreshInn();
        }

        public void SetTownUiVisible(bool visible)
        {
            if (root != null) root.SetActive(visible);
        }

        public void SetGuildDialogueMode(bool visible, GameObject dialogueRoot = null)
        {
            EnsureUi();
            EnsureGuildInteriorVisuals();
            if (root == null) return;

            root.SetActive(true);
            if (guildDialogueBackground != null)
            {
                if (visible && dialogueRoot != null)
                {
                    guildDialogueBackground.transform.SetParent(dialogueRoot.transform, false);
                    Stretch(guildDialogueBackground.rectTransform);
                    guildDialogueBackground.transform.SetAsFirstSibling();
                }
                else if (!visible && guildDialogueBackground.transform.parent != root.transform)
                {
                    guildDialogueBackground.transform.SetParent(root.transform, false);
                    Stretch(guildDialogueBackground.rectTransform);
                    guildDialogueBackground.transform.SetSiblingIndex(Mathf.Min(1, root.transform.childCount - 1));
                }
                guildDialogueBackground.gameObject.SetActive(visible);
            }
            if (goldLabel != null)
                goldLabel.gameObject.SetActive(!visible);
            if (buildingSelection != null)
                buildingSelection.SetActive(false);
            if (shopPanel != null)
                shopPanel.SetActive(false);
            if (innPanel != null)
                innPanel.SetActive(false);
            if (guildPanel != null)
                guildPanel.SetActive(!visible);
            if (guildInteriorBackground != null)
                guildInteriorBackground.gameObject.SetActive(!visible);

            // The town canvas renders above the battle canvas, so only the reparented dialogue backdrop stays visible.
            root.SetActive(!visible);
        }

        private void EnsureGuildInteriorVisuals()
        {
            if (root == null || guildPanel == null) return;

            var sprite = Resources.Load<Sprite>("TownArt/GuildInterior");
            guildInteriorBackground ??= EnsureGuildBackground("Guild Interior Background", root.transform, 1);
            guildDialogueBackground ??= EnsureGuildBackground("Guild Dialogue Background", root.transform, 1);
            if (guildInteriorBackground != null)
            {
                guildInteriorBackground.sprite = sprite;
                guildInteriorBackground.gameObject.SetActive(false);
            }
            if (guildDialogueBackground != null)
            {
                guildDialogueBackground.sprite = sprite;
                guildDialogueBackground.gameObject.SetActive(false);
            }
        }

        private static Image EnsureGuildBackground(string objectName, Transform parent, int siblingIndex)
        {
            var existing = FindChild(parent, objectName);
            var backgroundObject = existing != null
                ? existing.gameObject
                : new GameObject(objectName, typeof(RectTransform), typeof(Image));
            if (backgroundObject.transform.parent != parent)
                backgroundObject.transform.SetParent(parent, false);

            var rect = backgroundObject.GetComponent<RectTransform>();
            Stretch(rect);
            backgroundObject.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));
            var image = backgroundObject.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private void AddUnlockedPartyKeys()
        {
            foreach (var key in TownProgressState.GetUnlockedPartyKeys())
                if (!partyKeys.Contains(key)) partyKeys.Add(key);
        }

        private void RefreshAll()        {
            if (goldLabel != null) goldLabel.text = $"보유 골드  {TownProgressState.Gold} G";
            RefreshShop();
            RefreshGuild();
            RefreshInn();
        }

        private void RefreshShop()
        {
            ClearChildren(shopContent);
            foreach (var item in catalog)
            {
                var captured = item;
                var owned = TownProgressState.GetOwnedCount(item);
                CreateListButton(shopContent,
                    $"[{GetItemCategoryName(item)}] {item.DisplayName}   {item.Price} G\n{item.Description}   보유 {owned}",
                    () =>
                    {
                        if (!TownProgressState.TryPurchase(captured)) return;
                        RefreshAll();
                    }, TownProgressState.Gold >= item.Price);
            }
        }

        private void RefreshGuild()
        {
            ClearChildren(guildContent);
            if (stages.Count == 0)
            {
                CreateMessage(guildContent, "등록된 스테이지가 없습니다.");
                return;
            }
            var choicesRow = CreateGuildChoicesRow(guildContent);
            foreach (var stage in stages)
            {
                var captured = stage;
                CreateGuildStageButton(choicesRow, stage.DisplayName, stage.mapDescription, () => stageSelected?.Invoke(captured));
            }
        }

        private void RefreshInn()
        {
            ClearChildren(innPartyTabs);
            ClearChildren(innEquipmentTabs);
            ClearChildren(innContent);
            var armor = TownProgressState.GetEquippedItem(selectedPartyKey, EquipmentSlot.Armor);
            var weapon = TownProgressState.GetEquippedItem(selectedPartyKey, EquipmentSlot.Weapon);
            var displayName = GetUnitDisplayName(selectedPartyKey);
            if (innStatusLabel != null)
                innStatusLabel.text = displayName;
            if (innPortrait != null)
            {
                innPortrait.sprite = LoadUnitPortrait(selectedPartyKey);
                innPortrait.enabled = innPortrait.sprite != null;
                var portraitFitter = innPortrait.GetComponent<AspectRatioFitter>();
                if (portraitFitter != null && innPortrait.sprite != null)
                    portraitFitter.aspectRatio = innPortrait.sprite.rect.width / innPortrait.sprite.rect.height;
                var wizardPortrait = selectedPartyKey.IndexOf("Wizard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     selectedPartyKey.IndexOf("Mage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     selectedPartyKey.Contains("마법사");
                innPortrait.rectTransform.localScale = Vector3.one * (wizardPortrait ? 1.35f : 1f);
                innPortrait.rectTransform.anchoredPosition = wizardPortrait ? new Vector2(0f, -8f) : Vector2.zero;
            }

            foreach (var key in partyKeys)
            {
                var capturedKey = key;
                CreatePartyTab(innPartyTabs, GetUnitDisplayName(key), key == selectedPartyKey, () =>
                {
                    selectedPartyKey = capturedKey;
                    RefreshInn();
                });
            }

            SetEquipmentSlot(armorSlotLabel, "방어구", armor, "모든 캐릭터 공용");
            SetEquipmentSlot(weaponSlotLabel, "무기", weapon,
                $"{GetWeaponKindName(TownProgressState.GetRequiredWeaponKind(selectedPartyKey))} 전용");

            CreateEquipmentTab("무기", EquipmentSlot.Weapon);
            CreateEquipmentTab("방어구", EquipmentSlot.Armor);
            var categoryName = selectedEquipmentSlot == EquipmentSlot.Weapon
                ? $"무기 · {GetWeaponKindName(TownProgressState.GetRequiredWeaponKind(selectedPartyKey))}"
                : "방어구 · 모든 캐릭터 공용";
            CreateMessage(innContent, categoryName);
            if (!CreateInventoryItems(selectedEquipmentSlot))
                CreateMessage(innContent, "장착 가능한 보유 아이템이 없습니다.");
        }

        private void CreateEquipmentTab(string label, EquipmentSlot slot)
        {
            CreatePartyTab(innEquipmentTabs, label, selectedEquipmentSlot == slot, () =>
            {
                selectedEquipmentSlot = slot;
                RefreshInn();
            });
        }

        private bool CreateInventoryItems(EquipmentSlot slot)
        {
            var found = false;
            foreach (var item in catalog)
            {
                if (item == null || item.EquipmentSlot != slot || TownProgressState.GetOwnedCount(item) <= 0) continue;
                if (slot == EquipmentSlot.Weapon && !TownProgressState.CanEquip(selectedPartyKey, item)) continue;
                found = true;
                var captured = item;
                var equipped = TownProgressState.GetEquippedItem(selectedPartyKey, slot) == item;
                var available = TownProgressState.GetAvailableCount(item);
                CreateListButton(innContent,
                    $"{item.DisplayName}  ·  사용 가능 {available}\n{item.Description}",
                    () =>
                    {
                        if (TownProgressState.TryEquip(selectedPartyKey, captured)) RefreshInn();
                    }, equipped || available > 0, equipped);
            }
            return found;
        }

        private static void SetEquipmentSlot(TMP_Text label, string slotName, TownItemData item, string rule)
        {
            if (label == null) return;
            label.text = item != null
                ? $"{slotName}\n{item.DisplayName}\n{item.Description}"
                : $"{slotName}\n비어 있음\n{rule}";
        }

        private static Sprite LoadUnitPortrait(string key)
        {
            if (key.IndexOf("Knight", StringComparison.OrdinalIgnoreCase) >= 0)
                return Resources.Load<Sprite>("UnitPortraits/KnightPortrait");
            if (key.IndexOf("Archer", StringComparison.OrdinalIgnoreCase) >= 0)
                return Resources.Load<Sprite>("UnitPortraits/ArcherPortrait");
            if (key.IndexOf("Wizard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("Mage", StringComparison.OrdinalIgnoreCase) >= 0)
                return Resources.Load<Sprite>("UnitPortraits/WizardPortrait");
            return null;
        }

        private static void CreatePartyTab(Transform parent, string text, bool selected, Action action)
        {
            var buttonObject = new GameObject("Party Tab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = selected ? 58f : 50f;
            layout.flexibleWidth = 1f;
            buttonObject.GetComponent<Image>().color = selected
                ? new Color(.88f, .76f, .38f, 1f)
                : new Color(.55f, .5f, .38f, .98f);
            buttonObject.GetComponent<Button>().onClick.AddListener(() => action?.Invoke());
            var label = CreateText("Text", buttonObject.transform, 20, TextAlignmentOptions.Center);
            label.text = text;
            label.color = Color.black;
            Stretch(label.rectTransform, 6f);
        }
        private static string GetItemCategoryName(TownItemData item)
        {
            if (item == null || item.EquipmentSlot == EquipmentSlot.Armor) return "방어구";
            return GetWeaponKindName(item.WeaponKind);
        }

        private static string GetWeaponKindName(WeaponKind kind)
        {
            return kind switch
            {
                WeaponKind.Sword => "검",
                WeaponKind.Bow => "활",
                WeaponKind.Staff => "지팡이",
                _ => "무기"
            };
        }
        private void EnsureUi()
        {
            if (root != null) return;
            var canvasObject = new GameObject("Town Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            root = new GameObject("Town Hub", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvasObject.transform, false);
            Stretch(root.GetComponent<RectTransform>());
            var background = root.GetComponent<Image>();
            background.sprite = Resources.Load<Sprite>("BattleArt/ForestVillageBackground");
            background.color = background.sprite != null ? Color.white : new Color(.12f, .16f, .14f, 1f);
            background.preserveAspect = false;

            var shade = new GameObject("Town Shade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(root.transform, false);
            Stretch(shade.GetComponent<RectTransform>());
            shade.GetComponent<Image>().color = new Color(0f, 0f, 0f, .38f);

            goldLabel = CreateText("Gold", root.transform, 30, TextAlignmentOptions.Center);
            var goldRect = goldLabel.rectTransform;
            goldRect.anchorMin = new Vector2(.35f, .92f); goldRect.anchorMax = new Vector2(.65f, .99f);
            goldRect.offsetMin = goldRect.offsetMax = Vector2.zero;

            CreateBuildingSelection();
            shopContent = CreateSection("Shop", "상점", out _, out shopPanel);
            guildContent = CreateSection("Guild", "길드", out _, out guildPanel);
            innContent = CreateInnSection(out innStatusLabel, out innPanel);
            EnsureGuildInteriorVisuals();
            root.SetActive(false);
        }

        private void CreateBuildingSelection()
        {
            buildingSelection = new GameObject("Town Buildings", typeof(RectTransform));
            buildingSelection.transform.SetParent(root.transform, false);
            Stretch(buildingSelection.GetComponent<RectTransform>());
            CreateBuildingButton("Shop Building", "TownArt/ShopBuilding", "상점", 0.015f, .335f, () => OpenFacility(shopPanel));
            CreateBuildingButton("Guild Building", "TownArt/GuildBuilding", "길드", .335f, .665f, () => OpenFacility(guildPanel));
            CreateBuildingButton("Inn Building", "TownArt/InnBuilding", "여관", .665f, .985f, () => OpenFacility(innPanel));
        }

        private void CreateBuildingButton(string objectName, string resourcePath, string labelText, float minX, float maxX, Action action)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(buildingSelection.transform, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, .12f); rect.anchorMax = new Vector2(maxX, .86f);
            rect.offsetMin = new Vector2(8f, 0f); rect.offsetMax = new Vector2(-8f, 0f);
            var image = buttonObject.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(resourcePath);
            image.preserveAspect = true;
            image.color = Color.white;
            buttonObject.GetComponent<Button>().onClick.AddListener(() => action?.Invoke());
            var label = CreateText("Label", buttonObject.transform, 30, TextAlignmentOptions.Center);
            label.text = labelText;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.anchorMin = new Vector2(.12f, 0f); label.rectTransform.anchorMax = new Vector2(.88f, .13f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }

        private void ShowTownOverview()
        {
            if (buildingSelection != null) buildingSelection.SetActive(true);
            if (shopPanel != null) shopPanel.SetActive(false);
            if (guildPanel != null) guildPanel.SetActive(false);
            if (innPanel != null) innPanel.SetActive(false);
            if (guildInteriorBackground != null) guildInteriorBackground.gameObject.SetActive(false);
        }

        private void OpenFacility(GameObject target)
        {
            if (buildingSelection != null) buildingSelection.SetActive(false);
            if (shopPanel != null) shopPanel.SetActive(target == shopPanel);
            if (guildPanel != null) guildPanel.SetActive(target == guildPanel);
            if (innPanel != null) innPanel.SetActive(target == innPanel);
            if (guildInteriorBackground != null)
                guildInteriorBackground.gameObject.SetActive(target == guildPanel);
            if (target == guildPanel) guildOpened?.Invoke();
        }

        private bool TryBindSceneUi()
        {
            foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
            {
                if (rect == null || rect.name != "Town Hub") continue;
                var shop = FindChild(rect, "Shop");
                var guild = FindChild(rect, "Guild");
                var inn = FindChild(rect, "Inn");
                root = rect.gameObject;
                shopPanel = shop != null ? shop.gameObject : null;
                guildPanel = guild != null ? guild.gameObject : null;
                innPanel = inn != null ? inn.gameObject : null;
                buildingSelection = FindChild(rect, "Town Buildings")?.gameObject;
                goldLabel = FindChild(rect, "Gold")?.GetComponent<TMP_Text>();
                shopContent = FindChild(shop, "Content");
                guildContent = FindChild(guild, "Content");

                if (innPanel != null &&
                    (FindChild(inn, "Party Tabs") == null || FindChild(inn, "Equipment Tabs") == null ||
                     FindChild(inn, "Portrait Image") == null ||
                     FindChild(inn, "Portrait Image").GetComponent<AspectRatioFitter>() == null))
                {
                    ConfigureInnPanel(innPanel);
                    innContent = BuildInnPanel(innPanel, out innStatusLabel);
                }
                else
                {
                    innPartyTabs = FindChild(inn, "Party Tabs");
                    innEquipmentTabs = FindChild(inn, "Equipment Tabs");
                    innPortrait = FindChild(inn, "Portrait Image")?.GetComponent<Image>();
                    armorSlotButton = FindChild(inn, "Armor Slot")?.GetComponent<Button>();
                    weaponSlotButton = FindChild(inn, "Weapon Slot")?.GetComponent<Button>();
                    armorSlotLabel = FindChild(armorSlotButton != null ? armorSlotButton.transform : null, "Slot Text")?.GetComponent<TMP_Text>();
                    weaponSlotLabel = FindChild(weaponSlotButton != null ? weaponSlotButton.transform : null, "Slot Text")?.GetComponent<TMP_Text>();
                    innContent = FindChild(inn, "Inventory Content");
                    innStatusLabel = FindChild(inn, "Status")?.GetComponent<TMP_Text>();
                }

                if (goldLabel != null && shopContent != null && guildContent != null &&
                    innContent != null && innPartyTabs != null && innEquipmentTabs != null && innPortrait != null &&
                    armorSlotLabel != null && weaponSlotLabel != null)
                {
                    ConfigureFacilityPanel(shopPanel);
                    ConfigureFacilityPanel(guildPanel);
                    ConfigureInnPanel(innPanel);
                    if (FindChild(shop, "Close Button") == null) CreateCloseButton(shop);
                    if (FindChild(guild, "Close Button") == null) CreateCloseButton(guild);
                    if (FindChild(inn, "Close Button") == null) CreateCloseButton(inn);
                    if (buildingSelection == null) CreateBuildingSelection();
                    else BindExistingBuildingButtons();
                    EnsureGuildInteriorVisuals();
                    ShowTownOverview();
                    return true;
                }
                root = null;
            }
            return false;
        }

        private void BindExistingBuildingButtons()
        {
            BindExistingBuildingButton("Shop Building", shopPanel);
            BindExistingBuildingButton("Guild Building", guildPanel);
            BindExistingBuildingButton("Inn Building", innPanel);
        }

        private void BindExistingBuildingButton(string objectName, GameObject target)
        {
            var button = FindChild(buildingSelection != null ? buildingSelection.transform : null, objectName)
                ?.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => OpenFacility(target));
        }

        private Transform CreateInnSection(out TMP_Text status, out GameObject sectionPanel)
        {
            sectionPanel = new GameObject("Inn", typeof(RectTransform), typeof(Image));
            sectionPanel.transform.SetParent(root.transform, false);
            ConfigureInnPanel(sectionPanel);
            sectionPanel.GetComponent<Image>().color = new Color(.04f, .05f, .055f, .97f);
            var content = BuildInnPanel(sectionPanel, out status);
            sectionPanel.SetActive(false);
            return content;
        }

        private Transform BuildInnPanel(GameObject panel, out TMP_Text status)
        {
            ClearPanelChildren(panel.transform);
            var title = CreateText("Title", panel.transform, 36, TextAlignmentOptions.Center);
            title.text = "여관";
            title.rectTransform.anchorMin = new Vector2(.35f, .91f);
            title.rectTransform.anchorMax = new Vector2(.65f, .99f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            CreateCloseButton(panel.transform);

            var tabsObject = new GameObject("Party Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabsObject.transform.SetParent(panel.transform, false);
            innPartyTabs = tabsObject.transform;
            var tabsRect = tabsObject.GetComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(.05f, .82f); tabsRect.anchorMax = new Vector2(.46f, .9f);
            tabsRect.offsetMin = tabsRect.offsetMax = Vector2.zero;
            var tabsLayout = tabsObject.GetComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 6f; tabsLayout.childControlWidth = true; tabsLayout.childControlHeight = false;
            tabsLayout.childForceExpandWidth = true; tabsLayout.childAlignment = TextAnchor.LowerLeft;

            var equipmentTabsObject = new GameObject("Equipment Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            equipmentTabsObject.transform.SetParent(panel.transform, false);
            innEquipmentTabs = equipmentTabsObject.transform;
            var equipmentTabsRect = equipmentTabsObject.GetComponent<RectTransform>();
            equipmentTabsRect.anchorMin = new Vector2(.68f, .82f); equipmentTabsRect.anchorMax = new Vector2(.95f, .9f);
            equipmentTabsRect.offsetMin = equipmentTabsRect.offsetMax = Vector2.zero;
            var equipmentTabsLayout = equipmentTabsObject.GetComponent<HorizontalLayoutGroup>();
            equipmentTabsLayout.spacing = 6f;
            equipmentTabsLayout.childControlWidth = true;
            equipmentTabsLayout.childControlHeight = false;
            equipmentTabsLayout.childForceExpandWidth = true;
            equipmentTabsLayout.childAlignment = TextAnchor.LowerRight;

            status = CreateText("Status", panel.transform, 24, TextAlignmentOptions.Center);
            status.rectTransform.anchorMin = new Vector2(.04f, .73f);
            status.rectTransform.anchorMax = new Vector2(.29f, .81f);
            status.rectTransform.offsetMin = status.rectTransform.offsetMax = Vector2.zero;

            var portraitObject = new GameObject("Character Portrait", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            portraitObject.transform.SetParent(panel.transform, false);
            portraitObject.GetComponent<Image>().color = new Color(.12f, .13f, .14f, 1f);
            portraitObject.GetComponent<Image>().raycastTarget = false;
            var portraitRect = portraitObject.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(.04f, .12f); portraitRect.anchorMax = new Vector2(.29f, .73f);
            portraitRect.offsetMin = portraitRect.offsetMax = Vector2.zero;

            var portraitImageObject = new GameObject("Portrait Image", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            portraitImageObject.transform.SetParent(portraitObject.transform, false);
            innPortrait = portraitImageObject.GetComponent<Image>();
            innPortrait.preserveAspect = false;
            innPortrait.raycastTarget = false;
            var portraitImageRect = portraitImageObject.GetComponent<RectTransform>();
            portraitImageRect.anchorMin = portraitImageRect.anchorMax = new Vector2(.5f, .5f);
            portraitImageRect.pivot = new Vector2(.5f, .5f);
            portraitImageRect.anchoredPosition = Vector2.zero;
            portraitImageObject.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            armorSlotButton = CreateEquipmentSlotButton(panel.transform, "Armor Slot", EquipmentSlot.Armor,
                new Vector2(.32f, .48f), new Vector2(.52f, .75f), out armorSlotLabel);
            weaponSlotButton = CreateEquipmentSlotButton(panel.transform, "Weapon Slot", EquipmentSlot.Weapon,
                new Vector2(.32f, .16f), new Vector2(.52f, .43f), out weaponSlotLabel);

            var inventoryTitle = CreateText("Inventory Title", panel.transform, 23, TextAlignmentOptions.Center);
            inventoryTitle.text = "인벤토리";
            inventoryTitle.rectTransform.anchorMin = new Vector2(.56f, .75f);
            inventoryTitle.rectTransform.anchorMax = new Vector2(.96f, .81f);
            inventoryTitle.rectTransform.offsetMin = inventoryTitle.rectTransform.offsetMax = Vector2.zero;

            var scrollObject = new GameObject("Inventory Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(panel.transform, false);
            var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(.56f, .1f); scrollRectTransform.anchorMax = new Vector2(.96f, .75f);
            scrollRectTransform.offsetMin = scrollRectTransform.offsetMax = Vector2.zero;
            scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, .2f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            var content = new GameObject("Inventory Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = Vector2.one; contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10); layout.spacing = 8f;
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = contentRect;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            return content.transform;
        }

        private Button CreateEquipmentSlotButton(
            Transform parent, string objectName, EquipmentSlot slot,
            Vector2 anchorMin, Vector2 anchorMax, out TMP_Text label)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            buttonObject.GetComponent<Image>().color = new Color(.72f, .7f, .62f, .96f);
            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                TownProgressState.Unequip(selectedPartyKey, slot);
                RefreshInn();
            });
            label = CreateText("Slot Text", buttonObject.transform, 20, TextAlignmentOptions.Center);
            label.color = Color.black; label.enableWordWrapping = true;
            Stretch(label.rectTransform, 10f);
            return button;
        }

        private static void ConfigureInnPanel(GameObject panel)
        {
            if (panel == null) return;
            var rect = panel.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchorMin = new Vector2(.08f, .06f); rect.anchorMax = new Vector2(.92f, .92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void ClearPanelChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
        private Transform CreateSection(string objectName, string title, out TMP_Text status, out GameObject sectionPanel)
        {
            var panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            sectionPanel = panel;
            ConfigureFacilityPanel(panel);
            panel.GetComponent<Image>().color = new Color(.04f, .05f, .055f, .96f);

            var titleLabel = CreateText("Title", panel.transform, 38, TextAlignmentOptions.Center);
            titleLabel.text = title;
            var titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0f, .88f); titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

            status = CreateText("Status", panel.transform, 20, TextAlignmentOptions.Center);
            var statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(.05f, .81f); statusRect.anchorMax = new Vector2(.95f, .88f);
            statusRect.offsetMin = statusRect.offsetMax = Vector2.zero;

            CreateCloseButton(panel.transform);
            var scrollObject = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(panel.transform, false);
            var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(.04f, .04f); scrollRectTransform.anchorMax = new Vector2(.96f, .81f);
            scrollRectTransform.offsetMin = scrollRectTransform.offsetMax = Vector2.zero;
            scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, .12f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = Vector2.one; contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10); layout.spacing = 10f; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = contentRect; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            panel.SetActive(false);
            return content.transform;
        }

        private static void ConfigureFacilityPanel(GameObject panel)
        {
            if (panel == null) return;
            var rect = panel.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchorMin = new Vector2(.18f, .08f); rect.anchorMax = new Vector2(.82f, .9f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void CreateCloseButton(Transform parent)
        {
            var buttonObject = new GameObject("Close Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one; rect.anchorMax = Vector2.one; rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-16f, -16f); rect.sizeDelta = new Vector2(48f, 48f);
            buttonObject.GetComponent<Image>().color = new Color(.82f, .84f, .8f, .98f);
            buttonObject.GetComponent<Button>().onClick.AddListener(ShowTownOverview);
            var label = CreateText("Text", buttonObject.transform, 28, TextAlignmentOptions.Center);
            label.text = "X"; label.color = Color.black;
            Stretch(label.rectTransform);
        }
        private static Transform CreateGuildChoicesRow(Transform parent)
        {
            var rowObject = new GameObject("Stage Choices Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);
            var rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.preferredHeight = 520f;
            rowElement.minHeight = 360f;
            var rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(8, 8, 8, 8);
            rowLayout.spacing = 18f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            return rowObject.transform;
        }
        private static void CreateGuildStageButton(Transform parent, string title, string description, Action action)
        {
            var buttonObject = new GameObject("Stage Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.minWidth = 220f;
            layout.preferredWidth = 300f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(.82f, .84f, .8f, .98f);
            buttonObject.GetComponent<Button>().onClick.AddListener(() => action?.Invoke());

            var titleLabel = CreateText("Stage Name", buttonObject.transform, 27, TextAlignmentOptions.Left);
            titleLabel.text = title;
            titleLabel.color = Color.black;
            titleLabel.rectTransform.anchorMin = new Vector2(.05f, .56f);
            titleLabel.rectTransform.anchorMax = new Vector2(.95f, .9f);
            titleLabel.rectTransform.offsetMin = titleLabel.rectTransform.offsetMax = Vector2.zero;

            var descriptionLabel = CreateText("Stage Description", buttonObject.transform, 19, TextAlignmentOptions.TopLeft);
            descriptionLabel.text = string.IsNullOrWhiteSpace(description) ? "설명 없음" : description;
            descriptionLabel.color = new Color(.12f, .12f, .12f, 1f);
            descriptionLabel.enableWordWrapping = true;
            descriptionLabel.rectTransform.anchorMin = new Vector2(.05f, .12f);
            descriptionLabel.rectTransform.anchorMax = new Vector2(.95f, .56f);
            descriptionLabel.rectTransform.offsetMin = descriptionLabel.rectTransform.offsetMax = Vector2.zero;
        }

        private static void CreateListButton(Transform parent, string text, Action action, bool interactable = true, bool selected = false)
        {
            var buttonObject = new GameObject("Town Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<LayoutElement>().preferredHeight = 76f;
            var image = buttonObject.GetComponent<Image>();
            image.color = selected ? new Color(.78f, .68f, .32f, .98f) : new Color(.82f, .84f, .8f, .96f);
            var button = buttonObject.GetComponent<Button>(); button.interactable = interactable;
            if (action != null) button.onClick.AddListener(() => action());
            var label = CreateText("Text", buttonObject.transform, 19, TextAlignmentOptions.Center);
            label.text = text; label.color = Color.black; label.enableWordWrapping = true;
            Stretch(label.rectTransform, 10f);
        }

        private static void CreateMessage(Transform parent, string text)
        {
            var label = CreateText("Message", parent, 18, TextAlignmentOptions.Center);
            label.text = text;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        }

        private static TMP_Text CreateText(string objectName, Transform parent, int fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var label = textObject.AddComponent<TextMeshProUGUI>();
            try { if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset; }
            catch (NullReferenceException) { }
            label.fontSize = fontSize; label.color = Color.white; label.alignment = alignment; label.raycastTarget = false;
            return label;
        }

        private static string GetUnitDisplayName(string key)
        {
            if (key.IndexOf("Wizard", StringComparison.OrdinalIgnoreCase) >= 0) return "마법사";
            if (key.IndexOf("Archer", StringComparison.OrdinalIgnoreCase) >= 0) return "궁수";
            if (key.IndexOf("Knight", StringComparison.OrdinalIgnoreCase) >= 0) return "기사";
            return key;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                parent.GetChild(i).gameObject.SetActive(false);
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        private static Transform FindChild(Transform rootTransform, string childName)
        {
            if (rootTransform == null) return null;
            foreach (var child in rootTransform.GetComponentsInChildren<Transform>(true))
                if (child != null && child.name == childName) return child;
            return null;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding); rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}

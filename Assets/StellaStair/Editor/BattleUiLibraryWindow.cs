using StellaStair.Battle;
using StellaStair.Grid;
using StellaStair.Input;
using StellaStair.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StellaStair.Editor
{
    public sealed class BattleUiLibraryWindow : EditorWindow
    {
        private BattleUiData selectedUi;

        [MenuItem("Stella Stair/Battle UI Library")]
        private static void Open() => GetWindow<BattleUiLibraryWindow>("Battle UI Library");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Shared Battle UI", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "전투 UI를 별도 에셋으로 저장하고 모든 랜덤 스테이지에서 공통 적용합니다.",
                MessageType.Info);
            selectedUi = (BattleUiData)EditorGUILayout.ObjectField(
                "Battle UI", selectedUi, typeof(BattleUiData), false);

            if (GUILayout.Button("Save Current Battle UI As New"))
                SaveAsNew();
            using (new EditorGUI.DisabledScope(selectedUi == null))
            {
                if (GUILayout.Button("Overwrite Selected Battle UI"))
                    SaveSelected();
                if (GUILayout.Button("Apply Selected UI To Scene"))
                    ApplySelected();
                if (GUILayout.Button("Use Selected UI For All Stages"))
                    RegisterAsSharedUi();
            }
        }

        private void SaveAsNew()
        {
            if (!AssetDatabase.IsValidFolder("Assets/StellaStair/UI"))
                AssetDatabase.CreateFolder("Assets/StellaStair", "UI");
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Battle UI", "BattleUI", "asset", "UI 에셋 이름을 입력하세요.",
                "Assets/StellaStair/UI");
            if (string.IsNullOrEmpty(path))
                return;
            selectedUi = CreateInstance<BattleUiData>();
            AssetDatabase.CreateAsset(selectedUi, path);
            SaveSelected();
            Selection.activeObject = selectedUi;
        }

        private void SaveSelected()
        {
            EnsureActionUiInScene();
            EnsureLevelUpUiInScene();
            EnsureSelectedUnitInfoUiInScene();
            DialogueUiBinder.BindCurrentSceneDialogueUi();
            selectedUi.CaptureCurrentScene();
            SaveRuntimePrefab(selectedUi);
            EditorUtility.SetDirty(selectedUi);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureActionUiInScene()
        {
            TacticalInputController.EnsureDefaultActionButtons();
            ActionButtonBinder.BindCurrentSceneButtons();
        }

        private static void BindCurrentSceneLevelUpUi(LevelUpUpgradePresenter presenter)
        {
            if (presenter == null)
                return;
            GameObject overlay = null;
            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
            {
                if (rect != null && rect.name == "Level Up Upgrade Overlay")
                {
                    overlay = rect.gameObject;
                    break;
                }
            }
            if (overlay == null)
                return;

            var panelTransform = FindChildByName(overlay.transform, "Level Up Upgrade Panel");
            presenter.BindSceneUi(overlay, panelTransform != null ? panelTransform.gameObject : null);
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == childName)
                    return child;
            }
            return null;
        }
        [MenuItem("Stella Stair/Create Selected Unit Info UI In Current Scene")]
        public static void EnsureSelectedUnitInfoUiInScene()
        {
            var panel = FindSceneSelectedUnitInfoPanel();
            if (panel == null)
            {
                var canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas == null)
                {
                    var canvasObject = new GameObject("Battle UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    Undo.RegisterCreatedObjectUndo(canvasObject, "Create Battle UI Canvas");
                    canvas = canvasObject.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 40;
                    var scaler = canvasObject.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920f, 1080f);
                }
                panel = new GameObject("Selected Unit Info", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(panel, "Create Selected Unit Info UI");
                panel.transform.SetParent(canvas.transform, false);
                var panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0f, 0f);
                panelRect.anchorMax = new Vector2(0f, 0f);
                panelRect.pivot = new Vector2(0f, 0f);
                panelRect.anchoredPosition = new Vector2(28f, 28f);
                panelRect.sizeDelta = new Vector2(360f, 120f);
            panel.transform.localScale = Vector3.one * 2f;
                panel.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.065f, 0.94f);
                CreateSelectedInfoImage(panel.transform);
                CreateSelectedInfoLabel(panel.transform, "Name", 24, new Vector2(140f, -28f));
                CreateSelectedInfoLabel(panel.transform, "Level", 19, new Vector2(140f, 4f));
                CreateSelectedInfoLabel(panel.transform, "Experience", 17, new Vector2(140f, 36f));
                panel.SetActive(false);
            }

            var input = Object.FindAnyObjectByType<TacticalInputController>();
            if (input != null)
            {
                var presenter = input.GetComponent<SelectedUnitInfoPresenter>();
                if (presenter == null)
                    presenter = input.gameObject.AddComponent<SelectedUnitInfoPresenter>();
                presenter.BindSceneUi(panel);
                EditorUtility.SetDirty(presenter);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = panel;
        }

        private static GameObject FindSceneSelectedUnitInfoPanel()
        {
            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
                if (rect != null && rect.name == "Selected Unit Info")
                    return rect.gameObject;
            return null;
        }

        private static void CreateSelectedInfoImage(Transform parent)
        {
            var imageObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(imageObject, "Create Selected Unit Portrait");
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(16f, 0f);
            rect.sizeDelta = new Vector2(112f, 124f);
            imageObject.GetComponent<Image>().preserveAspect = true;
        }

        private static void CreateSelectedInfoLabel(Transform parent, string objectName, int fontSize, Vector2 position)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(labelObject, "Create Selected Unit Label");
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(-156f, 30f);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.enableWordWrapping = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static void EnsureLevelUpUiInScene()
        {
            var battle = Object.FindAnyObjectByType<DeploymentManager>();
            if (battle == null)
                return;
            var presenter = battle.GetComponent<LevelUpUpgradePresenter>();
            if (presenter == null)
                presenter = battle.gameObject.AddComponent<LevelUpUpgradePresenter>();
            presenter.Configure(battle);
            BindCurrentSceneLevelUpUi(presenter);
            presenter.EnsureUiExistsInScene();
            EditorUtility.SetDirty(presenter);
            if (presenter.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
        }

        private static bool IsDialogueUiRoot(GameObject root)
        {
            if (root == null)
                return false;
            var normalized = root.name.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
            return normalized.Contains("dialogue") || normalized.Contains("conversation") ||
                   normalized.Contains("talk");
        }
        private static void SaveRuntimePrefab(BattleUiData data)
        {
            var assetPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(assetPath))
                return;
            var prefabPath = System.IO.Path.ChangeExtension(assetPath, ".prefab");
            var container = new GameObject($"{data.name} Runtime UI");
            var copiedRoots = new System.Collections.Generic.HashSet<GameObject>();

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                var root = canvas.transform.root.gameObject;
                if (IsDialogueUiRoot(root))
                    continue;
                if (copiedRoots.Add(root))
                    Object.Instantiate(root, container.transform).name = root.name;
            }
            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
            {
                var root = eventSystem.transform.root.gameObject;
                if (copiedRoots.Add(root))
                    Object.Instantiate(root, container.transform).name = root.name;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(container, prefabPath);
            DestroyImmediate(container);
            data.SetRuntimeUiPrefab(prefab);
        }

        private void ApplySelected()
        {
            selectedUi.ApplyToCurrentScene();
            EnsureActionUiInScene();
            EnsureLevelUpUiInScene();
            EnsureSelectedUnitInfoUiInScene();
            DialogueUiBinder.BindCurrentSceneDialogueUi();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        private void RegisterAsSharedUi()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null)
                return;
            var progression = Object.FindAnyObjectByType<StageProgression>();
            if (progression == null)
                progression = board.gameObject.AddComponent<StageProgression>();
            var serialized = new SerializedObject(progression);
            serialized.FindProperty("commonBattleUi").objectReferenceValue = selectedUi;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(progression);
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
        }
    }
}

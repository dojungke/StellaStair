using StellaStair.Battle;
using StellaStair.Grid;
using StellaStair.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

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
            selectedUi.CaptureCurrentScene();
            SaveRuntimePrefab(selectedUi);
            EditorUtility.SetDirty(selectedUi);
            AssetDatabase.SaveAssets();
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

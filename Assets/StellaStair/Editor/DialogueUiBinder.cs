using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Grid;
using StellaStair.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace StellaStair.Editor
{
    public static class DialogueUiBinder
    {
        private const string DefaultBattleUiPath = "Assets/StellaStair/UI/BattleUI.asset";
        private const string DialogueDatabasePath = "Assets/StellaStair/Resources/TacticalDialogueDatabase.asset";


        public static void BindSampleSceneDialogueUi()
        {
            const string scenePath = "Assets/StellaStair/Sample/TacticalBattle.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            BindCurrentSceneDialogueUi();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Stella Stair/Rebind Dialogue UI")]
        public static void RebindCurrentSceneDialogueUi()
        {
            if (BindCurrentSceneDialogueUi())
                Debug.Log("Dialogue UI references rebound for the current scene.");
            else
                Debug.LogWarning("Dialogue UI root was not found. Name the root with Dialogue/Dialog/대화 or add TacticalDialoguePresenter to it.");
        }

        public static bool BindCurrentSceneDialogueUi()
        {
            var progression = FindOrCreateStageProgression();
            if (progression == null)
                return false;

            var stageRoot = FindStageRoot();
            var dialogueRoot = FindDialogueRoot() ?? TacticalDialoguePresenter.EnsureDefaultDialogueRootInScene();
            var presenter = FindOrCreateDialoguePresenter(dialogueRoot);

            var serialized = new SerializedObject(progression);
            SetObject(serialized, "commonBattleUi", AssetDatabase.LoadAssetAtPath<BattleUiData>(DefaultBattleUiPath));
            SetObject(serialized, "stageCanvas", stageRoot);
            if (stageRoot != null)
            {
                SetObject(serialized, "stageNameLabel", FindStageText(stageRoot.transform, false));
                SetObject(serialized, "stageDescriptionLabel", FindStageText(stageRoot.transform, true));
                SetObject(serialized, "stageStartButton", FindStageStartButton(stageRoot.transform));
            }
            SetObject(serialized, "dialogueDatabase", AssetDatabase.LoadAssetAtPath<TacticalDialogueDatabase>(DialogueDatabasePath));
            SetObject(serialized, "dialoguePresenter", presenter);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(progression);

            if (presenter != null && dialogueRoot != null)
                BindPresenter(presenter, dialogueRoot);

            EditorSceneManager.MarkSceneDirty(progression.gameObject.scene);
            return stageRoot != null || presenter != null;
        }

        private static StageProgression FindOrCreateStageProgression()
        {
            var progression = Object.FindAnyObjectByType<StageProgression>(FindObjectsInactive.Include);
            if (progression != null)
                return progression;

            var board = Object.FindAnyObjectByType<TacticalBoard>(FindObjectsInactive.Include);
            if (board != null)
                return board.gameObject.AddComponent<StageProgression>();

            var deployment = Object.FindAnyObjectByType<DeploymentManager>(FindObjectsInactive.Include);
            return deployment != null ? deployment.gameObject.AddComponent<StageProgression>() : null;
        }

        private static void BindPresenter(TacticalDialoguePresenter presenter, GameObject dialogueRoot)
        {
            var serialized = new SerializedObject(presenter);
            SetObject(serialized, "dialogueRoot", dialogueRoot);
            SetObject(serialized, "leftPortraitImage", FindPortrait(dialogueRoot.transform, true));
            SetObject(serialized, "rightPortraitImage", FindPortrait(dialogueRoot.transform, false));
            var left = serialized.FindProperty("leftPortraitImage")?.objectReferenceValue as Image;
            var right = serialized.FindProperty("rightPortraitImage")?.objectReferenceValue as Image;
            SetObject(serialized, "leftDarkOverlayImage", FindOverlay(left));
            SetObject(serialized, "rightDarkOverlayImage", FindOverlay(right));
            SetObject(serialized, "speakerNameLabel", FindDialogueText(dialogueRoot.transform, true));
            SetObject(serialized, "dialogueTextLabel", FindDialogueText(dialogueRoot.transform, false));
            SetObject(serialized, "nextButton", dialogueRoot.GetComponentInChildren<Button>(true));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }

        private static TacticalDialoguePresenter FindOrCreateDialoguePresenter(GameObject dialogueRoot)
        {
            var presenter = Object.FindAnyObjectByType<TacticalDialoguePresenter>(FindObjectsInactive.Include);
            if (presenter != null)
                return presenter;
            return dialogueRoot != null ? dialogueRoot.AddComponent<TacticalDialoguePresenter>() : null;
        }

        private static GameObject FindStageRoot()
        {
            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rect != null && IsStageRootName(rect.name))
                    return rect.gameObject;
            }
            return null;
        }

        private static GameObject FindDialogueRoot()
        {
            var existing = Object.FindAnyObjectByType<TacticalDialoguePresenter>(FindObjectsInactive.Include);
            if (existing != null)
            {
                var serialized = new SerializedObject(existing);
                var root = serialized.FindProperty("dialogueRoot")?.objectReferenceValue as GameObject;
                if (root != null)
                    return root;
                if (existing.TryGetComponent<RectTransform>(out _))
                    return existing.gameObject;
            }

            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rect != null && IsDialogueRootName(rect.name))
                    return rect.gameObject;
            }

            var left = FindNamedPortraitTransform(true);
            var right = FindNamedPortraitTransform(false);
            var common = FindCommonAncestor(left, right);
            return common != null ? common.gameObject : null;
        }

        private static Component FindStageText(Transform root, bool description)
        {
            Component fallback = null;
            foreach (var label in FindTextComponents(root))
            {
                if (label == null)
                    continue;
                fallback ??= label;
                var normalized = Normalize(label.name);
                if (description && (normalized.Contains("description") || normalized.EndsWith("desc") || label.name.Contains("설명")))
                    return label;
                if (!description && (normalized.Contains("title") || normalized.Contains("stagename") ||
                                     normalized.Contains("mapname") || label.name.Contains("이름")))
                    return label;
            }
            return description ? null : fallback;
        }

        private static Button FindStageStartButton(Transform root)
        {
            Button fallback = null;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                    continue;
                fallback ??= button;
                var normalized = Normalize(button.name);
                if (normalized.Contains("start") || normalized.Contains("begin") || button.name.Contains("시작"))
                    return button;
            }
            return fallback;
        }

        private static Image FindPortrait(Transform root, bool left)
        {
            Image fallback = null;
            Image bestByPosition = null;
            var bestScore = float.NegativeInfinity;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image == null || IsOverlayName(image.name) || HasTextDescendant(image.transform))
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

        private static Image FindOverlay(Image portrait)
        {
            if (portrait == null)
                return null;
            foreach (var image in portrait.transform.parent.GetComponentsInChildren<Image>(true))
            {
                if (image != null && image != portrait && IsOverlayName(image.name))
                    return image;
            }
            return null;
        }

        private static Component FindDialogueText(Transform root, bool speaker)
        {
            Component fallback = null;
            foreach (var text in FindTextComponents(root))
            {
                if (text == null)
                    continue;
                var normalized = Normalize(text.name);
                if (speaker)
                {
                    fallback ??= text;
                    if (normalized.Contains("name") || normalized.Contains("speaker") || text.name.Contains("이름"))
                        return text;
                    continue;
                }

                if (normalized.Contains("dialogue") || normalized.Contains("dialog") || normalized.Contains("line") ||
                    normalized.Contains("text") || text.name.Contains("대사"))
                    return text;
            }
            return fallback;
        }

        private static IEnumerable<Component> FindTextComponents(Transform root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;
                var type = component.GetType();
                if (type.FullName != null && type.FullName.StartsWith("TMPro.") && type.Name.Contains("Text"))
                    yield return component;
            }
        }

        private static bool HasTextDescendant(Transform root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;
                var type = component.GetType();
                if (type.FullName != null && type.FullName.StartsWith("TMPro.") && type.Name.Contains("Text"))
                    return true;
            }
            return false;
        }

        private static Transform FindNamedPortraitTransform(bool left)
        {
            foreach (var image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (image == null || IsOverlayName(image.name))
                    continue;
                var normalized = Normalize(image.name);
                if (left && (normalized.Contains("left") || normalized.Contains("main") || image.name.Contains("왼")))
                    return image.transform;
                if (!left && (normalized.Contains("right") || normalized.Contains("sub") || image.name.Contains("오른")))
                    return image.transform;
            }
            return null;
        }

        private static Transform FindCommonAncestor(Transform first, Transform second)
        {
            if (first == null)
                return second != null ? second.parent : null;
            if (second == null)
                return first.parent;
            var ancestors = new HashSet<Transform>();
            for (var current = first; current != null; current = current.parent)
                ancestors.Add(current);
            for (var current = second; current != null; current = current.parent)
                if (ancestors.Contains(current))
                    return current;
            return first.parent;
        }

        private static bool IsStageRootName(string value)
        {
            var normalized = Normalize(value);
            return normalized == "stagecanvas" || normalized == "stagepanel" ||
                   normalized == "stageintro" || normalized == "stageintroduction" ||
                   value.Contains("스테이지") && (value.Contains("캔버스") || value.Contains("Canvas"));
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

        private static bool IsOverlayName(string value)
        {
            var normalized = Normalize(value);
            return normalized.Contains("shadow") || normalized.Contains("dark") ||
                   normalized.Contains("dim") || normalized.Contains("overlay") || value.Contains("그림자");
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
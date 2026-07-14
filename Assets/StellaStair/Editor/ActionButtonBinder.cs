using System;
using System.Collections.Generic;
using StellaStair.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace StellaStair.Editor
{
    public static class ActionButtonBinder
    {
        [InitializeOnLoadMethod]
        private static void ScheduleBinding()
        {
            EditorApplication.delayCall -= BindCurrentSceneButtons;
            EditorApplication.delayCall += BindCurrentSceneButtons;
        }

        [MenuItem("Stella Stair/Bind Current Action Buttons")]
        public static void BindCurrentSceneButtons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var controller = UnityEngine.Object.FindAnyObjectByType<TacticalInputController>(
                FindObjectsInactive.Include);
            if (controller == null)
                return;

            if (BindController(controller))
                Debug.Log("Action buttons bound.");
        }

        public static bool BindController(TacticalInputController controller)
        {
            if (controller == null)
                return false;

            var candidates = GetActionButtonCandidates();
            if (candidates.Count < 3)
                return false;

            candidates.Sort((left, right) => right.transform.position.x.CompareTo(left.transform.position.x));

            var attackButton = FindButtonByName(candidates, "Attack Button") ?? candidates[0];
            var attackChangeButton = FindButtonByName(
                candidates, "Attack change Button", "Attack Change Button", "AttackChangeButton", "Change Button (1)", "Change Button");
            var cancelButton = FindButtonByName(
                candidates, "Cancel Button", "Move Undo Button", "Move Reset Button");
            var turnButton = FindButtonByName(candidates, "Turn Button");

            if (attackChangeButton == null && candidates.Count >= 4)
                attackChangeButton = candidates[1];
            if (cancelButton == null)
                cancelButton = candidates.Count >= 4 ? candidates[2] : candidates[1];
            if (turnButton == null)
                turnButton = candidates[^1];

            var serializedController = new SerializedObject(controller);
            var attackProperty = serializedController.FindProperty("attackButton");
            var attackChangeProperty = serializedController.FindProperty("attackChangeButton");
            var cancelProperty = serializedController.FindProperty("moveUndoButton");
            var turnProperty = serializedController.FindProperty("turnButton");
            if (attackProperty == null || attackChangeProperty == null ||
                cancelProperty == null || turnProperty == null)
                return false;

            attackProperty.objectReferenceValue = attackButton;
            attackChangeProperty.objectReferenceValue = attackChangeButton;
            cancelProperty.objectReferenceValue = cancelButton;
            turnProperty.objectReferenceValue = turnButton;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            return true;
        }

        private static List<Button> GetActionButtonCandidates()
        {
            var result = new List<Button>();
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (var button in buttons)
            {
                if (button == null || IsLevelUpUiButton(button))
                    continue;
                result.Add(button);
            }
            return result;
        }

        private static Button FindButtonByName(IReadOnlyList<Button> buttons, params string[] names)
        {
            foreach (var button in buttons)
                foreach (var buttonName in names)
                    if (button.name == buttonName)
                        return button;
            return null;
        }

        private static bool IsLevelUpUiButton(Button button)
        {
            var current = button.transform;
            while (current != null)
            {
                if (current.name == "Level Up Upgrade Overlay" || current.name == "Level Up Upgrade Panel")
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}
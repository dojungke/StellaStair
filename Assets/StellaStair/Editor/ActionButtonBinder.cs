using System;
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

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            if (buttons.Length < 3)
                return;

            Array.Sort(buttons, (left, right) =>
            {
                var leftX = left.transform.position.x;
                var rightX = right.transform.position.x;
                return rightX.CompareTo(leftX);
            });

            var serializedController = new SerializedObject(controller);
            var attackProperty = serializedController.FindProperty("attackButton");
            var cancelProperty = serializedController.FindProperty("moveUndoButton");
            var turnProperty = serializedController.FindProperty("turnButton");
            if (attackProperty == null || cancelProperty == null || turnProperty == null)
                return;

            attackProperty.objectReferenceValue = buttons[0];
            cancelProperty.objectReferenceValue = buttons[1];
            turnProperty.objectReferenceValue = buttons[^1];
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log($"Action buttons bound: attack={buttons[0].name}, cancel={buttons[1].name}, turn={buttons[^1].name}");
        }
    }
}

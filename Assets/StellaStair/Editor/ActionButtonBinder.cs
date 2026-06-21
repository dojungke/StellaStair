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
            if (buttons.Length < 2)
                return;

            Array.Sort(buttons, (left, right) =>
            {
                var leftX = ((RectTransform)left.transform).anchoredPosition.x;
                var rightX = ((RectTransform)right.transform).anchoredPosition.x;
                return rightX.CompareTo(leftX);
            });

            var serializedController = new SerializedObject(controller);
            var attackProperty = serializedController.FindProperty("attackButton");
            var cancelProperty = serializedController.FindProperty("moveUndoButton");
            if (attackProperty == null || cancelProperty == null)
                return;

            attackProperty.objectReferenceValue = buttons[0];
            cancelProperty.objectReferenceValue = buttons[1];
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log($"Action buttons bound: attack={buttons[0].name}, cancel={buttons[1].name}");
        }
    }
}

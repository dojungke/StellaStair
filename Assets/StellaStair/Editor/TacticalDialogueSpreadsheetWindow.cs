using System.IO;
using UnityEditor;
using UnityEngine;

namespace StellaStair.Editor
{
    public sealed class TacticalDialogueSpreadsheetWindow : EditorWindow
    {
        [MenuItem("Stella Stair/Dialogue")]
        public static void Open()
        {
            var window = GetWindow<TacticalDialogueSpreadsheetWindow>("Dialogue");
            window.minSize = new Vector2(380f, 190f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tactical Dialogues", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Open the Drive dialogue sheet or apply dialogue rows to the TacticalDialogueDatabase asset.",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("CSV", TacticalDialogueCsvImporter.LocalCsvPath);

                if (GUILayout.Button("Open Local CSV"))
                    OpenLocalCsv();

                if (GUILayout.Button("Open Google Drive Document"))
                    TacticalDialogueCsvImporter.OpenGoogleDriveDocument();

                if (GUILayout.Button("Download Google Drive CSV And Apply"))
                    TacticalDialogueCsvImporter.DownloadAndApply();

                if (GUILayout.Button("Apply Local CSV To Project"))
                    TacticalDialogueCsvImporter.Import();
            }
        }

        private static void OpenLocalCsv()
        {
            var fullPath = Path.GetFullPath(TacticalDialogueCsvImporter.LocalCsvPath);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Tactical Dialogues", $"CSV file was not found.\n{TacticalDialogueCsvImporter.LocalCsvPath}", "OK");
                return;
            }

            Application.OpenURL("file:///" + fullPath.Replace('\\', '/'));
        }
    }
}
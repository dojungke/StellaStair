using System.IO;
using UnityEditor;
using UnityEngine;

namespace StellaStair.Editor
{
    public sealed class UnitAttackRangeWorkbookWindow : EditorWindow
    {
        private const string WorkbookPath = "Assets/StellaStair/GameData/UnitAttackRanges.xlsx";
        private const string GoogleSheetUrl = "https://docs.google.com/spreadsheets/d/1ihP2wx33SlkhMQt6dd9YTmJlJMfmzFx4Fi27ZsNFNdE/edit?usp=drivesdk";

        [MenuItem("Stella Stair/Attack Ranges")]
        public static void Open()
        {
            var window = GetWindow<UnitAttackRangeWorkbookWindow>("Attack Ranges");
            window.minSize = new Vector2(380f, 210f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unit Attack Ranges", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Open the local workbook or apply attack range data to CSV and UnitDefinition assets.",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Workbook", WorkbookPath);

                if (GUILayout.Button("Open Local Workbook"))
                    OpenLocalWorkbook();

                if (GUILayout.Button("Open Google Drive Document"))
                    Application.OpenURL(GoogleSheetUrl);

                if (GUILayout.Button("Download Google Sheet And Apply"))
                    UnitAttackRangeWorkbookImporter.DownloadAndApplyWorkbook();

                if (GUILayout.Button("Apply Local Workbook To Project"))
                    UnitAttackRangeWorkbookImporter.ApplyWorkbook();

                if (GUILayout.Button("Apply CSV To Unit Definitions"))
                    UnitAttackRangeCsvImporter.Import();
            }
        }

        private static void OpenLocalWorkbook()
        {
            var fullPath = Path.GetFullPath(WorkbookPath);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Unit Attack Ranges", $"Workbook file was not found.\n{WorkbookPath}", "OK");
                return;
            }

            Application.OpenURL("file:///" + fullPath.Replace('\\', '/'));
        }
    }
}
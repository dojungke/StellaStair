using System;
using UnityEditor;
using UnityEngine;

namespace StellaStair.Editor
{
    public sealed class TacticalDialogueCsvAutoImporter : AssetPostprocessor
    {
        private const string CsvPath = "Assets/StellaStair/GameData/TacticalDialogues.csv";
        private static bool importing;

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importing || !Contains(importedAssets, CsvPath) && !Contains(movedAssets, CsvPath))
                return;

            try
            {
                importing = true;
                TacticalDialogueCsvImporter.Import();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to auto import tactical dialogue spreadsheet: {ex.Message}\n{ex}");
            }
            finally
            {
                importing = false;
            }
        }

        private static bool Contains(string[] paths, string target)
        {
            if (paths == null)
                return false;
            foreach (var path in paths)
                if (string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
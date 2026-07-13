using System;
using System.Linq;
using UnityEditor;

namespace StellaStair.Editor
{
    public sealed class UnitAttackRangeWorkbookAutoImporter : AssetPostprocessor
    {
        private const string WorkbookPath = "Assets/StellaStair/GameData/UnitAttackRanges.xlsx";
        private const string RangeCsvPath = "Assets/StellaStair/GameData/UnitAttackRanges.csv";
        private const string AssignmentCsvPath = "Assets/StellaStair/GameData/UnitAttackRangeAssignments.csv";
        private static bool applyingAttackRangeData;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (applyingAttackRangeData || UnitAttackRangeWorkbookImporter.SuppressWorkbookPostprocess)
                return;

            var workbookChanged = ContainsPath(importedAssets, WorkbookPath) || ContainsPath(movedAssets, WorkbookPath);
            var csvChanged = ContainsPath(importedAssets, RangeCsvPath) || ContainsPath(importedAssets, AssignmentCsvPath) ||
                             ContainsPath(movedAssets, RangeCsvPath) || ContainsPath(movedAssets, AssignmentCsvPath);
            if (!workbookChanged && !csvChanged)
                return;

            applyingAttackRangeData = true;
            try
            {
                if (workbookChanged)
                    UnitAttackRangeWorkbookImporter.ApplyWorkbook();
                else
                    UnitAttackRangeCsvImporter.Import();
            }
            finally
            {
                applyingAttackRangeData = false;
            }
        }

        private static bool ContainsPath(string[] paths, string targetPath)
        {
            return paths != null && paths.Any(path =>
                string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase));
        }
    }
}
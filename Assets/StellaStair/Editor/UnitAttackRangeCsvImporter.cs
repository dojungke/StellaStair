using System;
using System.Collections.Generic;
using System.IO;
using StellaStair.Units;
using UnityEditor;
using UnityEngine;

namespace StellaStair.Editor
{
    public static class UnitAttackRangeCsvImporter
    {
        private const string RangeCsvPath = "Assets/StellaStair/GameData/UnitAttackRanges.csv";
        private const string AssignmentCsvPath = "Assets/StellaStair/GameData/UnitAttackRangeAssignments.csv";
        private const string DefinitionRoot = "Assets/StellaStair/Resources/UnitDefinitions";
        private const int GridSize = 15;
        private const int EmptyCell = -1;
        private const int RangeCell = 0;
        private const int CenterCell = 1;
        public static void Import()
        {
            if (!File.Exists(RangeCsvPath))
            {
                Debug.LogError($"Unit attack range CSV not found: {RangeCsvPath}");
                return;
            }
            if (!File.Exists(AssignmentCsvPath))
            {
                Debug.LogError($"Unit attack range assignment CSV not found: {AssignmentCsvPath}");
                return;
            }

            var rangeDefinitions = LoadRangeDefinitions();
            var cleared = ClearAllDefinitionAttackOffsets();
            var imported = 0;
            var lines = File.ReadAllLines(AssignmentCsvPath);
            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var columns = ParseCsvLine(lines[i]);
                if (columns.Count < 3)
                {
                    Debug.LogWarning($"Invalid attack range assignment row {i + 1}: {lines[i]}");
                    continue;
                }

                var unitName = columns[0].Trim();
                var attackMode = "Default";
                var targetRangeId = NormalizeRangeId(columns[1]);
                var effectRangeId = NormalizeRangeId(columns[2]);
                if (columns.Count >= 4)
                {
                    attackMode = string.IsNullOrWhiteSpace(columns[1]) ? "Default" : columns[1].Trim();
                    targetRangeId = NormalizeRangeId(columns[2]);
                    effectRangeId = NormalizeRangeId(columns[3]);
                }
                var definition = LoadDefinition(unitName);
                if (definition == null)
                {
                    Debug.LogWarning($"UnitDefinition not found for attack range assignment row {i + 1}: {unitName}");
                    continue;
                }

                if (!rangeDefinitions.TryGetValue(RangeKey("Target", targetRangeId), out var targetMask))
                {
                    Debug.LogWarning($"Target range id not found at assignment row {i + 1}: {targetRangeId}");
                    continue;
                }
                if (!rangeDefinitions.TryGetValue(RangeKey("Effect", effectRangeId), out var effectMask))
                {
                    Debug.LogWarning($"Effect range id not found at assignment row {i + 1}: {effectRangeId}");
                    continue;
                }

                definition.ConfigureAttackOffsets(
                    attackMode,
                    targetMask.ToOffsets(includeCenterCell: false),
                    effectMask.ToOffsets(includeCenterCell: true));
                EditorUtility.SetDirty(definition);
                imported++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Imported attack range assignments for {imported} rows after clearing {cleared} unit definitions from {AssignmentCsvPath}.");
        }

        private static int ClearAllDefinitionAttackOffsets()
        {
            var cleared = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UnitDefinition", new[] { DefinitionRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
                if (definition == null)
                    continue;
                definition.ClearAttackOffsetDefinitions();
                EditorUtility.SetDirty(definition);
                cleared++;
            }
            return cleared;
        }
        private static Dictionary<string, RangeMask> LoadRangeDefinitions()
        {
            var definitions = new Dictionary<string, RangeMask>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(RangeCsvPath);
            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var columns = ParseCsvLine(lines[i]);
                if (columns.Count < GridSize + 3)
                {
                    Debug.LogWarning($"Invalid attack range grid row {i + 1}: {lines[i]}");
                    continue;
                }

                var maskType = columns[0].Trim();
                var rangeId = NormalizeRangeId(columns[1]);
                if (!int.TryParse(columns[2], out var rowNumber) || rowNumber < 1 || rowNumber > GridSize)
                {
                    Debug.LogWarning($"Invalid attack range grid row number at CSV row {i + 1}: {columns[2]}");
                    continue;
                }

                var key = RangeKey(maskType, rangeId);
                if (!definitions.TryGetValue(key, out var mask))
                {
                    mask = new RangeMask($"{maskType} {rangeId}");
                    definitions.Add(key, mask);
                }

                for (var x = 0; x < GridSize; x++)
                {
                    var value = ParseCellValue(columns[x + 3], i + 1, x + 4);
                    mask.SetCell(rowNumber - 1, x, value);
                }
            }
            return definitions;
        }

        private static string NormalizeRangeId(string rawValue)
        {
            var value = (rawValue ?? string.Empty).Trim();
            if (float.TryParse(value, out var numeric) && Mathf.Approximately(numeric, Mathf.Round(numeric)))
                return Mathf.RoundToInt(numeric).ToString();
            return value;
        }
        private static string RangeKey(string maskType, string rangeId)
        {
            return $"{maskType.Trim()}:{NormalizeRangeId(rangeId)}";
        }

        private static UnitDefinition LoadDefinition(string unitName)
        {
            var directPath = $"{DefinitionRoot}/{unitName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(directPath);
            if (definition != null)
                return definition;

            foreach (var guid in AssetDatabase.FindAssets("t:UnitDefinition", new[] { DefinitionRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
                if (candidate != null && string.Equals(
                        candidate.DisplayName, unitName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private static int ParseCellValue(string rawValue, int row, int column)
        {
            var value = rawValue.Trim();
            if (string.IsNullOrEmpty(value))
                return EmptyCell;
            if (value == "0")
                return RangeCell;
            if (value == "1")
                return CenterCell;
            if (float.TryParse(value, out var numeric))
            {
                if (Mathf.Approximately(numeric, 0f))
                    return RangeCell;
                if (Mathf.Approximately(numeric, 1f))
                    return CenterCell;
            }

            Debug.LogWarning($"Invalid cell value at CSV row {row}, column {column}: {rawValue}. Use blank, 0, or 1.");
            return EmptyCell;
        }

        private sealed class RangeMask
        {
            private readonly string name;
            private readonly int[,] cells = new int[GridSize, GridSize];
            private readonly bool[] rowsSet = new bool[GridSize];

            public RangeMask(string name)
            {
                this.name = name;
                for (var y = 0; y < GridSize; y++)
                    for (var x = 0; x < GridSize; x++)
                        cells[y, x] = EmptyCell;
            }

            public void SetCell(int row, int column, int value)
            {
                cells[row, column] = value;
                rowsSet[row] = true;
            }

            public List<GridOffset> ToOffsets(bool includeCenterCell)
            {
                var offsets = new List<GridOffset>();
                var centerX = GridSize / 2;
                var centerY = GridSize / 2;
                var foundCenter = false;

                for (var y = 0; y < GridSize; y++)
                {
                    if (!rowsSet[y])
                        Debug.LogWarning($"Missing row {y + 1} in attack range grid: {name}");

                    for (var x = 0; x < GridSize; x++)
                    {
                        if (cells[y, x] != CenterCell)
                            continue;
                        if (foundCenter)
                            Debug.LogWarning($"Multiple center cells found in attack range grid: {name}");
                        centerX = x;
                        centerY = y;
                        foundCenter = true;
                    }
                }

                if (!foundCenter)
                    Debug.LogWarning($"No center cell value 1 found in attack range grid: {name}. Using grid center.");

                for (var y = 0; y < GridSize; y++)
                {
                    for (var x = 0; x < GridSize; x++)
                    {
                        var value = cells[y, x];
                        if (value != RangeCell && !(includeCenterCell && value == CenterCell))
                            continue;
                        offsets.Add(new GridOffset(x - centerX, centerY - y));
                    }
                }
                return offsets;
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = string.Empty;
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current += '"';
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current);
                    current = string.Empty;
                }
                else
                {
                    current += c;
                }
            }

            values.Add(current);
            return values;
        }
    }
}
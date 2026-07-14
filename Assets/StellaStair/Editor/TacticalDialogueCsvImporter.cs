using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using StellaStair.Presentation;
using UnityEditor;
using UnityEngine;

namespace StellaStair.Editor
{
    public static class TacticalDialogueCsvImporter
    {
        private const string CsvPath = "Assets/StellaStair/GameData/TacticalDialogues.csv";
        private const string DatabasePath = "Assets/StellaStair/Resources/TacticalDialogueDatabase.asset";
        private const string GoogleSheetId = "1-MI5RBH0Fk7BNKsdhHuxybp247ZKNt6TlELXMZEDVM0";
        private const string GoogleDriveDocumentUrl = "https://docs.google.com/spreadsheets/d/" + GoogleSheetId + "/edit?usp=drivesdk";
        private const string StageDialoguesCsvDownloadUrl = "https://docs.google.com/spreadsheets/d/" + GoogleSheetId + "/export?format=csv&gid=2044681652";
        private const string BattleDialoguesCsvDownloadUrl = "https://docs.google.com/spreadsheets/d/" + GoogleSheetId + "/export?format=csv&gid=2044681653";

        public static string LocalCsvPath => CsvPath;
        public static string DriveDocumentUrl => GoogleDriveDocumentUrl;

        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"Tactical dialogue spreadsheet csv not found: {CsvPath}");
                return;
            }

            try
            {
                EnsureFolder("Assets/StellaStair", "Resources");
                var database = AssetDatabase.LoadAssetAtPath<TacticalDialogueDatabase>(DatabasePath);
                if (database == null)
                {
                    database = ScriptableObject.CreateInstance<TacticalDialogueDatabase>();
                    AssetDatabase.CreateAsset(database, DatabasePath);
                }

                var lines = ReadLines(CsvPath);
                database.ReplaceLines(lines);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(DatabasePath);
                Debug.Log($"Imported {lines.Count} tactical dialogue rows into {DatabasePath}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to import tactical dialogue spreadsheet: {ex.Message}\n{ex}");
            }
        }

        public static void DownloadAndApply()
        {
            try
            {
                using var client = new WebClient();
                client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");

                var stageText = DownloadDialogueCsv(client, StageDialoguesCsvDownloadUrl, "StageDialogues");
                var battleText = DownloadDialogueCsv(client, BattleDialoguesCsvDownloadUrl, "BattleDialogues");
                var text = MergeCsvTexts(stageText, battleText);
                if (!LooksLikeDialogueCsv(text))
                {
                    Debug.LogError(
                        "Downloaded tactical dialogue data was not a valid csv file. " +
                        "If the Drive file is private, Unity cannot download it without Google OAuth credentials. " +
                        "Download the StageDialogues and BattleDialogues tabs through Codex/Google Drive or your browser, then apply locally.");
                    return;
                }

                File.WriteAllText(CsvPath, text, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(CsvPath);
                Import();
                AssetDatabase.Refresh();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Debug.LogError(
                    "Google Drive download failed with 401 Unauthorized. " +
                    "Unity cannot download a private Drive file without Google OAuth credentials. " +
                    "Download the StageDialogues and BattleDialogues tabs through Codex/Google Drive or your browser, then apply locally.\n" + ex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to download tactical dialogue spreadsheet: {ex.Message}\n{ex}");
            }
        }

        public static void OpenGoogleDriveDocument()
        {
            Application.OpenURL(GoogleDriveDocumentUrl);
        }


        private static string DownloadDialogueCsv(WebClient client, string url, string sheetName)
        {
            var data = client.DownloadData(url);
            var text = DecodeUtf8(data);
            if (!LooksLikeDialogueCsv(text))
                throw new InvalidDataException($"Downloaded {sheetName} was not a tactical dialogue csv.");
            return text;
        }

        private static string MergeCsvTexts(params string[] csvTexts)
        {
            var mergedRows = new List<List<string>>();
            var hasHeader = false;

            foreach (var csvText in csvTexts)
            {
                var rows = ReadCsvText(csvText);
                if (rows.Count == 0)
                    continue;

                if (!hasHeader)
                {
                    mergedRows.Add(rows[0]);
                    hasHeader = true;
                }

                for (var i = 1; i < rows.Count; i++)
                {
                    if (!IsEmptyRow(rows[i]))
                        mergedRows.Add(rows[i]);
                }
            }

            return WriteCsv(mergedRows);
        }
        private static List<TacticalDialogueDatabase.Line> ReadLines(string path)
        {
            var result = new List<TacticalDialogueDatabase.Line>();
            var rows = ReadCsv(path);
            if (rows.Count <= 1)
                return result;

            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var usedOrders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < rows[0].Count; i++)
                header[NormalizeHeader(rows[0][i])] = i;

            for (var i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (IsEmptyRow(row))
                    continue;

                var stageKey = Get(row, header, "stagekey");
                var timing = ParseTiming(Get(row, header, "timing"));
                var order = ResolveUniqueOrder(
                    usedOrders, stageKey, timing, ParseInt(Get(row, header, "order"), result.Count + 1));

                result.Add(new TacticalDialogueDatabase.Line
                {
                    stageKey = stageKey,
                    timing = timing,
                    duration = ParseFloat(Get(row, header, "duration"), 2.2f),
                    order = order,
                    speakerName = Get(row, header, "speaker"),
                    skillKey = Get(row, header, "skillkey"),
                    text = NormalizeText(Get(row, header, "text")),
                    leftCharacterId = Get(row, header, "leftcharacter"),
                    leftPortrait = ParsePortrait(Get(row, header, "leftportrait")),
                    rightCharacterId = Get(row, header, "rightcharacter"),
                    rightPortrait = ParsePortrait(Get(row, header, "rightportrait"))
                });
            }

            return result;
        }

        private static int ResolveUniqueOrder(HashSet<string> usedOrders, string stageKey, TacticalDialogueTiming timing, int preferredOrder)
        {
            var order = Math.Max(1, preferredOrder);
            var prefix = $"{stageKey}|{timing}|";
            while (!usedOrders.Add(prefix + order))
                order++;
            return order;
        }
        private static TacticalDialogueTiming ParseTiming(string value)
        {
            var normalized = NormalizeHeader(value);
            return normalized switch
            {
                "before" or "beforebattle" or "prebattle" or "start" => TacticalDialogueTiming.BeforeBattle,
                "after" or "afterbattle" or "victory" or "aftervictory" or "clear" => TacticalDialogueTiming.AfterVictory,
                "defeat" or "afterdefeat" or "lose" or "loss" => TacticalDialogueTiming.AfterDefeat,
                "enemykilled" or "enemykill" or "kill" or "killed" => TacticalDialogueTiming.EnemyKilled,
                "levelup" or "level" or "upgrade" => TacticalDialogueTiming.LevelUp,
                "allyhealed" or "allyheal" or "heal" or "healed" => TacticalDialogueTiming.AllyHealed,
                "skillused" or "skill" or "skilluse" or "attackused" => TacticalDialogueTiming.SkillUsed,
                _ => TacticalDialogueTiming.BeforeBattle
            };
        }

        private static TacticalDialoguePortraitMode ParsePortrait(string value)
        {
            var normalized = NormalizeHeader(value);
            return normalized switch
            {
                "normal" or "default" or "base" or "\uAE30\uBCF8" => TacticalDialoguePortraitMode.Normal,
                "dark" or "shadow" or "dim" or "\uC5B4\uB450\uC6C0" => TacticalDialoguePortraitMode.Dark,
                _ => TacticalDialoguePortraitMode.Empty
            };
        }

        private static float ParseFloat(string value, float fallback)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? Mathf.Max(0.1f, result)
                : fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out var result) ? result : fallback;
        }

        private static string Get(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> header, string key)
        {
            return header.TryGetValue(key, out var index) && index >= 0 && index < row.Count
                ? row[index]?.Trim() ?? string.Empty
                : string.Empty;
        }


        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n");
        }
        private static string NormalizeHeader(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsEmptyRow(IEnumerable<string> row)
        {
            foreach (var cell in row)
                if (!string.IsNullOrWhiteSpace(cell))
                    return false;
            return true;
        }

        private static List<List<string>> ReadCsv(string path)
        {
            return ReadCsvText(File.ReadAllText(path, Encoding.UTF8));
        }

        private static List<List<string>> ReadCsvText(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var builder = new StringBuilder();
            text = (text ?? string.Empty).TrimStart('\uFEFF');
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    row.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    row.Add(builder.ToString());
                    builder.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    continue;
                }

                builder.Append(c);
            }

            if (builder.Length > 0 || row.Count > 0)
            {
                row.Add(builder.ToString());
                rows.Add(row);
            }
            return rows;
        }
        private static string WriteCsv(IEnumerable<List<string>> rows)
        {
            var builder = new StringBuilder();
            var firstRow = true;
            foreach (var row in rows)
            {
                if (!firstRow)
                    builder.AppendLine();
                firstRow = false;

                for (var i = 0; i < row.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    builder.Append(EscapeCsv(row[i]));
                }
            }
            return builder.ToString();
        }

        private static string EscapeCsv(string value)
        {
            value ??= string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
        private static string DecodeUtf8(byte[] data)
        {
            return data == null || data.Length == 0
                ? string.Empty
                : Encoding.UTF8.GetString(data).TrimStart('\uFEFF');
        }

        private static bool LooksLikeDialogueCsv(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
            var header = firstLineEnd >= 0 ? text.Substring(0, firstLineEnd) : text;
            var normalized = NormalizeHeader(header);
            return normalized.Contains("stagekey") && normalized.Contains("timing") &&
                   normalized.Contains("speaker") && normalized.Contains("text");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
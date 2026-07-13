using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace StellaStair.Editor
{
    public static class UnitAttackRangeWorkbookImporter
    {
        private const string WorkbookPath = "Assets/StellaStair/GameData/UnitAttackRanges.xlsx";
        private const string RangeCsvPath = "Assets/StellaStair/GameData/UnitAttackRanges.csv";
        private const string AssignmentCsvPath = "Assets/StellaStair/GameData/UnitAttackRangeAssignments.csv";
        private const string GoogleSheetId = "1ihP2wx33SlkhMQt6dd9YTmJlJMfmzFx4Fi27ZsNFNdE";
        private const string GoogleSheetExportUrl = "https://docs.google.com/spreadsheets/d/" + GoogleSheetId + "/export?format=xlsx";
        private const string GoogleSheetEditUrl = "https://docs.google.com/spreadsheets/d/" + GoogleSheetId + "/edit";
        private const int GridSize = 15;
        private static bool suppressWorkbookPostprocess;
        public static bool SuppressWorkbookPostprocess => suppressWorkbookPostprocess;

        public static void ApplyWorkbook()
        {
            if (!File.Exists(WorkbookPath))
            {
                Debug.LogError($"Unit attack range workbook not found: {WorkbookPath}");
                return;
            }

            try
            {
                var workbook = XlsxWorkbook.Load(WorkbookPath);
                WriteRangeCsv(workbook);
                WriteAssignmentCsv(workbook);
                AssetDatabase.ImportAsset(RangeCsvPath);
                AssetDatabase.ImportAsset(AssignmentCsvPath);
                UnitAttackRangeCsvImporter.Import();
                AssetDatabase.Refresh();
                Debug.Log($"Applied unit attack ranges from workbook: {WorkbookPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to apply unit attack ranges workbook: {ex.Message}\n{ex}");
            }
        }
        public static void DownloadAndApplyWorkbook()
        {
            try
            {
                using var client = new WebClient();
                client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");
                var data = client.DownloadData(GoogleSheetExportUrl);
                if (!LooksLikeXlsx(data))
                {
                    Debug.LogError(
                        "Downloaded Google Sheet data was not an xlsx file. " +
                        "If the sheet is private, download it through Codex/Google Drive or configure Google API authentication first.");
                    return;
                }

                File.WriteAllBytes(WorkbookPath, data);
                ImportWorkbookAssetWithoutAutoApply();
                ApplyWorkbook();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Debug.LogError(
                    "Google Sheets download failed with 401 Unauthorized. " +
                    "Unity cannot download a private Google Sheet without Google OAuth credentials. " +
                    "Download UnitAttackRanges.xlsx through Codex/Google Drive or your browser into Assets/StellaStair/GameData, " +
                    "then run Stella Stair > Download and Apply Unit Attack Ranges Workbook again after placing the workbook locally if needed.\n" + ex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to download unit attack ranges workbook: {ex.Message}\n{ex}");
            }
        }
        public static void OpenGoogleSheet()
        {
            Application.OpenURL(GoogleSheetEditUrl);
        }
        public static void AddTargetRangeBlock()
        {
            AddRangeBlock("Target");
        }
        public static void AddEffectRangeBlock()
        {
            AddRangeBlock("Effect");
        }

        private static void AddRangeBlock(string sheetName)
        {
            if (!File.Exists(WorkbookPath))
            {
                Debug.LogError($"Unit attack range workbook not found: {WorkbookPath}");
                return;
            }

            try
            {
                var workbook = XlsxWorkbook.Load(WorkbookPath);
                var sheet = workbook.GetSheet(sheetName);
                if (sheet == null)
                    throw new InvalidOperationException($"Sheet not found: {sheetName}");

                var ranges = FindRangeStarts(sheet, sheetName);
                if (ranges.Count <= 0)
                    throw new InvalidOperationException($"No existing {sheetName} range block was found.");

                var last = ranges.OrderBy(range => range.RowStart).Last();
                var nextRangeId = ranges.Max(range => range.RangeId) + 1;
                var nextRowStart = last.RowStart + 18;

                using (var archive = ZipFile.Open(WorkbookPath, ZipArchiveMode.Update))
                {
                    var sheetPath = GetWorksheetPath(archive, sheetName);
                    var entry = archive.GetEntry(sheetPath);
                    if (entry == null)
                        throw new InvalidOperationException($"Worksheet entry not found: {sheetPath}");

                    XDocument document;
                    using (var stream = entry.Open())
                        document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);

                    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var sheetData = document.Root?.Element(ns + "sheetData");
                    if (sheetData == null)
                        throw new InvalidOperationException($"Worksheet sheetData not found: {sheetName}");

                    var templateRows = sheetData.Elements(ns + "row")
                        .Where(row => TryReadRowNumber(row, out var rowNumber) &&
                                      rowNumber >= last.RowStart && rowNumber <= last.RowStart + GridSize)
                        .OrderBy(row => int.Parse(row.Attribute("r").Value))
                        .Select(row => new XElement(row))
                        .ToList();
                    if (templateRows.Count <= 0)
                        throw new InvalidOperationException($"Template rows not found for {sheetName} range {last.RangeId}.");

                    var newRows = new List<XElement>();
                    foreach (var row in templateRows)
                    {
                        var oldRowNumber = int.Parse(row.Attribute("r").Value);
                        var newRowNumber = nextRowStart + (oldRowNumber - last.RowStart);
                        ShiftRow(row, oldRowNumber, newRowNumber);
                        if (newRowNumber == nextRowStart)
                            ConfigureTitleRow(row, sheetName, nextRangeId, ns);
                        else
                            ConfigureGridRow(row, newRowNumber, nextRowStart + 8, ns);
                        newRows.Add(row);
                    }

                    foreach (var row in newRows)
                        sheetData.Add(row);
                    AddConditionalFormatting(document, nextRowStart + 1, nextRowStart + GridSize, ns);
                    UpdateDimension(document, nextRowStart + GridSize, ns);

                    entry.Delete();
                    var newEntry = archive.CreateEntry(sheetPath);
                    using (var stream = newEntry.Open())
                    using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = false }))
                        document.Save(writer);
                }

                ImportWorkbookAssetWithoutAutoApply();
                ApplyWorkbook();
                Debug.Log($"Added {sheetName} range {nextRangeId} to {WorkbookPath}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to add {sheetName} range block: {ex.Message}\n{ex}");
            }
        }

        private static List<(int RangeId, int RowStart)> FindRangeStarts(XlsxSheet sheet, string sheetName)
        {
            var result = new List<(int RangeId, int RowStart)>();
            var seen = new HashSet<int>();
            for (var row = 1; row <= sheet.MaxRow; row++)
            {
                var title = sheet.GetText(row, 1).Trim();
                if (!IsRangeTitle(title, sheetName))
                    continue;
                var rangeId = int.Parse(ParseRangeId(title, sheetName));
                if (seen.Add(rangeId))
                    result.Add((rangeId, row));
            }
            return result;
        }

        private static string GetWorksheetPath(ZipArchive archive, string sheetName)
        {
            var sheetTargets = XlsxWorkbook.LoadSheetTargets(archive);
            if (!sheetTargets.TryGetValue(sheetName, out var path))
                throw new InvalidOperationException($"Worksheet target not found: {sheetName}");
            return path;
        }

        private static bool TryReadRowNumber(XElement row, out int rowNumber)
        {
            rowNumber = 0;
            return int.TryParse(row.Attribute("r")?.Value, out rowNumber);
        }

        private static void ShiftRow(XElement row, int oldRowNumber, int newRowNumber)
        {
            row.SetAttributeValue("r", newRowNumber.ToString());
            foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                if (string.IsNullOrEmpty(reference))
                    continue;
                var column = Regex.Match(reference, "^[A-Z]+").Value;
                cell.SetAttributeValue("r", column + newRowNumber);
            }
        }

        private static void ConfigureTitleRow(XElement row, string sheetName, int rangeId, XNamespace ns)
        {
            var firstCell = row.Elements(ns + "c").FirstOrDefault();
            if (firstCell == null)
            {
                firstCell = new XElement(ns + "c", new XAttribute("r", "A" + row.Attribute("r").Value));
                row.Add(firstCell);
            }

            foreach (var cell in row.Elements(ns + "c"))
            {
                cell.Attribute("t")?.Remove();
                cell.Elements(ns + "v").Remove();
                cell.Elements(ns + "is").Remove();
            }
            firstCell.SetAttributeValue("t", "inlineStr");
            firstCell.Add(new XElement(ns + "is", new XElement(ns + "t", $"{sheetName} {rangeId}")));
        }

        private static void ConfigureGridRow(XElement row, int rowNumber, int centerRow, XNamespace ns)
        {
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var column = Regex.Match(reference, "^[A-Z]+").Value;
                cell.Attribute("t")?.Remove();
                cell.Elements(ns + "v").Remove();
                cell.Elements(ns + "is").Remove();
                if (rowNumber == centerRow && string.Equals(column, "H", StringComparison.OrdinalIgnoreCase))
                    cell.Add(new XElement(ns + "v", "1.0"));
            }
        }

        private static void AddConditionalFormatting(XDocument document, int firstGridRow, int lastGridRow, XNamespace ns)
        {
            var root = document.Root;
            if (root == null)
                return;
            var maxPriority = root.Descendants(ns + "cfRule")
                .Select(rule => int.TryParse(rule.Attribute("priority")?.Value, out var priority) ? priority : 0)
                .DefaultIfEmpty(0)
                .Max();
            var zeroRule = BuildConditionalFormatting($"A{firstGridRow}:O{lastGridRow}", 0, maxPriority + 1, ns);
            var oneRule = BuildConditionalFormatting($"A{firstGridRow}:O{lastGridRow}", 1, maxPriority + 2, ns);
            var lastFormatting = root.Elements(ns + "conditionalFormatting").LastOrDefault();
            if (lastFormatting != null)
            {
                lastFormatting.AddAfterSelf(zeroRule, oneRule);
                return;
            }

            var sheetData = root.Element(ns + "sheetData");
            if (sheetData != null)
                sheetData.AddAfterSelf(zeroRule, oneRule);
            else
                root.Add(zeroRule, oneRule);
        }

        private static XElement BuildConditionalFormatting(string range, int value, int priority, XNamespace ns)
        {
            return new XElement(ns + "conditionalFormatting",
                new XAttribute("sqref", range),
                new XElement(ns + "cfRule",
                    new XAttribute("type", "cellIs"),
                    new XAttribute("dxfId", value),
                    new XAttribute("priority", priority),
                    new XAttribute("operator", "equal"),
                    new XElement(ns + "formula", value.ToString())));
        }

        private static void UpdateDimension(XDocument document, int lastRow, XNamespace ns)
        {
            var dimension = document.Root?.Element(ns + "dimension");
            dimension?.SetAttributeValue("ref", $"A1:R{lastRow}");
        }
        private static void ImportWorkbookAssetWithoutAutoApply()
        {
            suppressWorkbookPostprocess = true;
            try
            {
                AssetDatabase.ImportAsset(WorkbookPath);
            }
            finally
            {
                suppressWorkbookPostprocess = false;
            }
        }
        private static bool LooksLikeXlsx(byte[] data)
        {
            return data != null && data.Length >= 4 &&
                   data[0] == 0x50 && data[1] == 0x4B &&
                   data[2] == 0x03 && data[3] == 0x04;
        }

        private static void WriteRangeCsv(XlsxWorkbook workbook)
        {
            var rows = new List<string[]>
            {
                BuildHeader("Range", "RangeId", "Row")
            };
            var targetCells = AppendRangeRows(workbook, rows, "Target");
            var effectCells = AppendRangeRows(workbook, rows, "Effect");
            if (targetCells <= 0)
                throw new InvalidOperationException("No Target range cells were found in the workbook. Import cancelled to avoid clearing unit attack ranges.");
            if (effectCells <= 0)
                throw new InvalidOperationException("No Effect range cells were found in the workbook. Import cancelled to avoid clearing unit attack ranges.");
            WriteCsv(RangeCsvPath, rows);
        }

        private static int AppendRangeRows(XlsxWorkbook workbook, List<string[]> rows, string sheetName)
        {
            var sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
                throw new InvalidOperationException($"Sheet not found: {sheetName}");

            var rangeCellCount = 0;
            var importedRangeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var rowStart = 1; rowStart <= sheet.MaxRow - GridSize; rowStart++)
            {
                var title = sheet.GetText(rowStart, 1).Trim();
                if (!IsRangeTitle(title, sheetName))
                    continue;

                var rangeId = ParseRangeId(title, sheetName);
                if (!importedRangeIds.Add(rangeId))
                    continue;

                for (var y = 0; y < GridSize; y++)
                {
                    var row = new string[GridSize + 3];
                    row[0] = sheetName;
                    row[1] = rangeId;
                    row[2] = (y + 1).ToString();
                    for (var x = 0; x < GridSize; x++)
                    {
                        var value = NormalizeRangeCellValue(sheet.GetText(rowStart + 1 + y, 1 + x));
                        row[x + 3] = value;
                        if (value == "0" || value == "1")
                            rangeCellCount++;
                    }
                    rows.Add(row);
                }
            }
            return rangeCellCount;
        }

        private static string NormalizeRangeCellValue(string rawValue)
        {
            var value = rawValue.Trim();
            if (value == "0" || value == "1")
                return value;
            if (float.TryParse(value, out var numeric))
            {
                if (Mathf.Approximately(numeric, 0f))
                    return "0";
                if (Mathf.Approximately(numeric, 1f))
                    return "1";
            }
            return string.Empty;
        }

        private static void WriteAssignmentCsv(XlsxWorkbook workbook)
        {
            var sheet = workbook.GetSheet("Assignments");
            if (sheet == null)
                throw new InvalidOperationException("Sheet not found: Assignments");

            var rows = new List<string[]>
            {
                new[] { "Unit", "AttackMode", "TargetRangeId", "EffectRangeId" }
            };
            for (var rowIndex = 2; rowIndex <= sheet.MaxRow; rowIndex++)
            {
                var unit = sheet.GetText(rowIndex, 1).Trim();
                if (string.IsNullOrEmpty(unit))
                    continue;

                var second = sheet.GetText(rowIndex, 2).Trim();
                var third = sheet.GetText(rowIndex, 3).Trim();
                var fourth = sheet.GetText(rowIndex, 4).Trim();
                var attackMode = "Default";
                var targetRangeId = NormalizeRangeId(second);
                var effectRangeId = NormalizeRangeId(third);
                if (!string.IsNullOrEmpty(fourth) || IsAttackModeName(second))
                {
                    attackMode = string.IsNullOrEmpty(second) ? "Default" : second;
                    targetRangeId = NormalizeRangeId(third);
                    effectRangeId = NormalizeRangeId(fourth);
                }

                rows.Add(new[]
                {
                    unit,
                    attackMode,
                    targetRangeId,
                    effectRangeId
                });
            }
            WriteCsv(AssignmentCsvPath, rows);
        }

        private static string NormalizeRangeId(string rawValue)
        {
            var value = (rawValue ?? string.Empty).Trim();
            if (float.TryParse(value, out var numeric) && Mathf.Approximately(numeric, Mathf.Round(numeric)))
                return Mathf.RoundToInt(numeric).ToString();
            return value;
        }
        private static bool IsAttackModeName(string value)
        {
            return string.Equals(value, "Default", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Thrust", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PiercingArrow", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "BowStrike", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Harpoon", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Fireball", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "IceSpike", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "NatureFragrance", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] BuildHeader(string first, string second, string third)
        {
            var header = new string[GridSize + 3];
            header[0] = first;
            header[1] = second;
            header[2] = third;
            for (var i = 0; i < GridSize; i++)
                header[i + 3] = $"C{i + 1:00}";
            return header;
        }

        private static bool IsRangeTitle(string title, string sheetName)
        {
            return !string.IsNullOrWhiteSpace(title) &&
                   title.TrimStart().StartsWith(sheetName, StringComparison.OrdinalIgnoreCase) &&
                   Regex.IsMatch(title, @"\d+");
        }

        private static string ParseRangeId(string title, string sheetName)
        {
            var match = Regex.Match(title, @"\d+");
            if (!match.Success)
                throw new InvalidOperationException($"Could not find range id in {sheetName} title: {title}");
            return match.Value;
        }

        private static void WriteCsv(string path, IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            foreach (var row in rows)
                builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static string EscapeCsv(string value)
        {
            value ??= string.Empty;
            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
                return value;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private sealed class XlsxWorkbook
        {
            private readonly Dictionary<string, XlsxSheet> sheets;

            private XlsxWorkbook(Dictionary<string, XlsxSheet> sheets)
            {
                this.sheets = sheets;
            }

            public static XlsxWorkbook Load(string path)
            {
                using var archive = ZipFile.OpenRead(path);
                var sharedStrings = LoadSharedStrings(archive);
                var sheetTargets = LoadSheetTargets(archive);
                var sheets = new Dictionary<string, XlsxSheet>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in sheetTargets)
                {
                    var entry = archive.GetEntry(pair.Value);
                    if (entry == null)
                        continue;
                    using var stream = entry.Open();
                    sheets[pair.Key] = XlsxSheet.Load(stream, sharedStrings);
                }
                return new XlsxWorkbook(sheets);
            }

            public XlsxSheet GetSheet(string name)
            {
                return sheets.TryGetValue(name, out var sheet) ? sheet : null;
            }

            private static List<string> LoadSharedStrings(ZipArchive archive)
            {
                var entry = archive.GetEntry("xl/sharedStrings.xml");
                var values = new List<string>();
                if (entry == null)
                    return values;

                using var stream = entry.Open();
                var doc = XDocument.Load(stream);
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var item in doc.Descendants(ns + "si"))
                {
                    var text = string.Concat(item.Descendants(ns + "t").Select(t => t.Value));
                    values.Add(text);
                }
                return values;
            }

            public static Dictionary<string, string> LoadSheetTargets(ZipArchive archive)
            {
                var workbookEntry = archive.GetEntry("xl/workbook.xml");
                var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
                if (workbookEntry == null || relsEntry == null)
                    throw new InvalidOperationException("Invalid xlsx workbook: workbook metadata missing.");

                XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                XNamespace packageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

                Dictionary<string, string> relTargets;
                using (var relsStream = relsEntry.Open())
                {
                    var relsDoc = XDocument.Load(relsStream);
                    relTargets = relsDoc.Descendants(packageRelNs + "Relationship")
                        .Where(e => e.Attribute("Id") != null && e.Attribute("Target") != null)
                        .ToDictionary(
                            e => e.Attribute("Id").Value,
                            e => NormalizeWorkbookTarget(e.Attribute("Target").Value));
                }

                using var workbookStream = workbookEntry.Open();
                var workbookDoc = XDocument.Load(workbookStream);
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var sheet in workbookDoc.Descendants(mainNs + "sheet"))
                {
                    var name = sheet.Attribute("name")?.Value;
                    var relId = sheet.Attribute(relNs + "id")?.Value;
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(relId) || !relTargets.TryGetValue(relId, out var target))
                        continue;
                    result[name] = target;
                }
                return result;
            }

            private static string NormalizeWorkbookTarget(string target)
            {
                target = target.Replace('\\', '/');
                if (target.StartsWith("/", StringComparison.Ordinal))
                    return target.TrimStart('/');
                if (target.StartsWith("xl/", StringComparison.Ordinal))
                    return target;
                return "xl/" + target;
            }
        }

        private sealed class XlsxSheet
        {
            private readonly Dictionary<(int Row, int Column), string> cells;

            private XlsxSheet(Dictionary<(int Row, int Column), string> cells, int maxRow)
            {
                this.cells = cells;
                MaxRow = maxRow;
            }

            public int MaxRow { get; }

            public static XlsxSheet Load(Stream stream, IReadOnlyList<string> sharedStrings)
            {
                var cells = new Dictionary<(int Row, int Column), string>();
                var maxRow = 0;
                var doc = XDocument.Load(stream);
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (var cell in doc.Descendants(ns + "c"))
                {
                    var reference = cell.Attribute("r")?.Value;
                    if (string.IsNullOrEmpty(reference) || !TryParseReference(reference, out var row, out var column))
                        continue;

                    maxRow = Math.Max(maxRow, row);
                    var value = ReadCellValue(cell, ns, sharedStrings);
                    if (!string.IsNullOrEmpty(value))
                        cells[(row, column)] = value;
                }
                return new XlsxSheet(cells, maxRow);
            }

            public string GetText(int row, int column)
            {
                return cells.TryGetValue((row, column), out var value) ? value : string.Empty;
            }

            private static string ReadCellValue(XElement cell, XNamespace ns, IReadOnlyList<string> sharedStrings)
            {
                var type = cell.Attribute("t")?.Value;
                if (type == "inlineStr")
                    return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));

                var raw = cell.Element(ns + "v")?.Value;
                if (string.IsNullOrEmpty(raw))
                    return string.Empty;

                if (type == "s" && int.TryParse(raw, out var sharedIndex) &&
                    sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                    return sharedStrings[sharedIndex];

                return raw;
            }

            private static bool TryParseReference(string reference, out int row, out int column)
            {
                row = 0;
                column = 0;
                var index = 0;
                while (index < reference.Length && char.IsLetter(reference[index]))
                {
                    column = column * 26 + char.ToUpperInvariant(reference[index]) - 'A' + 1;
                    index++;
                }
                if (column <= 0 || index >= reference.Length)
                    return false;
                return int.TryParse(reference.Substring(index), out row);
            }
        }
    }
}
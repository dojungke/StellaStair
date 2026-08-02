import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const root = "C:/Users/user/GameProject/StellaStair";
const csvPath = path.join(root, "Assets/StellaStair/GameData/UnitAttackRanges.csv");
const xlsxPath = path.join(root, "Assets/StellaStair/GameData/UnitAttackRanges.xlsx");

const csvText = await fs.readFile(csvPath, "utf8");
const lines = csvText.trim().split(/\r?\n/);
const rows = lines.slice(1).map((line) => line.split(","));
const byUnit = new Map();
for (const row of rows) {
  if (row.length < 18) continue;
  const [unit, type, rowNumberRaw, ...cells] = row;
  if (!byUnit.has(unit)) byUnit.set(unit, { Target: new Map(), Effect: new Map() });
  byUnit.get(unit)[type].set(Number(rowNumberRaw), cells.slice(0, 15).map((v) => Number(v || 0)));
}

const workbook = Workbook.create();

function a1(row, col) {
  let n = col;
  let s = "";
  while (n > 0) {
    const m = (n - 1) % 26;
    s = String.fromCharCode(65 + m) + s;
    n = Math.floor((n - 1) / 26);
  }
  return `${s}${row}`;
}

function addConditionalColors(range) {
  range.conditionalFormats.add("cellIs", {
    operator: "equal",
    formula: 1,
    format: { fill: "#E8505B", font: { color: "#E8505B" } },
  });
  range.conditionalFormats.add("cellIs", {
    operator: "equal",
    formula: 2,
    format: { fill: "#FFD966", font: { color: "#FFD966" } },
  });
  range.conditionalFormats.add("cellIs", {
    operator: "equal",
    formula: 0,
    format: { fill: "#FFFFFF", font: { color: "#FFFFFF" } },
  });
}

function writeRangeSheet(sheetName, rangeType) {
  const sheet = workbook.worksheets.add(sheetName);
  sheet.showGridLines = false;
  sheet.freezePanes.freezeRows(1);
  sheet.getRange("A1:O1").values = [[`${sheetName} ¹üÀ§ ÆíÁý`]];
  sheet.getRange("A1:O1").merge();
  sheet.getRange("A1:O1").format = {
    fill: "#D9EAF7",
    font: { bold: true, color: "#1F2937", size: 14 },
    horizontalAlignment: "center",
    verticalAlignment: "center",
  };
  sheet.getRange("Q1:AE1").values = [["0 ºóÄ­ / 1 »¡°­ / 2 ³ë¶û Áß½ÉÁ¡"]];
  sheet.getRange("Q1:AE1").merge();
  sheet.getRange("Q1:AE1").format = {
    fill: "#F3F6F8",
    font: { italic: true, color: "#666666" },
    horizontalAlignment: "center",
  };

  let rowStart = 3;
  for (const [unit, masks] of byUnit.entries()) {
    const titleRange = sheet.getRange(`${a1(rowStart, 1)}:${a1(rowStart, 15)}`);
    titleRange.values = [[unit]];
    titleRange.merge();
    titleRange.format = {
      fill: "#EEF2F7",
      font: { bold: true, color: "#111827" },
      horizontalAlignment: "center",
    };

    const matrix = [];
    for (let y = 1; y <= 15; y++) {
      matrix.push(masks[rangeType].get(y) ?? Array(15).fill(0));
    }
    const top = rowStart + 1;
    const bottom = rowStart + 15;
    const grid = sheet.getRange(`${a1(top, 1)}:${a1(bottom, 15)}`);
    grid.values = matrix;
    grid.format = {
      fill: "#FFFFFF",
      font: { color: "#FFFFFF" },
      horizontalAlignment: "center",
      verticalAlignment: "center",
      numberFormat: ";;;",
      borders: { preset: "outside", style: "medium", color: "#9CA3AF" },
    };
    addConditionalColors(grid);
    rowStart += 18;
  }

  const usedRows = Math.max(1, rowStart - 1);
  sheet.getRange(`A1:O${usedRows}`).format.columnWidthPx = 22;
  sheet.getRange(`A1:O${usedRows}`).format.rowHeightPx = 22;
  sheet.getRange("A1:O1").format.rowHeightPx = 28;
}

writeRangeSheet("Target", "Target");
writeRangeSheet("Effect", "Effect");

const raw = workbook.worksheets.add("RawCSV");
const rawValues = lines.map((line) => line.split(","));
raw.getRangeByIndexes(0, 0, rawValues.length, 18).values = rawValues;
raw.getRangeByIndexes(0, 0, rawValues.length, 18).format = {
  horizontalAlignment: "center",
  borders: { preset: "all", style: "thin", color: "#E5E7EB" },
};
raw.getRange("A1:R1").format = { fill: "#D9EAF7", font: { bold: true } };
raw.freezePanes.freezeRows(1);

const targetPreview = await workbook.render({ sheetName: "Target", range: "A1:O40", scale: 1, format: "png" });
await fs.writeFile(path.join(root, "Assets/StellaStair/GameData/UnitAttackRanges_TargetPreview.png"), new Uint8Array(await targetPreview.arrayBuffer()));
const effectPreview = await workbook.render({ sheetName: "Effect", range: "A1:O40", scale: 1, format: "png" });
await fs.writeFile(path.join(root, "Assets/StellaStair/GameData/UnitAttackRanges_EffectPreview.png"), new Uint8Array(await effectPreview.arrayBuffer()));

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(xlsxPath);

const inspect = await workbook.inspect({ kind: "sheet", include: "id,name", maxChars: 2000 });
console.log(inspect.ndjson);
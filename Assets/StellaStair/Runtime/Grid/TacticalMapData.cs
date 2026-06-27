using System;
using System.Collections.Generic;
using StellaStair.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StellaStair.Grid
{
    [CreateAssetMenu(menuName = "Stella Stair/Tactical Map Data", fileName = "TacticalMap")]
    public sealed class TacticalMapData : ScriptableObject
    {
        [Serializable]
        public sealed class Cell
        {
            public Vector3Int position;
            public TileBase tile;
            public Color color = Color.white;
        }

        [Serializable]
        public sealed class UiElement
        {
            public string hierarchyPath;
            public bool active;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector2 pivot;
            public Vector3 localScale = Vector3.one;
            public float localRotationZ;
            public bool hasTmpText;
            public string tmpText;
        }

        public List<Cell> walkable = new();
        public List<Cell> playerDeployment = new();
        public List<Cell> ladders = new();
        public List<Cell> wood = new();
        public List<Cell> crates = new();
        public List<Cell> bombCrates = new();
        public List<Cell> objectiveTargets = new();
        public List<Cell> defenseObjectives = new();
        public List<Cell> enemyGuardSpawns = new();
        public List<Cell> enemySoldierSpawns = new();
        public List<UiElement> uiElements = new();
        public TacticalStageType stageType = TacticalStageType.Elimination;
        [Min(1)] public int defenseTurnsToSurvive = 5;

        public void ApplyTo(TacticalBoard board)
        {
            if (board == null)
                return;
            Restore(board.WalkableTilemap, walkable);
            Restore(board.PlayerDeploymentTilemap, playerDeployment);
            Restore(board.LadderTilemap, ladders);
            Restore(board.WoodTilemap, wood);
            Restore(board.CrateTilemap, crates);
            Restore(board.BombCrateTilemap, bombCrates);
            var objectiveLayer = board.ObjectiveTilemap;
            if (objectiveLayer == null && objectiveTargets.Count > 0)
                objectiveLayer = CreateLayer(board, "Attack Objectives", 20);
            Restore(objectiveLayer, objectiveTargets);
            var defenseObjectiveLayer = board.DefenseObjectiveTilemap;
            if (defenseObjectiveLayer == null && defenseObjectives.Count > 0)
                defenseObjectiveLayer = CreateLayer(board, "Defense Objectives", 20);
            Restore(defenseObjectiveLayer, defenseObjectives);
            Restore(FindLayer(board, "Enemy Guard Spawns"), enemyGuardSpawns);
            Restore(FindLayer(board, "Enemy Soldier Spawns"), enemySoldierSpawns);
        }

        public void ApplyUi()
        {
            var elements = UnityEngine.Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include);
            var byPath = new Dictionary<string, RectTransform>();
            foreach (var element in elements)
            {
                if (element == null || element.GetComponentInParent<Canvas>(true) == null)
                    continue;
                byPath[GetHierarchyPath(element)] = element;
            }

            foreach (var saved in uiElements)
            {
                if (saved == null || string.IsNullOrEmpty(saved.hierarchyPath) ||
                    !byPath.TryGetValue(saved.hierarchyPath, out var element))
                    continue;
                element.anchorMin = saved.anchorMin;
                element.anchorMax = saved.anchorMax;
                element.anchoredPosition = saved.anchoredPosition;
                element.sizeDelta = saved.sizeDelta;
                element.pivot = saved.pivot;
                element.localScale = saved.localScale;
                element.localRotation = Quaternion.Euler(0f, 0f, saved.localRotationZ);
                if (saved.hasTmpText && element.TryGetComponent<TMP_Text>(out var label))
                    label.text = saved.tmpText;
                element.gameObject.SetActive(saved.active);
            }
        }

        public static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }
            return path;
        }

        private static Tilemap FindLayer(TacticalBoard board, string layerName)
        {
            var child = board.Grid != null ? board.Grid.transform.Find(layerName) : null;
            return child != null ? child.GetComponent<Tilemap>() : null;
        }

        private static Tilemap CreateLayer(TacticalBoard board, string layerName, int sortingOrder)
        {
            if (board == null || board.Grid == null)
                return null;
            var child = board.Grid.transform.Find(layerName);
            if (child != null && child.TryGetComponent<Tilemap>(out var existing))
                return existing;

            var gameObject = new GameObject(layerName, typeof(Tilemap), typeof(TilemapRenderer));
            gameObject.transform.SetParent(board.Grid.transform, false);
            gameObject.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            var tilemap = gameObject.GetComponent<Tilemap>();
            if (layerName == "Attack Objectives")
                board.ConfigureObjectives(tilemap, board.ObjectiveMaxHealth);
            if (layerName == "Defense Objectives")
                board.ConfigureDefenseObjectives(tilemap, board.DefenseObjectiveMaxHealth);
            return tilemap;
        }

        private static void Restore(Tilemap tilemap, List<Cell> source)
        {
            if (tilemap == null)
                return;
            tilemap.ClearAllTiles();
            foreach (var cell in source)
            {
                if (cell == null || cell.tile == null)
                    continue;
                tilemap.SetTile(cell.position, cell.tile);
                tilemap.SetTileFlags(cell.position, TileFlags.None);
                tilemap.SetColor(cell.position, cell.color);
            }
        }
    }
}

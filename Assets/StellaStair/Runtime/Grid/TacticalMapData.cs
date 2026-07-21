using System;
using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Units;
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
        public sealed class ObjectiveLayer
        {
            public TacticalObjectiveData data;
            public Color color = Color.white;
            public List<Cell> spawns = new();
        }

        [Serializable]
        public sealed class EnemyUnitLayer
        {
            [Tooltip("Enemy UnitDefinition used by this spawn layer.")]
            public UnitDefinition definition;
            [Tooltip("Optional tilemap name. When empty, it is generated from the definition name.")]
            public string layerName = string.Empty;
            public Color color = new(0.9f, 0.18f, 0.12f, 0.75f);
            public List<Cell> spawns = new();
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

        [Serializable]
        public sealed class Round
        {
            public string roundName = "New Round";
            public RoundStartCondition startCondition = RoundStartCondition.EnemiesDefeated;
            [Min(1)] public int startTurn = 2;
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
        [Header("Enemy Unit Layers")]
        [Tooltip("Add an entry for each enemy UnitDefinition that can be placed on this map.")]
        public List<EnemyUnitLayer> enemyUnitLayers = new();
        [Header("Objective Layers")]
        public List<ObjectiveLayer> objectiveLayers = new();
        public List<ObjectiveLayer> defenseObjectiveLayers = new();
        public string mapName = string.Empty;
        [TextArea(2, 5)] public string mapDescription = string.Empty;
        // Legacy fields retained so older map assets continue to deserialize; use TacticalObjectiveData for new maps.
        [HideInInspector] public string objectiveName = string.Empty;
        [HideInInspector] public string objectiveDescription = string.Empty;
        public Sprite backgroundSprite;
        public Color backgroundTint = Color.white;
        public List<UiElement> uiElements = new();
        public List<Round> rounds = new();
        public TacticalStageType stageType = TacticalStageType.Elimination;
        [HideInInspector, Min(1)] public int attackObjectiveMaxHealth = 8;
        [HideInInspector, Min(1)] public int defenseObjectiveMaxHealth = 12;
        [HideInInspector] public Sprite attackObjectiveSprite;
        [HideInInspector] public Sprite defenseObjectiveSprite;
        [Header("Objective Assets")]
        public TacticalObjectiveData attackObjective;
        public TacticalObjectiveData defenseObjective;
        [Header("Object Assets")]
        [HideInInspector] public TacticalObjectData woodObject;
        [HideInInspector] public TacticalObjectData crateObject;
        [HideInInspector] public TacticalObjectData bombCrateObject;
        [Min(1)] public int defenseTurnsToSurvive = 5;
        [Min(1)] public int escortTurnsToSurvive = 5;
        public string escortUnitProgressKey = string.Empty;
        private static readonly List<Cell> EmptyCells = new();

        public string DisplayName => string.IsNullOrWhiteSpace(mapName) ? name : mapName.Trim();
        public Color BackgroundTint => backgroundTint.a > 0f ? backgroundTint : Color.white;
        public int AttackObjectiveMaxHealth => attackObjectiveMaxHealth > 0 ? attackObjectiveMaxHealth : 8;
        public int DefenseObjectiveMaxHealth => defenseObjectiveMaxHealth > 0 ? defenseObjectiveMaxHealth : 12;

        private void OnValidate()
        {
            attackObjectiveMaxHealth = AttackObjectiveMaxHealth;
            defenseObjectiveMaxHealth = DefenseObjectiveMaxHealth;
        }

        public void ApplyTo(TacticalBoard board)
        {
            if (board == null)
                return;
            board.ConfigureObjectiveSettings(
                attackObjective != null ? attackObjective.MaxHealth : AttackObjectiveMaxHealth,
                defenseObjective != null ? defenseObjective.MaxHealth : DefenseObjectiveMaxHealth,
                attackObjective != null ? attackObjective.Sprite : attackObjectiveSprite,
                defenseObjective != null ? defenseObjective.Sprite : defenseObjectiveSprite,
                attackObjective, defenseObjective,
                woodObject, crateObject, bombCrateObject);
            Restore(board.WalkableTilemap, walkable);
            Restore(board.PlayerDeploymentTilemap, playerDeployment);
            Restore(board.LadderTilemap, ladders);
            Restore(board.WoodTilemap, wood);
            ApplySpriteOverride(board.WoodTilemap, woodObject != null ? woodObject.Sprite : null);
            var crateLayer = board.CrateTilemap;
            if (crateLayer == null && crates.Count > 0)
                crateLayer = CreateLayer(board, "Crates", 18);
            Restore(crateLayer, crates);
            var bombCrateLayer = board.BombCrateTilemap;
            if (bombCrateLayer == null && bombCrates.Count > 0)
                bombCrateLayer = CreateLayer(board, "Bomb Crates", 19);
            Restore(bombCrateLayer, bombCrates);
            var objectiveLayer = board.ObjectiveTilemap;
            if (objectiveLayer == null && objectiveTargets.Count > 0)
                objectiveLayer = CreateLayer(board, "Attack Objectives", 20);
            Restore(objectiveLayer, objectiveTargets);
            var defenseObjectiveLayer = board.DefenseObjectiveTilemap;
            if (stageType == TacticalStageType.Defense)
            {
                if (defenseObjectiveLayer == null && defenseObjectives.Count > 0)
                    defenseObjectiveLayer = CreateLayer(board, "Defense Objectives", 20);
                Restore(defenseObjectiveLayer, defenseObjectives);
            }
            else
            {
                Restore(defenseObjectiveLayer, EmptyCells);
            }
            var enemyGuardLayer = FindLayer(board, "Enemy Guard Spawns");
            if (enemyGuardLayer == null && enemyGuardSpawns.Count > 0)
                enemyGuardLayer = CreateLayer(board, "Enemy Guard Spawns", 21);
            Restore(enemyGuardLayer, enemyGuardSpawns);
            var enemySoldierLayer = FindLayer(board, "Enemy Soldier Spawns");
            if (enemySoldierLayer == null && enemySoldierSpawns.Count > 0)
                enemySoldierLayer = CreateLayer(board, "Enemy Soldier Spawns", 21);
            Restore(enemySoldierLayer, enemySoldierSpawns);
            ApplyEnemyUnitLayers(board);
            RemoveGeneratedObjectiveLayers(board);
            ApplyObjectiveLayers(board, objectiveLayers, false);
            ApplyObjectiveLayers(board, defenseObjectiveLayers, true);
        }

        public void RemoveGeneratedObjectiveLayers(TacticalBoard board)
        {
            if (board == null || board.Grid == null)
                return;
            var children = new List<Transform>();
            foreach (Transform child in board.Grid.transform)
            {
                if (child == null || child.name == "Attack Objectives" || child.name == "Defense Objectives")
                    continue;
                if (child.name.EndsWith(" Objectives", StringComparison.Ordinal))
                    children.Add(child);
            }
            foreach (var child in children)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private void ApplyObjectiveLayers(TacticalBoard board, List<ObjectiveLayer> layers, bool defense)
        {
            if (board == null || layers == null)
                return;
            foreach (var layer in layers)
            {
                if (layer == null || layer.data == null)
                    continue;
                var layerName = GetObjectiveLayerName(layer, defense);
                var tilemap = FindLayer(board, layerName);
                if (tilemap == null)
                    tilemap = CreateLayer(board, layerName, 20);
                Restore(tilemap, layer.spawns);
                if (Application.isPlaying)
                    board.ConfigureObjectiveLayer(tilemap, layer.data, defense);
            }
        }

        private static string GetObjectiveLayerName(ObjectiveLayer layer, bool defense)
        {
            var name = layer != null && layer.data != null ? layer.data.name : "Objective";
            return $"{name}{(defense ? " Defense" : string.Empty)} Objectives";
        }

        private void ApplyEnemyUnitLayers(TacticalBoard board)
        {
            if (board == null || enemyUnitLayers == null)
                return;

            foreach (var layer in enemyUnitLayers)
            {
                if (layer == null || layer.definition == null)
                    continue;
                var layerName = GetEnemyLayerName(layer);
                var tilemap = FindLayer(board, layerName);
                if (tilemap == null)
                    tilemap = CreateEnemyLayer(board, layerName, layer.definition, layer.color);
                Restore(tilemap, layer.spawns);
                var marker = tilemap.GetComponent<EnemySpawnTilemap>();
                if (marker == null)
                    marker = tilemap.gameObject.AddComponent<EnemySpawnTilemap>();
                marker.Configure(layer.definition, layer.color);
            }
        }

        private static string GetEnemyLayerName(EnemyUnitLayer layer)
        {
            var definitionName = layer != null && layer.definition != null
                ? layer.definition.name
                : "Enemy";
            return $"{definitionName} Spawns";
        }
        private static Tilemap CreateEnemyLayer(
            TacticalBoard board, string layerName, UnitDefinition definition, Color color)
        {
            var tilemap = CreateLayer(board, layerName, 21);
            if (tilemap == null)
                return null;
            var marker = tilemap.GetComponent<EnemySpawnTilemap>();
            if (marker == null)
                marker = tilemap.gameObject.AddComponent<EnemySpawnTilemap>();
            marker.Configure(definition, color);
            return tilemap;
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

        private static void ApplySpriteOverride(Tilemap tilemap, Sprite sprite)
        {
            if (tilemap == null || sprite == null)
                return;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(position))
                    continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = tilemap.GetColor(position);
                tilemap.SetTile(position, tile);
            }
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
            if (layerName == "Crates")
                board.ConfigureCrates(tilemap);
            if (layerName == "Bomb Crates")
                board.ConfigureBombCrates(tilemap);
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

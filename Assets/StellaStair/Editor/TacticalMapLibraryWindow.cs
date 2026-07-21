using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Grid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StellaStair.Editor
{
    // Map data intentionally excludes shared battle UI.
    public sealed class TacticalMapLibraryWindow : EditorWindow
    {
        private TacticalMapData selectedMap;
        private int selectedRoundIndex;

        [MenuItem("Stella Stair/Map Library")]
        private static void Open() =>
            GetWindow<TacticalMapLibraryWindow>("Stella Map Library");

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tactical Map Library", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "\uB9F5\uB370\uC774\uD130\uB97C \uC120\uD0DD\uD558\uACE0 \uD604\uC7AC \uC2A4\uD14C\uC774\uC9C0\uC5D0 \uBD88\uB7EC\uC624\uAC70\uB098, \uC2DC\uC98C\uC758 \uBCC0\uACBD \uB0B4\uC6A9\uC744 \uC120\uD0DD\uD55C \uB9F5\uC5D0 \uB36E\uC5B4\uC4F8 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                MessageType.Info);
            selectedMap = (TacticalMapData)EditorGUILayout.ObjectField(
                "Selected Map", selectedMap, typeof(TacticalMapData), false);
            if (selectedMap != null)
            {
                EditorGUI.BeginChangeCheck();
                var mapName = EditorGUILayout.TextField("Map Name", selectedMap.DisplayName);
                EditorGUILayout.LabelField("Map Description");
                var mapDescription = EditorGUILayout.TextArea(
                    selectedMap.mapDescription ?? string.Empty, GUILayout.MinHeight(48f));
                var backgroundSprite = (Sprite)EditorGUILayout.ObjectField(
                    "Background Image", selectedMap.backgroundSprite, typeof(Sprite), false);
                var backgroundTint = EditorGUILayout.ColorField(
                    "Background Tint", selectedMap.BackgroundTint);
                var stageType = (TacticalStageType)EditorGUILayout.EnumPopup(
                    "Stage Type", selectedMap.stageType);
                var defenseTurns = selectedMap.defenseTurnsToSurvive;
                var escortTurns = selectedMap.escortTurnsToSurvive;
                var escortKey = selectedMap.escortUnitProgressKey;
                if (stageType == TacticalStageType.Defense)
                {
                    defenseTurns = EditorGUILayout.IntField(
                        "Defense Turns", defenseTurns);
                }

                if (stageType == TacticalStageType.Escort)
                {
                    escortTurns = EditorGUILayout.IntField(
                        "Escort Turns", escortTurns);
                    escortKey = EditorGUILayout.TextField(
                        "Escort Unit Key", escortKey);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedMap, "Change Map Settings");
                    selectedMap.mapName = mapName?.Trim() ?? string.Empty;
                    selectedMap.mapDescription = mapDescription ?? string.Empty;
                    selectedMap.backgroundSprite = backgroundSprite;
                    selectedMap.backgroundTint = backgroundTint;
                    selectedMap.stageType = stageType;



                    selectedMap.defenseTurnsToSurvive = Mathf.Max(1, defenseTurns);
                    selectedMap.escortTurnsToSurvive = Mathf.Max(1, escortTurns);
                    selectedMap.escortUnitProgressKey = escortKey ?? string.Empty;
                    SaveSelectedMapAsset();
                }

                DrawEnemyUnitLayers();
                DrawObjectiveLayers();
                DrawRoundControls();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Current Map As New"))
                SaveAsNew();
            using (new EditorGUI.DisabledScope(selectedMap == null))
            {
                if (GUILayout.Button("Overwrite Selected Map") &&
                    EditorUtility.DisplayDialog(
                        "Map Override Confirmation",
                        "The current scene map data will overwrite the selected map asset. Continue?",
                        "Overwrite", "Cancel"))
                    SaveTo(selectedMap);
                if (GUILayout.Button("Load Selected Map Into Scene"))
                    LoadSelected();
                if (GUILayout.Button("Register Selected Map As Stage"))
                    RegisterSelectedStage();
            }
        }

        private void DrawEnemyUnitLayers()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Enemy Unit Layers", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Enemy types configured for this map. Each type is loaded into its own spawn tilemap.",
                EditorStyles.miniLabel);

            if (selectedMap.enemyUnitLayers == null)
                selectedMap.enemyUnitLayers = new List<TacticalMapData.EnemyUnitLayer>();

            var removeIndex = -1;
            for (var i = 0; i < selectedMap.enemyUnitLayers.Count; i++)
            {
                var layer = selectedMap.enemyUnitLayers[i];
                if (layer == null)
                {
                    layer = new TacticalMapData.EnemyUnitLayer();
                    selectedMap.enemyUnitLayers[i] = layer;
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUI.BeginChangeCheck();
                    layer.definition = (StellaStair.Units.UnitDefinition)EditorGUILayout.ObjectField(
                        layer.definition, typeof(StellaStair.Units.UnitDefinition), false,
                        GUILayout.MinWidth(140f));


                    layer.color = EditorGUILayout.ColorField(layer.color, GUILayout.Width(72f));
                    EditorGUILayout.LabelField(
                        $"{(layer.spawns != null ? layer.spawns.Count : 0)} cells",
                        EditorStyles.miniLabel, GUILayout.Width(55f));
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                        removeIndex = i;
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(selectedMap, "Edit Enemy Unit Layers");
                        EditorUtility.SetDirty(selectedMap);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            if (removeIndex >= 0)
            {
                var removedLayer = selectedMap.enemyUnitLayers[removeIndex];
                Undo.RecordObject(selectedMap, "Remove Enemy Unit Layer");
                if (removedLayer != null && removedLayer.definition != null)
                {
                    var board = Object.FindAnyObjectByType<TacticalBoard>();
                    var tilemap = FindLayer(board, GetEnemyLayerName(removedLayer));
                    if (tilemap != null)
                        Undo.DestroyObjectImmediate(tilemap.gameObject);
                }
                selectedMap.enemyUnitLayers.RemoveAt(removeIndex);
                SaveSelectedMapAsset();
                return;
            }

            if (GUILayout.Button("+ Add Enemy Unit Type"))
            {
                Undo.RecordObject(selectedMap, "Add Enemy Unit Layer");
                selectedMap.enemyUnitLayers.Add(new TacticalMapData.EnemyUnitLayer());
                SaveSelectedMapAsset();
            }
        }

        private void DrawObjectiveLayers()
        {
            if (selectedMap == null)
                return;
            var isDefense = selectedMap.stageType == TacticalStageType.Defense;
            var layers = isDefense ? selectedMap.defenseObjectiveLayers : selectedMap.objectiveLayers;
            if (layers == null)
            {
                layers = new List<TacticalMapData.ObjectiveLayer>();
                if (isDefense)
                    selectedMap.defenseObjectiveLayers = layers;
                else
                    selectedMap.objectiveLayers = layers;
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(isDefense ? "Defense Objective Types" : "Objective Types", EditorStyles.boldLabel);
            var removeIndex = -1;
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i] ?? (layers[i] = new TacticalMapData.ObjectiveLayer());
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    layer.data = (TacticalObjectiveData)EditorGUILayout.ObjectField(layer.data, typeof(TacticalObjectiveData), false, GUILayout.MinWidth(160f));
                    if (layer.data == null && GUILayout.Button("Create", GUILayout.Width(56f)))
                        layer.data = CreateObjectiveDataAsset(isDefense ? "DefenseObjectiveData" : "ObjectiveData");
                    layer.color = EditorGUILayout.ColorField(layer.color, GUILayout.Width(90f));
                    if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                        removeIndex = i;
                }
            }
            if (removeIndex >= 0)
            {
                var removedLayer = layers[removeIndex];
                Undo.RecordObject(selectedMap, "Remove Objective Layer");
                if (removedLayer != null && removedLayer.data != null)
                {
                    var board = Object.FindAnyObjectByType<TacticalBoard>();
                    var tilemap = FindLayer(board, GetObjectiveLayerName(removedLayer, isDefense));
                    if (tilemap != null)
                        Undo.DestroyObjectImmediate(tilemap.gameObject);
                }
                layers.RemoveAt(removeIndex);
                SaveSelectedMapAsset();
                return;
            }
            if (GUILayout.Button(isDefense ? "+ Add Defense Objective Type" : "+ Add Objective Type"))
            {
                Undo.RecordObject(selectedMap, "Add Objective Layer");
                layers.Add(new TacticalMapData.ObjectiveLayer());
                SaveSelectedMapAsset();
            }
        }

        private void DrawRoundControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rounds", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Initial Map + {selectedMap.rounds.Count} extra round(s)",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add", GUILayout.Width(72)))
                    AddRound(selectedMap.rounds.Count);
                using (new EditorGUI.DisabledScope(selectedMap.rounds.Count == 0))
                {
                    if (GUILayout.Button("- Remove", GUILayout.Width(88)))
                        RemoveSelectedRound();
                    if (GUILayout.Button("Duplicate", GUILayout.Width(88)))
                        DuplicateSelectedRound();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var desiredCount = EditorGUILayout.IntField("Extra Round Count", selectedMap.rounds.Count);
                if (EditorGUI.EndChangeCheck())
                    SetRoundCount(Mathf.Max(0, desiredCount));

                using (new EditorGUI.DisabledScope(selectedMap.rounds.Count == 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(44)))
                        MoveSelectedRound(-1);
                    if (GUILayout.Button("Down", GUILayout.Width(56)))
                        MoveSelectedRound(1);
                }
            }

            if (selectedMap.rounds.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "\uB9F5\uB370\uC774\uD130\uB97C \uC120\uD0DD\uD558\uACE0 \uD604\uC7AC \uC2A4\uD14C\uC774\uC9C0\uC5D0 \uBD88\uB7EC\uC624\uAC70\uB098, \uC2DC\uC98C\uC758 \uBCC0\uACBD \uB0B4\uC6A9\uC744 \uC120\uD0DD\uD55C \uB9F5\uC5D0 \uB36E\uC5B4\uC4F8 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    MessageType.Info);
                return;
            }

            selectedRoundIndex = ClampRoundIndex(selectedRoundIndex);
            var names = new string[selectedMap.rounds.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = GetRoundDisplayName(i, selectedMap.rounds[i]);
            selectedRoundIndex = EditorGUILayout.Popup("Selected Round", selectedRoundIndex, names);
            var round = selectedMap.rounds[selectedRoundIndex];

            EditorGUI.BeginChangeCheck();
            var roundName = EditorGUILayout.TextField("Round Name", round.roundName);
            var condition = (RoundStartCondition)EditorGUILayout.EnumPopup(
                "Start Condition", round.startCondition);
            var startTurn = EditorGUILayout.IntField("Start Turn", round.startTurn);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedMap, "Edit Tactical Round");
                round.roundName = roundName;
                round.startCondition = condition;
                round.startTurn = Mathf.Max(1, startTurn);
                SaveSelectedMapAsset();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Current Map To Round"))
                    SaveCurrentMapToRound(round);
                if (GUILayout.Button("Load Round Into Scene"))
                    LoadRoundIntoScene(round);
            }
        }

        private void AddRound(int insertIndex)
        {
            Undo.RecordObject(selectedMap, "Add Tactical Round");
            insertIndex = Mathf.Clamp(insertIndex, 0, selectedMap.rounds.Count);
            selectedMap.rounds.Insert(insertIndex, CreateDefaultRound(insertIndex));
            selectedRoundIndex = insertIndex;
            SaveSelectedMapAsset();
        }

        private void RemoveSelectedRound()
        {
            if (selectedMap.rounds.Count == 0)
                return;
            selectedRoundIndex = ClampRoundIndex(selectedRoundIndex);
            var round = selectedMap.rounds[selectedRoundIndex];
            if (!EditorUtility.DisplayDialog(
                    "Remove Round",
                    $"Remove '{GetRoundDisplayName(selectedRoundIndex, round)}'?",
                    "Remove", "Cancel"))
                return;

            Undo.RecordObject(selectedMap, "Remove Tactical Round");
            selectedMap.rounds.RemoveAt(selectedRoundIndex);
            selectedRoundIndex = ClampRoundIndex(selectedRoundIndex);
            SaveSelectedMapAsset();
        }

        private void DuplicateSelectedRound()
        {
            if (selectedMap.rounds.Count == 0)
                return;
            selectedRoundIndex = ClampRoundIndex(selectedRoundIndex);
            Undo.RecordObject(selectedMap, "Duplicate Tactical Round");
            var clone = CloneRound(selectedMap.rounds[selectedRoundIndex]);
            clone.roundName = $"{GetRoundDisplayName(selectedRoundIndex, clone)} Copy";
            var insertIndex = selectedRoundIndex + 1;
            selectedMap.rounds.Insert(insertIndex, clone);
            selectedRoundIndex = insertIndex;
            SaveSelectedMapAsset();
        }

        private void MoveSelectedRound(int direction)
        {
            if (selectedMap.rounds.Count == 0)
                return;
            var oldIndex = Mathf.Clamp(selectedRoundIndex, 0, selectedMap.rounds.Count - 1);
            var newIndex = Mathf.Clamp(oldIndex + direction, 0, selectedMap.rounds.Count - 1);
            if (oldIndex == newIndex)
                return;

            Undo.RecordObject(selectedMap, "Move Tactical Round");
            var round = selectedMap.rounds[oldIndex];
            selectedMap.rounds.RemoveAt(oldIndex);
            selectedMap.rounds.Insert(newIndex, round);
            selectedRoundIndex = newIndex;
            SaveSelectedMapAsset();
        }

        private void SetRoundCount(int desiredCount)
        {
            if (selectedMap.rounds.Count == desiredCount)
                return;

            Undo.RecordObject(selectedMap, "Set Tactical Round Count");
            while (selectedMap.rounds.Count < desiredCount)
                selectedMap.rounds.Add(CreateDefaultRound(selectedMap.rounds.Count));
            while (selectedMap.rounds.Count > desiredCount)
                selectedMap.rounds.RemoveAt(selectedMap.rounds.Count - 1);
            selectedRoundIndex = ClampRoundIndex(selectedRoundIndex);
            SaveSelectedMapAsset();
        }

        private int ClampRoundIndex(int index)
        {
            return selectedMap.rounds.Count == 0
                ? 0
                : Mathf.Clamp(index, 0, selectedMap.rounds.Count - 1);
        }

        private TacticalMapData.Round CreateDefaultRound(int index)
        {
            var round = new TacticalMapData.Round
            {
                roundName = $"Round {index + 2}",
                startCondition = RoundStartCondition.EnemiesDefeated,
                startTurn = index + 2
            };
            CopySelectedMapToRound(round);
            return round;
        }

        private void CopySelectedMapToRound(TacticalMapData.Round round)
        {
            if (selectedMap == null || round == null)
                return;
            CopyCells(selectedMap.walkable, round.walkable);
            CopyCells(selectedMap.playerDeployment, round.playerDeployment);
            CopyCells(selectedMap.ladders, round.ladders);
            CopyCells(selectedMap.wood, round.wood);
        }

        private static TacticalMapData.Round CloneRound(TacticalMapData.Round source)
        {
            var clone = new TacticalMapData.Round
            {
                roundName = source.roundName,
                startCondition = source.startCondition,
                startTurn = source.startTurn
            };
            CopyCells(source.walkable, clone.walkable);
            CopyCells(source.playerDeployment, clone.playerDeployment);
            CopyCells(source.ladders, clone.ladders);
            CopyCells(source.wood, clone.wood);
            CopyCells(source.crates, clone.crates);
            CopyCells(source.bombCrates, clone.bombCrates);
            CopyCells(source.objectiveTargets, clone.objectiveTargets);
            CopyCells(source.defenseObjectives, clone.defenseObjectives);
            CopyCells(source.enemyGuardSpawns, clone.enemyGuardSpawns);
            CopyCells(source.enemySoldierSpawns, clone.enemySoldierSpawns);
            return clone;
        }

        private static void CopyCells(
            List<TacticalMapData.Cell> source,
            List<TacticalMapData.Cell> destination)
        {
            destination.Clear();
            if (source == null)
                return;
            foreach (var cell in source)
            {
                if (cell == null)
                    continue;
                destination.Add(new TacticalMapData.Cell
                {
                    position = cell.position,
                    tile = cell.tile,
                    color = cell.color
                });
            }
        }

        private static void RemoveBaseMapCells(
            List<TacticalMapData.Cell> roundCells,
            List<TacticalMapData.Cell> baseCells)
        {
            if (roundCells == null || baseCells == null || baseCells.Count == 0)
                return;

            var basePositions = new HashSet<Vector3Int>();
            foreach (var cell in baseCells)
                if (cell != null && cell.tile != null)
                    basePositions.Add(cell.position);

            roundCells.RemoveAll(cell => cell == null || basePositions.Contains(cell.position));
        }

        private static string GetRoundDisplayName(int index, TacticalMapData.Round round)
        {
            return round == null || string.IsNullOrEmpty(round.roundName)
                ? $"Round {index + 2}"
                : round.roundName;
        }

        private TacticalObjectiveData CreateObjectiveDataAsset(string defaultName)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Tactical Objective Data", defaultName, "asset", "Choose where to save the objective data asset.");
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var asset = ScriptableObject.CreateInstance<TacticalObjectiveData>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            GUI.changed = true;
            return asset;
        }

        private void SaveSelectedMapAsset()
        {
            EditorUtility.SetDirty(selectedMap);
            AssetDatabase.SaveAssets();
        }

        private void SaveAsNew()
        {
            EnsureMapsFolder();
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Tactical Map", "NewTacticalMap", "asset",
                "??ν븷 留??대쫫???낅젰?섏꽭??", "Assets/StellaStair/Maps");
            if (string.IsNullOrEmpty(path))
                return;
            var map = CreateInstance<TacticalMapData>();
            map.mapName = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(map, path);
            selectedMap = map;
            SaveTo(map);
            Selection.activeObject = map;
        }

        private static void EnsureMapsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/StellaStair/Maps"))
                AssetDatabase.CreateFolder("Assets/StellaStair", "Maps");
        }

        private static TacticalBoard FindBoard()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null)
                EditorUtility.DisplayDialog("Map Library", "?꾩옱 ?ъ뿉 TacticalBoard媛 ?놁뒿?덈떎.", "OK");
            return board;
        }

        private static void SaveTo(TacticalMapData map)
        {
            var board = FindBoard();
            if (board == null || map == null)
                return;

            Capture(board.WalkableTilemap, map.walkable);
            Capture(board.PlayerDeploymentTilemap, map.playerDeployment);
            Capture(board.LadderTilemap, map.ladders);
            Capture(board.WoodTilemap, map.wood);
            Capture(board.CrateTilemap, map.crates);
            Capture(board.BombCrateTilemap, map.bombCrates);
            Capture(board.ObjectiveTilemap, map.objectiveTargets);
            Capture(board.DefenseObjectiveTilemap, map.defenseObjectives);
            Capture(FindLayer(board, "Enemy Guard Spawns"), map.enemyGuardSpawns);
            Capture(FindLayer(board, "Enemy Soldier Spawns"), map.enemySoldierSpawns);
            CaptureEnemyUnitLayers(board, map.enemyUnitLayers);
            CaptureObjectiveLayers(board, map.objectiveLayers, false);
            CaptureObjectiveLayers(board, map.defenseObjectiveLayers, true);

            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            Debug.Log($"Map saved: {AssetDatabase.GetAssetPath(map)}");
        }

        private void SaveCurrentMapToRound(TacticalMapData.Round round)
        {
            var board = FindBoard();
            if (board == null || round == null || selectedMap == null)
                return;

            Capture(board.WalkableTilemap, round.walkable);
            Capture(board.PlayerDeploymentTilemap, round.playerDeployment);
            Capture(board.LadderTilemap, round.ladders);
            Capture(board.WoodTilemap, round.wood);
            Capture(board.CrateTilemap, round.crates);
            Capture(board.BombCrateTilemap, round.bombCrates);
            Capture(board.ObjectiveTilemap, round.objectiveTargets);
            Capture(board.DefenseObjectiveTilemap, round.defenseObjectives);
            Capture(FindLayer(board, "Enemy Guard Spawns"), round.enemyGuardSpawns);
            Capture(FindLayer(board, "Enemy Soldier Spawns"), round.enemySoldierSpawns);
            RemoveBaseMapCells(round.enemyGuardSpawns, selectedMap.enemyGuardSpawns);
            RemoveBaseMapCells(round.enemySoldierSpawns, selectedMap.enemySoldierSpawns);

            SaveSelectedMapAsset();
            Debug.Log($"Round saved: {selectedMap.name} / {round.roundName}");
        }

        private void LoadRoundIntoScene(TacticalMapData.Round round)
        {
            var board = FindBoard();
            if (board == null || round == null)
                return;
            if (!EditorUtility.DisplayDialog(
                    "Load Tactical Round",
                    "\uC120\uD0DD\uD55C \uB77C\uC6B4\uB4DC \uB370\uC774\uD130\uB85C \uD604\uC7AC \uC2A4\uD14C\uC774\uC9C0\uC758 \uB9F5\uC744 \uB36E\uC5B4\uC4F0\uB2C8\uB2E4. \uACC4\uC18D\uD558\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?",
                    "Load", "Cancel"))
                return;

            Restore(board.WalkableTilemap, round.walkable);
            Restore(board.PlayerDeploymentTilemap, round.playerDeployment);
            Restore(board.LadderTilemap, round.ladders);
            Restore(board.WoodTilemap, round.wood);
            var crateLayer = board.CrateTilemap != null || round.crates.Count > 0
                ? EnsureLayer(board, "Crates", 18)
                : null;
            Restore(crateLayer, round.crates);
            var bombCrateLayer = board.BombCrateTilemap != null || round.bombCrates.Count > 0
                ? EnsureLayer(board, "Bomb Crates", 19)
                : null;
            Restore(bombCrateLayer, round.bombCrates);
            var objectiveLayer = board.ObjectiveTilemap != null || round.objectiveTargets.Count > 0
                ? EnsureLayer(board, "Attack Objectives", 20)
                : null;
            Restore(objectiveLayer, round.objectiveTargets);
            var defenseObjectiveLayer = board.DefenseObjectiveTilemap != null ||
                                        round.defenseObjectives.Count > 0
                ? EnsureLayer(board, "Defense Objectives", 20)
                : null;
            Restore(defenseObjectiveLayer, round.defenseObjectives);
            Restore(EnsureLayer(board, "Enemy Guard Spawns", 21), round.enemyGuardSpawns);
            Restore(EnsureLayer(board, "Enemy Soldier Spawns", 21), round.enemySoldierSpawns);

            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            SceneView.RepaintAll();
            Debug.Log($"Round loaded: {round.roundName}");
        }

        private void LoadSelected()
        {
            var board = FindBoard();
            if (board == null || selectedMap == null)
                return;
            if (!EditorUtility.DisplayDialog(
                    "Load Tactical Map",
                    "\uC120\uD0DD\uD55C \uB9F5 \uB370\uC774\uD130\uB85C \uD604\uC7AC \uC2A4\uD14C\uC774\uC9C0\uC758 \uB9F5\uC744 \uB36E\uC5B4\uC4F0\uB2C8\uB2E4. \uACC4\uC18D\uD558\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?",
                    "Load", "Cancel"))
                return;

            Restore(board.WalkableTilemap, selectedMap.walkable);
            Restore(board.PlayerDeploymentTilemap, selectedMap.playerDeployment);
            Restore(board.LadderTilemap, selectedMap.ladders);
            Restore(board.WoodTilemap, selectedMap.wood);
            var crateLayer = board.CrateTilemap != null || selectedMap.crates.Count > 0
                ? EnsureLayer(board, "Crates", 18)
                : null;
            Restore(crateLayer, selectedMap.crates);
            var bombCrateLayer = board.BombCrateTilemap != null || selectedMap.bombCrates.Count > 0
                ? EnsureLayer(board, "Bomb Crates", 19)
                : null;
            Restore(bombCrateLayer, selectedMap.bombCrates);
            var objectiveLayer = board.ObjectiveTilemap != null || selectedMap.objectiveTargets.Count > 0
                ? EnsureLayer(board, "Attack Objectives", 20)
                : null;
            Restore(objectiveLayer, selectedMap.objectiveTargets);
            var defenseObjectiveLayer = board.DefenseObjectiveTilemap != null ||
                                        selectedMap.defenseObjectives.Count > 0
                ? EnsureLayer(board, "Defense Objectives", 20)
                : null;
            Restore(defenseObjectiveLayer, selectedMap.defenseObjectives);
            Restore(EnsureLayer(board, "Enemy Guard Spawns", 21), selectedMap.enemyGuardSpawns);
            Restore(EnsureLayer(board, "Enemy Soldier Spawns", 21), selectedMap.enemySoldierSpawns);
            RestoreEnemyUnitLayers(board, selectedMap.enemyUnitLayers);
            selectedMap.RemoveGeneratedObjectiveLayers(board);
            RestoreObjectiveLayers(board, selectedMap.objectiveLayers, false);
            RestoreObjectiveLayers(board, selectedMap.defenseObjectiveLayers, true);

            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            SceneView.RepaintAll();
            Debug.Log($"Map loaded: {selectedMap.name}");
        }

        private void RegisterSelectedStage()
        {
            if (selectedMap == null)
                return;
            var board = FindBoard();
            if (board == null)
                return;
            var progression = Object.FindAnyObjectByType<StageProgression>();
            if (progression == null)
                progression = board.gameObject.AddComponent<StageProgression>();

            var serialized = new SerializedObject(progression);
            var stages = serialized.FindProperty("registeredStages");
            for (var i = 0; i < stages.arraySize; i++)
            {
                if (stages.GetArrayElementAtIndex(i).objectReferenceValue == selectedMap)
                    return;
            }
            var index = stages.arraySize;
            stages.InsertArrayElementAtIndex(index);
            stages.GetArrayElementAtIndex(index).objectReferenceValue = selectedMap;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(progression);
            EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
            Debug.Log($"Stage registered: {selectedMap.name}");
        }

        private static Tilemap FindLayer(TacticalBoard board, string name)
        {
            if (board == null)
                return null;
            var child = board.Grid != null ? board.Grid.transform.Find(name) : null;
            return child != null ? child.GetComponent<Tilemap>() : null;
        }

        private static Tilemap EnsureLayer(TacticalBoard board, string name, int sortingOrder)
        {
            if (board == null || board.Grid == null)
                return null;
            var existing = FindLayer(board, name);
            if (existing != null)
                return existing;

            Undo.RegisterCompleteObjectUndo(board.Grid.gameObject, "Create Tactical Map Layer");
            var gameObject = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            gameObject.transform.SetParent(board.Grid.transform, false);
            gameObject.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            var tilemap = gameObject.GetComponent<Tilemap>();
            if (name == "Crates")
                board.ConfigureCrates(tilemap);
            if (name == "Bomb Crates")
                board.ConfigureBombCrates(tilemap);
            if (name == "Attack Objectives")
                board.ConfigureObjectives(tilemap, board.ObjectiveMaxHealth);
            if (name == "Defense Objectives")
                board.ConfigureDefenseObjectives(tilemap, board.DefenseObjectiveMaxHealth);
            EditorUtility.SetDirty(board);
            return tilemap;
        }

        private static void CaptureEnemyUnitLayers(
            TacticalBoard board, List<TacticalMapData.EnemyUnitLayer> layers)
        {
            if (board == null || layers == null)
                return;
            foreach (var layer in layers)
            {
                if (layer == null || layer.definition == null)
                    continue;
                Capture(
                    FindLayer(board, GetEnemyLayerName(layer)),
                    layer.spawns);
            }
        }

        private static void RestoreEnemyUnitLayers(
            TacticalBoard board, List<TacticalMapData.EnemyUnitLayer> layers)
        {
            if (board == null || layers == null)
                return;
            foreach (var layer in layers)
            {
                if (layer == null || layer.definition == null)
                    continue;
                var tilemap = EnsureLayer(board, GetEnemyLayerName(layer), 21);
                Restore(tilemap, layer.spawns);
                var marker = tilemap.GetComponent<EnemySpawnTilemap>();
                if (marker == null)
                    marker = tilemap.gameObject.AddComponent<EnemySpawnTilemap>();
                marker.Configure(layer.definition, layer.color);
            }
        }

        private static string GetEnemyLayerName(TacticalMapData.EnemyUnitLayer layer)
        {
            var definitionName = layer != null && layer.definition != null
                ? layer.definition.name
                : "Enemy";
            return $"{definitionName} Spawns";
        }
        private static void CaptureObjectiveLayers(TacticalBoard board, List<TacticalMapData.ObjectiveLayer> layers, bool defense)
        {
            if (board == null || layers == null)
                return;
            foreach (var layer in layers)
                if (layer != null && layer.data != null)
                    Capture(FindLayer(board, GetObjectiveLayerName(layer, defense)), layer.spawns);
        }

        private static void RestoreObjectiveLayers(TacticalBoard board, List<TacticalMapData.ObjectiveLayer> layers, bool defense)
        {
            if (board == null || layers == null)
                return;
            foreach (var layer in layers)
            {
                if (layer == null || layer.data == null)
                    continue;
                Restore(EnsureLayer(board, GetObjectiveLayerName(layer, defense), 20), layer.spawns);
            }
        }

        private static string GetObjectiveLayerName(TacticalMapData.ObjectiveLayer layer, bool defense)
        {
            var name = layer != null && layer.data != null ? layer.data.name : "Objective";
            return $"{name}{(defense ? " Defense" : string.Empty)} Objectives";
        }

        private static void Capture(Tilemap tilemap, List<TacticalMapData.Cell> destination)
        {
            destination.Clear();
            if (tilemap == null)
                return;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile == null)
                    continue;
                destination.Add(new TacticalMapData.Cell
                {
                    position = position,
                    tile = tile,
                    color = tilemap.GetColor(position)
                });
            }
        }

        private static void Restore(Tilemap tilemap, List<TacticalMapData.Cell> source)
        {
            if (tilemap == null)
                return;
            Undo.RegisterCompleteObjectUndo(tilemap, "Load Tactical Map");
            tilemap.ClearAllTiles();
            foreach (var cell in source)
            {
                if (cell == null || cell.tile == null)
                    continue;
                tilemap.SetTile(cell.position, cell.tile);
                tilemap.SetTileFlags(cell.position, TileFlags.None);
                tilemap.SetColor(cell.position, cell.color);
            }
            EditorUtility.SetDirty(tilemap);
        }
    }
}

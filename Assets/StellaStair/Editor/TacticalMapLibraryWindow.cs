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
                "현재 씬의 전술 맵 배치를 Map Data로 저장하거나 불러옵니다.",
                MessageType.Info);
            selectedMap = (TacticalMapData)EditorGUILayout.ObjectField(
                "Selected Map", selectedMap, typeof(TacticalMapData), false);
            if (selectedMap != null)
            {
                EditorGUI.BeginChangeCheck();
                var stageType = (TacticalStageType)EditorGUILayout.EnumPopup(
                    "Stage Type", selectedMap.stageType);
                var defenseTurns = EditorGUILayout.IntField(
                    "Defense Turns", selectedMap.defenseTurnsToSurvive);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedMap, "Change Stage Type");
                    selectedMap.stageType = stageType;
                    selectedMap.defenseTurnsToSurvive = Mathf.Max(1, defenseTurns);
                    SaveSelectedMapAsset();
                }

                DrawRoundControls();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Current Map As New"))
                SaveAsNew();
            using (new EditorGUI.DisabledScope(selectedMap == null))
            {
                if (GUILayout.Button("Overwrite Selected Map"))
                    SaveTo(selectedMap);
                if (GUILayout.Button("Load Selected Map Into Scene"))
                    LoadSelected();
                if (GUILayout.Button("Register Selected Map As Stage"))
                    RegisterSelectedStage();
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
                    "No extra rounds. Add a round, then save the current scene map into it.",
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
                "저장할 맵 이름을 입력하세요.", "Assets/StellaStair/Maps");
            if (string.IsNullOrEmpty(path))
                return;
            var map = CreateInstance<TacticalMapData>();
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
                EditorUtility.DisplayDialog("Map Library", "현재 씬에 TacticalBoard가 없습니다.", "OK");
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
                    "현재 씬의 배치를 선택한 라운드 맵으로 교체할까요?",
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
                    "현재 씬의 배치를 선택한 맵으로 교체할까요?",
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

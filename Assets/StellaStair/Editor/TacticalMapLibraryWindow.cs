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

        [MenuItem("Stella Stair/Map Library")]
        private static void Open() =>
            GetWindow<TacticalMapLibraryWindow>("Stella Map Library");

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tactical Map Library", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "현재 씬의 지형·오브젝트·적 스폰 타일을 Map Data로 저장하거나 불러옵니다.",
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
                    EditorUtility.SetDirty(selectedMap);
                    AssetDatabase.SaveAssets();
                }
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

        private void LoadSelected()
        {
            var board = FindBoard();
            if (board == null || selectedMap == null)
                return;
            if (!EditorUtility.DisplayDialog(
                    "Load Tactical Map",
                    "현재 타일 배치를 선택한 맵으로 교체할까요?",
                    "Load", "Cancel"))
                return;

            Restore(board.WalkableTilemap, selectedMap.walkable);
            Restore(board.PlayerDeploymentTilemap, selectedMap.playerDeployment);
            Restore(board.LadderTilemap, selectedMap.ladders);
            Restore(board.WoodTilemap, selectedMap.wood);
            Restore(board.CrateTilemap, selectedMap.crates);
            Restore(board.BombCrateTilemap, selectedMap.bombCrates);
            var objectiveLayer = board.ObjectiveTilemap != null || selectedMap.objectiveTargets.Count > 0
                ? EnsureLayer(board, "Attack Objectives", 20)
                : null;
            Restore(objectiveLayer, selectedMap.objectiveTargets);
            var defenseObjectiveLayer = board.DefenseObjectiveTilemap != null ||
                                        selectedMap.defenseObjectives.Count > 0
                ? EnsureLayer(board, "Defense Objectives", 20)
                : null;
            Restore(defenseObjectiveLayer, selectedMap.defenseObjectives);
            Restore(FindLayer(board, "Enemy Guard Spawns"), selectedMap.enemyGuardSpawns);
            Restore(FindLayer(board, "Enemy Soldier Spawns"), selectedMap.enemySoldierSpawns);

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
            if (name == "Attack Objectives")
                board.ConfigureObjectives(tilemap, board.ObjectiveMaxHealth);
            if (name == "Defense Objectives")
                board.ConfigureDefenseObjectives(tilemap, board.DefenseObjectiveMaxHealth);
            EditorUtility.SetDirty(board);
            return tilemap;
        }

        private static void Capture(
            Tilemap tilemap, List<TacticalMapData.Cell> destination)
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

        private static void Restore(
            Tilemap tilemap, List<TacticalMapData.Cell> source)
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

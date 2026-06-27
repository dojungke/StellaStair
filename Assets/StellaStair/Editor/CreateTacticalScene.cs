using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Grid;
using StellaStair.Input;
using StellaStair.Presentation;
using StellaStair.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace StellaStair.Editor
{
    public static class CreateTacticalScene
    {
        private const string Root = "Assets/StellaStair/Sample";

        [MenuItem("Stella Stair/Create Tactical Battle Scene")]
        public static void Create()
        {
            EnsureFolder("Assets/StellaStair", "Sample");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sprite = CreateSquareSprite($"{Root}/Square.asset");
            var groundTile = CreateTile($"{Root}/Ground.asset", sprite, new Color(0.22f, 0.25f, 0.31f));
            var zoneTile = CreateTile($"{Root}/DeploymentZone.asset", sprite, new Color(0.1f, 0.55f, 0.85f, 0.35f));
            var ladderTile = CreateTile($"{Root}/Ladder.asset", sprite, new Color(0.72f, 0.46f, 0.18f, 0.9f));
            var woodTile = CreateTile($"{Root}/Wood.asset", sprite, new Color(0.55f, 0.3f, 0.12f));
            var crateTile = CreateTile($"{Root}/Crate.asset", sprite, new Color(0.62f, 0.34f, 0.14f));
            var bombCrateTile = CreateTile(
                $"{Root}/BombCrate.asset", sprite, new Color(0.9f, 0.12f, 0.05f));
            var objectiveTile = CreateTile(
                $"{Root}/AttackObjective.asset", sprite, new Color(1f, 0.82f, 0.12f));
            var defenseObjectiveTile = CreateTile(
                $"{Root}/DefenseObjective.asset", sprite, new Color(0.2f, 0.95f, 1f));
            var guardSpawnTile = CreateTile(
                $"{Root}/EnemyGuardSpawn.asset", sprite, new Color(0.95f, 0.25f, 0.22f, 0.75f));
            var soldierSpawnTile = CreateTile(
                $"{Root}/EnemySoldierSpawn.asset", sprite, new Color(0.75f, 0.15f, 0.2f, 0.75f));

            var gridObject = new GameObject("Tactical Grid", typeof(UnityEngine.Grid));
            var grid = gridObject.GetComponent<UnityEngine.Grid>();
            var walkable = CreateTilemap(gridObject.transform, "Walkable", 0);
            var deploymentZone = CreateTilemap(gridObject.transform, "Player Deployment", 1);
            var ladders = CreateTilemap(gridObject.transform, "Ladders", 2);
            var wood = CreateTilemap(gridObject.transform, "Wood", 1);
            var crates = CreateTilemap(gridObject.transform, "Crates", 18);
            var bombCrates = CreateTilemap(gridObject.transform, "Bomb Crates", 19);
            var objectives = CreateTilemap(gridObject.transform, "Attack Objectives", 20);
            var defenseObjectives = CreateTilemap(gridObject.transform, "Defense Objectives", 20);
            var guardSpawns = CreateTilemap(gridObject.transform, "Enemy Guard Spawns", 16);
            var soldierSpawns = CreateTilemap(gridObject.transform, "Enemy Soldier Spawns", 16);
            guardSpawns.gameObject.AddComponent<EnemySpawnTilemap>().Configure(
                AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                    "Assets/StellaStair/Resources/UnitDefinitions/EnemyGuard.asset"),
                new Color(0.95f, 0.25f, 0.22f));
            soldierSpawns.gameObject.AddComponent<EnemySpawnTilemap>().Configure(
                AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                    "Assets/StellaStair/Resources/UnitDefinitions/EnemySoldier.asset"),
                new Color(0.75f, 0.15f, 0.2f));

            for (var x = -8; x <= 8; x++)
            {
                walkable.SetTile(new Vector3Int(x, 0), groundTile);
                if (x is >= -7 and <= -3)
                    deploymentZone.SetTile(new Vector3Int(x, 0), zoneTile);
            }
            for (var x = 1; x <= 4; x++)
                walkable.SetTile(new Vector3Int(x, 1), groundTile);
            walkable.SetTile(new Vector3Int(3, 3), groundTile);
            ladders.SetTile(new Vector3Int(3, 2), ladderTile);
            ladders.SetTile(new Vector3Int(3, 3), ladderTile);
            wood.SetTile(new Vector3Int(0, 1), woodTile);
            wood.SetTile(new Vector3Int(5, 1), woodTile);
            crates.SetTile(new Vector3Int(2, 2), crateTile);
            bombCrates.SetTile(new Vector3Int(6, 1), bombCrateTile);
            objectives.SetTile(new Vector3Int(8, 1), objectiveTile);
            defenseObjectives.SetTile(new Vector3Int(-1, 1), defenseObjectiveTile);
            guardSpawns.SetTile(new Vector3Int(4, 2), guardSpawnTile);
            soldierSpawns.SetTile(new Vector3Int(7, 1), soldierSpawnTile);

            var systems = new GameObject("Tactical Systems");
            var board = systems.AddComponent<TacticalBoard>();
            board.Configure(grid, walkable, deploymentZone);
            board.ConfigureLadder(ladders);
            board.ConfigureWood(wood, 2);
            board.ConfigureCrates(crates);
            board.ConfigureBombCrates(bombCrates);
            board.ConfigureObjectives(objectives, 8);
            board.ConfigureDefenseObjectives(defenseObjectives, 12);
            var deployment = systems.AddComponent<DeploymentManager>();
            systems.AddComponent<StageProgression>();
            var highlighter = new GameObject("Grid Highlights").AddComponent<GridHighlighter>();
            var camera = CreateCamera();
            var input = systems.AddComponent<TacticalInputController>();

            var units = new List<TacticalUnit>
            {
                CreateUnit("Player Knight", new Vector3(-6, 2.5f), sprite, new Color(0.25f, 0.65f, 1f)),
                CreateUnit("Player Archer", new Vector3(-4, 2.5f), sprite, new Color(0.3f, 0.9f, 0.55f))
            };
            deployment.Configure(board, units);
            input.Configure(camera, deployment, highlighter);

            var instructions = new GameObject("Instructions");
            instructions.AddComponent<SampleInstructions>();

            EditorSceneManager.MarkSceneDirty(scene);
            var path = $"{Root}/TacticalBattle.unity";
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeObject = systems;
            AssetDatabase.SaveAssets();
            Debug.Log($"전술 전장 씬 생성 완료: {path}");
        }

        [MenuItem("Stella Stair/Add Attack Objective Tilemap To Current Scene")]
        public static void AddAttackObjectiveTilemapToCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("TacticalBoard가 있는 씬을 먼저 열어 주세요.");
                return;
            }

            EnsureFolder("Assets/StellaStair", "Sample");
            var sprite = CreateSquareSprite($"{Root}/Square.asset");
            var objectiveTile = CreateTile(
                $"{Root}/AttackObjective.asset", sprite, new Color(1f, 0.82f, 0.12f));

            if (board.ObjectiveTilemap == null)
            {
                var objectives = CreateTilemap(board.Grid.transform, "Attack Objectives", 20);
                board.ConfigureObjectives(objectives, 8);
                EditorUtility.SetDirty(board);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Selection.activeObject = objectiveTile;
            AssetDatabase.SaveAssets();
            Debug.Log("Attack Objectives 타일맵과 AttackObjective 타일을 준비했습니다. 목표는 적진의 서 있는 칸에 배치하세요.");
        }

        [MenuItem("Stella Stair/Add Defense Objective Tilemap To Current Scene")]
        public static void AddDefenseObjectiveTilemapToCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("TacticalBoard가 있는 씬을 먼저 열어 주세요.");
                return;
            }

            EnsureFolder("Assets/StellaStair", "Sample");
            var sprite = CreateSquareSprite($"{Root}/Square.asset");
            var objectiveTile = CreateTile(
                $"{Root}/DefenseObjective.asset", sprite, new Color(0.2f, 0.95f, 1f));

            if (board.DefenseObjectiveTilemap == null)
            {
                var objectives = CreateTilemap(board.Grid.transform, "Defense Objectives", 20);
                board.ConfigureDefenseObjectives(objectives, 12);
                EditorUtility.SetDirty(board);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Selection.activeObject = objectiveTile;
            AssetDatabase.SaveAssets();
            Debug.Log("Defense Objectives 타일맵과 DefenseObjective 타일을 준비했습니다. 지킬 목표 위치에 배치하세요.");
        }

        [MenuItem("Stella Stair/Add Crate Tilemap To Current Scene")]
        public static void AddCrateTilemapToCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("TacticalBoard가 있는 씬을 먼저 열어 주세요.");
                return;
            }

            EnsureFolder("Assets/StellaStair", "Sample");
            var sprite = CreateSquareSprite($"{Root}/Square.asset");
            var crateTile = CreateTile(
                $"{Root}/Crate.asset", sprite, new Color(0.62f, 0.34f, 0.14f));
            var bombCrateTile = CreateTile(
                $"{Root}/BombCrate.asset", sprite, new Color(0.9f, 0.12f, 0.05f));
            var crates = board.CrateTilemap;
            if (crates == null)
            {
                crates = CreateTilemap(board.Grid.transform, "Crates", 18);
                board.ConfigureCrates(crates);
                EditorUtility.SetDirty(board);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            var bombCrates = board.BombCrateTilemap;
            if (bombCrates == null)
            {
                bombCrates = CreateTilemap(board.Grid.transform, "Bomb Crates", 19);
                board.ConfigureBombCrates(bombCrates);
                EditorUtility.SetDirty(board);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Selection.objects = new Object[] { crateTile, bombCrateTile };
            AssetDatabase.SaveAssets();
            Debug.Log("Crates 타일맵과 Crate 타일을 준비했습니다. Crate.asset을 Tile Palette에 추가해 배치하세요.");
        }

        [MenuItem("Stella Stair/Add Enemy Spawn Tilemaps To Current Scene")]
        public static void AddEnemySpawnTilemapsToCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("TacticalBoard가 있는 씬을 먼저 열어 주세요.");
                return;
            }

            EnsureFolder("Assets/StellaStair", "Sample");
            var sprite = CreateSquareSprite($"{Root}/Square.asset");
            var guardTile = CreateTile(
                $"{Root}/EnemyGuardSpawn.asset", sprite,
                new Color(0.95f, 0.25f, 0.22f, 0.75f));
            var soldierTile = CreateTile(
                $"{Root}/EnemySoldierSpawn.asset", sprite,
                new Color(0.75f, 0.15f, 0.2f, 0.75f));

            CreateEnemySpawnLayerIfMissing(
                board.Grid.transform, "Enemy Guard Spawns", "EnemyGuard",
                new Color(0.95f, 0.25f, 0.22f));
            CreateEnemySpawnLayerIfMissing(
                board.Grid.transform, "Enemy Soldier Spawns", "EnemySoldier",
                new Color(0.75f, 0.15f, 0.2f));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.objects = new Object[] { guardTile, soldierTile };
            AssetDatabase.SaveAssets();
        }

        private static void CreateEnemySpawnLayerIfMissing(
            Transform grid, string layerName, string definitionName, Color color)
        {
            if (grid.Find(layerName) != null)
                return;
            var tilemap = CreateTilemap(grid, layerName, 16);
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                $"Assets/StellaStair/Resources/UnitDefinitions/{definitionName}.asset");
            tilemap.gameObject.AddComponent<EnemySpawnTilemap>().Configure(definition, color);
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int order)
        {
            var gameObject = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            gameObject.transform.SetParent(parent);
            gameObject.GetComponent<TilemapRenderer>().sortingOrder = order;
            return gameObject.GetComponent<Tilemap>();
        }

        private static Tile CreateTile(string path, Sprite sprite, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (existing != null)
            {
                existing.sprite = sprite;
                existing.color = color;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            AssetDatabase.CreateAsset(tile, path);
            return tile;
        }

        private static Sprite CreateSquareSprite(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite existing)
                    return existing;

            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = "Square Texture",
                filterMode = FilterMode.Point
            };
            var pixels = new Color[16 * 16];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);

            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = "Square";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.ImportAsset(path);
            return sprite;
        }

        private static Camera CreateCamera()
        {
            var gameObject = new GameObject(
                "Main Camera", typeof(Camera), typeof(TacticalCameraPan));
            gameObject.tag = "MainCamera";
            gameObject.transform.position = new Vector3(0, 1.5f, -10);
            var camera = gameObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.15f);
            return camera;
        }

        private static TacticalUnit CreateUnit(string name, Vector3 position, Sprite sprite, Color color)
        {
            var gameObject = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(TacticalUnit));
            gameObject.transform.position = position;
            gameObject.transform.localScale = new Vector3(0.75f, 1.25f, 1f);
            var renderer = gameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 20;
            gameObject.GetComponent<BoxCollider2D>().size = Vector2.one;
            gameObject.GetComponent<TacticalUnit>().Configure(null, UnitTeam.Player);
            return gameObject.GetComponent<TacticalUnit>();
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}

using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Grid;
using StellaStair.Input;
using StellaStair.Presentation;
using StellaStair.Units;
using StellaStair.Town;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace StellaStair.Editor
{
    public static class CreateTacticalScene
    {
        private const string Root = "Assets/StellaStair/Sample";
        private const string GameDataRoot = "Assets/StellaStair/GameData";
        private const string ObjectDatabasePath = GameDataRoot + "/TacticalObjectDatabase.asset";
        private const string DefaultBattleUiPath = "Assets/StellaStair/UI/BattleUI.asset";
        private const string TempUiPrefabPrefix = "Assets/StellaStair/Sample/__TempCurrentSceneUi";

        [MenuItem("Stella Stair/Create Tactical Battle Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder("Assets/StellaStair", "Sample");
            var capturedUiPrefabPath = CaptureCurrentSceneUiPrefab();
            var objectDatabase = GetOrCreateObjectDatabase();
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
            guardSpawns.SetTile(new Vector3Int(4, 2), guardSpawnTile);
            soldierSpawns.SetTile(new Vector3Int(7, 1), soldierSpawnTile);

            var systems = new GameObject("Tactical Systems");
            var board = systems.AddComponent<TacticalBoard>();
            board.ConfigureObjectDatabase(objectDatabase);
            board.Configure(grid, walkable, deploymentZone);
            board.ConfigureLadder(ladders);
            board.ConfigureWood(wood, objectDatabase.WoodMaxHealth);
            board.ConfigureCrates(crates);
            board.ConfigureBombCrates(bombCrates);
            board.ConfigureObjectives(objectives, objectDatabase.AttackObjectiveMaxHealth);
            board.ConfigureDefenseObjectives(defenseObjectives, objectDatabase.DefenseObjectiveMaxHealth);
            var deployment = systems.AddComponent<DeploymentManager>();
            systems.AddComponent<StageProgression>();
            systems.AddComponent<TownHubPresenter>().EnsureUiExistsInScene();
            var highlighter = new GameObject("Grid Highlights").AddComponent<GridHighlighter>();
            var camera = CreateCamera();
            var input = systems.AddComponent<TacticalInputController>();
            systems.AddComponent<PlayerPartySpawner>();

            deployment.Configure(board, new List<TacticalUnit>());
            input.Configure(camera, deployment, highlighter);
            RestoreCapturedSceneUi(capturedUiPrefabPath);
            ApplyDefaultBattleUiToCurrentScene();
            RemoveDuplicateEventSystems();
            DialogueUiBinder.BindCurrentSceneDialogueUi();
            ActionButtonBinder.BindCurrentSceneButtons();
            EditorSceneManager.MarkSceneDirty(scene);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{Root}/TacticalBattle.unity");
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeObject = systems;
            AssetDatabase.SaveAssets();
            Debug.Log($"?�술 ?�장 ???�성 ?�료: {path}");
        }
        public static void CreateDefaultObjectDatabase()
        {
            var database = GetOrCreateObjectDatabase();
            foreach (var board in Object.FindObjectsByType<TacticalBoard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (board.ObjectDatabase != null)
                    continue;
                board.ConfigureObjectDatabase(database);
                EditorUtility.SetDirty(board);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeObject = database;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Stella Stair/Create Missing Tactical Tilemaps In Current Scene")]
        public static void CreateMissingTilemapsInCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("Open a scene with a TacticalBoard and Tactical Grid first.");
                return;
            }

            AddAttackObjectiveTilemapToCurrentScene();
            AddDefenseObjectiveTilemapToCurrentScene();
            AddCrateTilemapToCurrentScene();
            AddEnemySpawnTilemapsToCurrentScene();
            Debug.Log("Checked tactical tilemaps and created only missing current-scene tilemaps.");
        }

        public static void AddAttackObjectiveTilemapToCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("TacticalBoard가 ?�는 ?�을 먼�? ?�어 주세??");
                return;
            }

            EnsureFolder("Assets/StellaStair", "Sample");
            EnsureBoardObjectDatabase(board);
            var sprite = CreateSquareSprite($"{Root}/Square.asset");
            var objectiveTile = CreateTile(
                $"{Root}/AttackObjective.asset", sprite, new Color(1f, 0.82f, 0.12f));

            if (board.ObjectiveTilemap == null)
            {
                var objectives = CreateTilemap(board.Grid.transform, "Attack Objectives", 20);
                board.ConfigureObjectives(objectives, board.ObjectiveMaxHealth);
                EditorUtility.SetDirty(board);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Selection.activeObject = objectiveTile;
            AssetDatabase.SaveAssets();
            Debug.Log("Attack Objectives ?�?�맵�?AttackObjective ?�?�을 준비했?�니?? 목표???�진?????�는 칸에 배치?�세??");
        }
        public static void AddDefenseObjectiveTilemapToCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("TacticalBoard가 ?�는 ?�을 먼�? ?�어 주세??");
                return;
            }

            EnsureFolder("Assets/StellaStair", "Sample");
            EnsureBoardObjectDatabase(board);
            var sprite = CreateSquareSprite($"{Root}/Square.asset");
            var objectiveTile = CreateTile(
                $"{Root}/DefenseObjective.asset", sprite, new Color(0.2f, 0.95f, 1f));

            if (board.DefenseObjectiveTilemap == null)
            {
                var objectives = CreateTilemap(board.Grid.transform, "Defense Objectives", 20);
                board.ConfigureDefenseObjectives(objectives, board.DefenseObjectiveMaxHealth);
                EditorUtility.SetDirty(board);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Selection.activeObject = objectiveTile;
            AssetDatabase.SaveAssets();
            Debug.Log("Defense Objectives ?�?�맵�?DefenseObjective ?�?�을 준비했?�니?? 지??목표 ?�치??배치?�세??");
        }
        public static void AddCrateTilemapToCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("TacticalBoard가 ?�는 ?�을 먼�? ?�어 주세??");
                return;
            }

            EnsureFolder("Assets/StellaStair", "Sample");
            EnsureBoardObjectDatabase(board);
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
            Debug.Log("Crates ?�?�맵�?Crate ?�?�을 준비했?�니?? Crate.asset??Tile Palette??추�???배치?�세??");
        }
        public static void AddEnemySpawnTilemapsToCurrentScene()
        {
            var board = Object.FindAnyObjectByType<TacticalBoard>();
            if (board == null || board.Grid == null)
            {
                Debug.LogError("TacticalBoard가 ?�는 ?�을 먼�? ?�어 주세??");
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



        private static string CaptureCurrentSceneUiPrefab()
        {
            var roots = CollectCurrentSceneUiRoots();
            if (roots.Count == 0)
                return string.Empty;

            var container = new GameObject("Captured Scene UI");
            try
            {
                foreach (var root in roots)
                {
                    var copy = Object.Instantiate(root, container.transform);
                    copy.name = root.name;
                }

                var path = AssetDatabase.GenerateUniqueAssetPath(TempUiPrefabPrefix + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(container, path);
                return path;
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        private static List<GameObject> CollectCurrentSceneUiRoots()
        {
            var roots = new List<GameObject>();
            var seen = new HashSet<GameObject>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AddRoot(canvas != null ? canvas.transform.root.gameObject : null, roots, seen);
            foreach (var eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AddRoot(eventSystem != null ? eventSystem.transform.root.gameObject : null, roots, seen);
            return roots;
        }

        private static void AddRoot(GameObject root, List<GameObject> roots, HashSet<GameObject> seen)
        {
            if (root == null || !root.scene.IsValid() || !seen.Add(root))
                return;
            roots.Add(root);
        }

        private static void RestoreCapturedSceneUi(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    instance.name = prefab.name;
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    var children = new List<Transform>();
                    foreach (Transform child in instance.transform)
                        children.Add(child);
                    foreach (var child in children)
                        child.SetParent(null, true);
                    Object.DestroyImmediate(instance);
                }
            }

            AssetDatabase.DeleteAsset(prefabPath);
        }

        private static void RemoveDuplicateEventSystems()
        {
            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 1; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    Object.DestroyImmediate(systems[i].gameObject);
            }
        }
        private static void ApplyDefaultBattleUiToCurrentScene()
        {
            var battleUi = AssetDatabase.LoadAssetAtPath<BattleUiData>(DefaultBattleUiPath);
            if (battleUi == null)
            {
                Debug.LogWarning($"Default battle UI asset not found: {DefaultBattleUiPath}");
                return;
            }

            battleUi.ApplyToCurrentScene();
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


        private static TacticalObjectDatabase GetOrCreateObjectDatabase()
        {
            EnsureFolder("Assets/StellaStair", "GameData");
            var database = AssetDatabase.LoadAssetAtPath<TacticalObjectDatabase>(ObjectDatabasePath);
            if (database != null)
                return database;

            database = ScriptableObject.CreateInstance<TacticalObjectDatabase>();
            AssetDatabase.CreateAsset(database, ObjectDatabasePath);
            return database;
        }

        private static void EnsureBoardObjectDatabase(TacticalBoard board)
        {
            if (board == null || board.ObjectDatabase != null)
                return;
            board.ConfigureObjectDatabase(GetOrCreateObjectDatabase());
            EditorUtility.SetDirty(board);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}

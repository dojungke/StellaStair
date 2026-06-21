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

            var gridObject = new GameObject("Tactical Grid", typeof(UnityEngine.Grid));
            var grid = gridObject.GetComponent<UnityEngine.Grid>();
            var walkable = CreateTilemap(gridObject.transform, "Walkable", 0);
            var deploymentZone = CreateTilemap(gridObject.transform, "Player Deployment", 1);
            var ladders = CreateTilemap(gridObject.transform, "Ladders", 2);

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

            var systems = new GameObject("Tactical Systems");
            var board = systems.AddComponent<TacticalBoard>();
            board.Configure(grid, walkable, deploymentZone);
            board.ConfigureLadder(ladders);
            var deployment = systems.AddComponent<DeploymentManager>();
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
            var gameObject = new GameObject("Main Camera", typeof(Camera));
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

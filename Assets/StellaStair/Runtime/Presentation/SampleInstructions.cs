using StellaStair.Battle;
using StellaStair.Grid;
using StellaStair.Units;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StellaStair.Presentation
{
    public sealed class SampleInstructions : MonoBehaviour
    {
        private GUIStyle style;
        private DeploymentManager battle;

        private void Start()
        {
            battle = FindAnyObjectByType<DeploymentManager>();
            var board = battle != null ? battle.Board : null;
            if (board != null && board.LadderTilemap == null)
                CreateSampleLadder();
            if (board != null && board.WoodTilemap == null)
                CreateSampleWood();
            if (board != null && board.CrateTilemap == null)
                CreateSampleCrates();
            if (board != null && board.BombCrateTilemap == null)
                CreateSampleBombCrates();
            if (board != null && board.ObjectiveTilemap == null)
                CreateSampleObjectives();

            var knight = LoadDefinition("Knight");
            var archer = LoadDefinition("Archer");
            var wizard = LoadDefinition("Wizard");
            var enemyGuard = LoadDefinition("EnemyGuard");
            var enemySoldier = LoadDefinition("EnemySoldier");

            CreateDefaultPlayerParty(wizard, archer, knight);

            if (battle != null && battle.EnemyUnits.Count == 0)
            {
                SpawnEnemy("Enemy Guard", new GridPosition(4, 1), new Color(0.95f, 0.25f, 0.22f),
                    enemyGuard);
                SpawnEnemy("Enemy Soldier", new GridPosition(7, 0), new Color(0.75f, 0.15f, 0.2f),
                    enemySoldier);
            }
        }

        private void CreateDefaultPlayerParty(
            UnitDefinition wizard, UnitDefinition archer, UnitDefinition knight)
        {
            if (battle == null || battle.PlayerUnits.Count > 0)
                return;

            battle.ClearPlayers(true);
            SpawnPlayer("Player Wizard", new Vector3(-4f, 3.5f),
                new Color(0.7f, 0.35f, 1f), wizard);
            SpawnPlayer("Player Archer", new Vector3(-3f, 3.5f),
                new Color(0.25f, 0.85f, 0.35f), archer);
            SpawnPlayer("Player Knight", new Vector3(-2f, 3.5f),
                new Color(0.25f, 0.55f, 1f), knight);
        }

        private void CreateSampleLadder()
        {
            var board = battle.Board;
            var ladderObject = new GameObject("Ladders", typeof(Tilemap), typeof(TilemapRenderer));
            ladderObject.transform.SetParent(board.Grid.transform, false);
            var ladderMap = ladderObject.GetComponent<Tilemap>();
            ladderObject.GetComponent<TilemapRenderer>().sortingOrder = 2;

            var groundTile = board.WalkableTilemap.GetTile(new Vector3Int(3, 1, 0));
            if (groundTile != null)
                board.WalkableTilemap.SetTile(new Vector3Int(3, 3, 0), groundTile);

            var sprite = GetSampleSprite();
            var ladderTile = ScriptableObject.CreateInstance<Tile>();
            ladderTile.hideFlags = HideFlags.DontSave;
            ladderTile.sprite = sprite;
            ladderTile.color = new Color(0.72f, 0.46f, 0.18f, 0.9f);
            ladderMap.SetTile(new Vector3Int(3, 2, 0), ladderTile);
            ladderMap.SetTile(new Vector3Int(3, 3, 0), ladderTile);
            board.ConfigureLadder(ladderMap);
        }

        private void CreateSampleWood()
        {
            var board = battle.Board;
            var woodObject = new GameObject("Wood", typeof(Tilemap), typeof(TilemapRenderer));
            woodObject.transform.SetParent(board.Grid.transform, false);
            var woodMap = woodObject.GetComponent<Tilemap>();
            woodObject.GetComponent<TilemapRenderer>().sortingOrder = 1;

            var sprite = GetSampleSprite();
            var woodTile = ScriptableObject.CreateInstance<Tile>();
            woodTile.hideFlags = HideFlags.DontSave;
            woodTile.sprite = sprite;
            woodTile.color = new Color(0.55f, 0.3f, 0.12f);
            woodMap.SetTile(new Vector3Int(0, 1, 0), woodTile);
            woodMap.SetTile(new Vector3Int(5, 1, 0), woodTile);
            board.ConfigureWood(woodMap, board.WoodMaxHealth);
        }

        private void CreateSampleCrates()
        {
            var board = battle.Board;
            var crateObject = new GameObject("Crates", typeof(Tilemap), typeof(TilemapRenderer));
            crateObject.transform.SetParent(board.Grid.transform, false);
            var crateMap = crateObject.GetComponent<Tilemap>();
            crateObject.GetComponent<TilemapRenderer>().sortingOrder = 18;

            var sprite = GetSampleSprite();
            var crateTile = ScriptableObject.CreateInstance<Tile>();
            crateTile.hideFlags = HideFlags.DontSave;
            crateTile.sprite = sprite;
            crateTile.color = new Color(0.62f, 0.34f, 0.14f);
            crateMap.SetTile(new Vector3Int(2, 2, 0), crateTile);
            board.ConfigureCrates(crateMap);
        }

        private void CreateSampleBombCrates()
        {
            var board = battle.Board;
            var crateObject = new GameObject(
                "Bomb Crates", typeof(Tilemap), typeof(TilemapRenderer));
            crateObject.transform.SetParent(board.Grid.transform, false);
            var crateMap = crateObject.GetComponent<Tilemap>();
            crateObject.GetComponent<TilemapRenderer>().sortingOrder = 19;

            var sprite = GetSampleSprite();
            var crateTile = ScriptableObject.CreateInstance<Tile>();
            crateTile.hideFlags = HideFlags.DontSave;
            crateTile.sprite = sprite;
            crateTile.color = new Color(0.9f, 0.12f, 0.05f);
            crateMap.SetTile(new Vector3Int(6, 1, 0), crateTile);
            board.ConfigureBombCrates(crateMap);
        }

        private void CreateSampleObjectives()
        {
            var board = battle.Board;
            var objectiveObject = new GameObject(
                "Attack Objectives", typeof(Tilemap), typeof(TilemapRenderer));
            objectiveObject.transform.SetParent(board.Grid.transform, false);
            var objectiveMap = objectiveObject.GetComponent<Tilemap>();
            objectiveObject.GetComponent<TilemapRenderer>().sortingOrder = 20;

            var sprite = GetSampleSprite();
            var objectiveTile = ScriptableObject.CreateInstance<Tile>();
            objectiveTile.hideFlags = HideFlags.DontSave;
            objectiveTile.sprite = sprite;
            objectiveTile.color = new Color(1f, 0.82f, 0.12f);
            objectiveMap.SetTile(new Vector3Int(8, 1, 0), objectiveTile);
            board.ConfigureObjectives(objectiveMap, board.ObjectiveMaxHealth);
        }

        private Sprite GetSampleSprite()
        {
            if (battle == null || battle.PlayerUnits.Count == 0)
                return null;
            var unit = battle.PlayerUnits[0];
            if (unit == null)
                return null;
            var renderer = unit.GetComponent<SpriteRenderer>();
            return renderer != null ? renderer.sprite : null;
        }
        private void SpawnEnemy(string unitName, GridPosition position, Color color, UnitDefinition definition)
        {
            var sprite = GetSampleSprite();

            var enemyObject = new GameObject(unitName, typeof(SpriteRenderer), typeof(BoxCollider2D));
            enemyObject.transform.localScale = Vector3.one;
            var renderer = enemyObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 20;
            enemyObject.GetComponent<BoxCollider2D>().size = Vector2.one;
            var unit = enemyObject.AddComponent<TacticalUnit>();
            unit.Configure(definition, UnitTeam.Enemy);
            unit.EnsureClickableBody();
            battle.RegisterEnemy(unit, position);
        }

        private void SpawnPlayer(string unitName, Vector3 stagingPosition,
            Color color, UnitDefinition definition)
        {
            var sprite = GetSampleSprite();

            var playerObject = new GameObject(unitName, typeof(SpriteRenderer), typeof(BoxCollider2D));
            playerObject.transform.position = stagingPosition;
            playerObject.transform.localScale = Vector3.one;
            var renderer = playerObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 20;
            playerObject.GetComponent<BoxCollider2D>().size = Vector2.one;
            var unit = playerObject.AddComponent<TacticalUnit>();
            unit.Configure(definition, UnitTeam.Player);
            unit.EnsureClickableBody();
            battle.RegisterPlayer(unit);
        }

        private static UnitDefinition LoadDefinition(string assetName)
        {
            var definition = Resources.Load<UnitDefinition>($"UnitDefinitions/{assetName}");
            if (definition == null)
                Debug.LogError($"UnitDefinition asset is missing: UnitDefinitions/{assetName}");
            return definition;
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 17,
                normal = { textColor = Color.white }
            };

            var phase = battle != null ? battle.Phase.ToString() : "Loading";
            GUI.Box(new Rect(16, 16, 680, 100),
                $"?�태: {phase}\n?�동 �???MOVE CONFIRM / ATK ???�????ATK CONFIRM\n?�상 ?�하 ?�해??체력�??�시, Move Reset ?�동 취소, Space ??종료", style);

            if (battle == null) return;
            var y = 124f;
            foreach (var unit in battle.PlayerUnits)
                DrawHealth(unit, ref y);
            foreach (var unit in battle.EnemyUnits)
                DrawHealth(unit, ref y);
        }

        private void DrawHealth(TacticalUnit unit, ref float y)
        {
            if (unit == null) return;
            var label = unit.Definition != null ? unit.Definition.DisplayName : unit.name;
            GUI.Box(new Rect(16, y, 320, 28),
                $"{label}  HP {unit.CurrentHealth}/{unit.MaxHealth}  ?�동 {unit.RemainingMovement}/{unit.MovementPoints}", style);
            y += 30f;
        }
    }
}

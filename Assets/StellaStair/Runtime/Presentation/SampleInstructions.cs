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
            if (battle != null && battle.Board.LadderTilemap == null)
                CreateSampleLadder();
            if (battle != null && battle.Board.WoodTilemap == null)
                CreateSampleWood();
            if (battle != null && battle.Board.CrateTilemap == null)
                CreateSampleCrates();
            if (battle != null && battle.Board.BombCrateTilemap == null)
                CreateSampleBombCrates();
            if (battle != null && battle.Board.ObjectiveTilemap == null)
                CreateSampleObjectives();
            if (battle != null && battle.Board.DefenseObjectiveTilemap == null)
                CreateSampleDefenseObjectives();
            var knight = LoadDefinition("Knight");
            var archer = LoadDefinition("Archer");
            var wizard = LoadDefinition("Wizard");
            var enemyGuard = LoadDefinition("EnemyGuard");
            var enemySoldier = LoadDefinition("EnemySoldier");
            if (battle != null && battle.PlayerUnits.Count > 0)
                battle.PlayerUnits[0].Configure(knight, UnitTeam.Player);
            if (battle != null && battle.PlayerUnits.Count > 1)
                battle.PlayerUnits[1].Configure(archer, UnitTeam.Player);
            if (battle != null && battle.PlayerUnits.Count < 3)
                SpawnPlayer("Player Wizard", new Vector3(-2f, 2.5f),
                    new Color(0.7f, 0.35f, 1f), wizard);
            if (battle != null && battle.EnemyUnits.Count == 0)
            {
                SpawnEnemy("Enemy Guard", new GridPosition(4, 1), new Color(0.95f, 0.25f, 0.22f),
                    enemyGuard);
                SpawnEnemy("Enemy Soldier", new GridPosition(7, 0), new Color(0.75f, 0.15f, 0.2f),
                    enemySoldier);
            }
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

            Sprite sprite = null;
            if (battle.PlayerUnits.Count > 0 && battle.PlayerUnits[0] != null)
                sprite = battle.PlayerUnits[0].GetComponent<SpriteRenderer>().sprite;
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

            Sprite sprite = null;
            if (battle.PlayerUnits.Count > 0 && battle.PlayerUnits[0] != null)
                sprite = battle.PlayerUnits[0].GetComponent<SpriteRenderer>().sprite;
            var woodTile = ScriptableObject.CreateInstance<Tile>();
            woodTile.hideFlags = HideFlags.DontSave;
            woodTile.sprite = sprite;
            woodTile.color = new Color(0.55f, 0.3f, 0.12f);
            woodMap.SetTile(new Vector3Int(0, 1, 0), woodTile);
            woodMap.SetTile(new Vector3Int(5, 1, 0), woodTile);
            board.ConfigureWood(woodMap, 2);
        }

        private void CreateSampleCrates()
        {
            var board = battle.Board;
            var crateObject = new GameObject("Crates", typeof(Tilemap), typeof(TilemapRenderer));
            crateObject.transform.SetParent(board.Grid.transform, false);
            var crateMap = crateObject.GetComponent<Tilemap>();
            crateObject.GetComponent<TilemapRenderer>().sortingOrder = 18;

            Sprite sprite = null;
            if (battle.PlayerUnits.Count > 0 && battle.PlayerUnits[0] != null)
                sprite = battle.PlayerUnits[0].GetComponent<SpriteRenderer>().sprite;
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

            Sprite sprite = null;
            if (battle.PlayerUnits.Count > 0 && battle.PlayerUnits[0] != null)
                sprite = battle.PlayerUnits[0].GetComponent<SpriteRenderer>().sprite;
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

            Sprite sprite = null;
            if (battle.PlayerUnits.Count > 0 && battle.PlayerUnits[0] != null)
                sprite = battle.PlayerUnits[0].GetComponent<SpriteRenderer>().sprite;
            var objectiveTile = ScriptableObject.CreateInstance<Tile>();
            objectiveTile.hideFlags = HideFlags.DontSave;
            objectiveTile.sprite = sprite;
            objectiveTile.color = new Color(1f, 0.82f, 0.12f);
            objectiveMap.SetTile(new Vector3Int(8, 1, 0), objectiveTile);
            board.ConfigureObjectives(objectiveMap, 8);
        }

        private void CreateSampleDefenseObjectives()
        {
            var board = battle.Board;
            var objectiveObject = new GameObject(
                "Defense Objectives", typeof(Tilemap), typeof(TilemapRenderer));
            objectiveObject.transform.SetParent(board.Grid.transform, false);
            var objectiveMap = objectiveObject.GetComponent<Tilemap>();
            objectiveObject.GetComponent<TilemapRenderer>().sortingOrder = 20;

            Sprite sprite = null;
            if (battle.PlayerUnits.Count > 0 && battle.PlayerUnits[0] != null)
                sprite = battle.PlayerUnits[0].GetComponent<SpriteRenderer>().sprite;
            var objectiveTile = ScriptableObject.CreateInstance<Tile>();
            objectiveTile.hideFlags = HideFlags.DontSave;
            objectiveTile.sprite = sprite;
            objectiveTile.color = new Color(0.2f, 0.95f, 1f);
            objectiveMap.SetTile(new Vector3Int(-1, 1, 0), objectiveTile);
            board.ConfigureDefenseObjectives(objectiveMap, 12);
        }

        private void SpawnEnemy(string unitName, GridPosition position, Color color, UnitDefinition definition)
        {
            Sprite sprite = null;
            if (battle.PlayerUnits.Count > 0 && battle.PlayerUnits[0] != null)
                sprite = battle.PlayerUnits[0].GetComponent<SpriteRenderer>().sprite;

            var enemyObject = new GameObject(unitName, typeof(SpriteRenderer), typeof(BoxCollider2D));
            enemyObject.transform.localScale = new Vector3(0.75f, 1.25f, 1f);
            var renderer = enemyObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 20;
            enemyObject.GetComponent<BoxCollider2D>().size = Vector2.one;
            var unit = enemyObject.AddComponent<TacticalUnit>();
            unit.Configure(definition, UnitTeam.Enemy);
            battle.RegisterEnemy(unit, position);
        }

        private void SpawnPlayer(string unitName, Vector3 stagingPosition,
            Color color, UnitDefinition definition)
        {
            Sprite sprite = null;
            if (battle.PlayerUnits.Count > 0 && battle.PlayerUnits[0] != null)
                sprite = battle.PlayerUnits[0].GetComponent<SpriteRenderer>().sprite;

            var playerObject = new GameObject(unitName, typeof(SpriteRenderer), typeof(BoxCollider2D));
            playerObject.transform.position = stagingPosition;
            playerObject.transform.localScale = new Vector3(0.75f, 1.25f, 1f);
            var renderer = playerObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 20;
            playerObject.GetComponent<BoxCollider2D>().size = Vector2.one;
            var unit = playerObject.AddComponent<TacticalUnit>();
            unit.Configure(definition, UnitTeam.Player);
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
                $"상태: {phase}\n이동 칸 → MOVE CONFIRM / ATK → 대상 → ATK CONFIRM\n예상 낙하 피해는 체력바 표시, Move Reset 이동 취소, Space 턴 종료", style);

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
                $"{label}  HP {unit.CurrentHealth}/{unit.MaxHealth}  이동 {unit.RemainingMovement}/{unit.MovementPoints}", style);
            y += 30f;
        }
    }
}

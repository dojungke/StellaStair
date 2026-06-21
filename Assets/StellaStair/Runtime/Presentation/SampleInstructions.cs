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

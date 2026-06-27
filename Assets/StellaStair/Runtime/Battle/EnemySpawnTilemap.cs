using StellaStair.Grid;
using StellaStair.Units;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StellaStair.Battle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
    public sealed class EnemySpawnTilemap : MonoBehaviour
    {
        [SerializeField] private UnitDefinition definition;
        [SerializeField] private Color enemyColor = new(0.9f, 0.18f, 0.12f);
        [SerializeField] private Vector2 unitScale = new(0.75f, 1.25f);

        private Tilemap spawnTilemap;

        public void Configure(UnitDefinition unitDefinition, Color color)
        {
            definition = unitDefinition;
            enemyColor = color;
            if (Application.isPlaying)
                SpawnEnemies();
        }

        private void Awake()
        {
            spawnTilemap = GetComponent<Tilemap>();
            SpawnEnemies();
        }

        private void SpawnEnemies()
        {
            spawnTilemap ??= GetComponent<Tilemap>();
            var battle = FindAnyObjectByType<DeploymentManager>();
            if (battle == null || battle.Board == null || spawnTilemap == null)
                return;

            foreach (var cell in spawnTilemap.cellBounds.allPositionsWithin)
            {
                if (!spawnTilemap.HasTile(cell))
                    continue;

                var position = new GridPosition(cell.x, cell.y - 1);
                if (!battle.Board.CanEnter(position))
                    continue;

                var enemyObject = new GameObject(
                    $"{(definition != null ? definition.DisplayName : "Enemy")} {cell.x},{cell.y}",
                    typeof(SpriteRenderer), typeof(BoxCollider2D));
                enemyObject.transform.localScale = new Vector3(unitScale.x, unitScale.y, 1f);
                var renderer = enemyObject.GetComponent<SpriteRenderer>();
                renderer.sprite = spawnTilemap.GetSprite(cell);
                renderer.color = enemyColor;
                renderer.sortingOrder = 20;
                enemyObject.GetComponent<BoxCollider2D>().size = Vector2.one;
                var enemy = enemyObject.AddComponent<TacticalUnit>();
                enemy.Configure(definition, UnitTeam.Enemy);
                if (battle.RegisterEnemy(enemy, position))
                    spawnTilemap.SetTile(cell, null);
                else
                    Destroy(enemyObject);
            }
        }
    }
}

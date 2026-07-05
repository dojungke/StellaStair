using System.Collections.Generic;
using StellaStair.Grid;
using UnityEngine;

namespace StellaStair.Presentation
{
    public sealed class GridHighlighter : MonoBehaviour
    {
        [SerializeField] private Color reachableColor = new(0.15f, 0.75f, 1f, 0.35f);
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.15f, 0.6f);
        [SerializeField] private Color attackColor = new(1f, 0.2f, 0.15f, 0.55f);
        [SerializeField] private Color impactColor = new(1f, 0.55f, 0.05f, 0.9f);
        [SerializeField] private Color knockbackColor = new(1f, 0.9f, 0.1f, 0.9f);
        [SerializeField] private Color collisionColor = new(0.85f, 0.1f, 1f, 0.95f);
        [SerializeField] private Color voidColor = new(0.08f, 0.02f, 0.02f, 0.95f);
        [SerializeField] private int sortingOrder = 10;
        private readonly List<GameObject> markers = new();
        private Sprite markerSprite;

        public void Show(TacticalBoard board, IEnumerable<GridPosition> positions, GridPosition? selected = null)
        {
            Clear();
            EnsureSprite();
            foreach (var position in positions)
            {
                if (selected.HasValue && position == selected.Value)
                    continue;
                CreateMarker(board, position, reachableColor);
            }
        }

        public void Show(TacticalBoard board, IEnumerable<GridPosition> positions,
            IEnumerable<GridPosition> attackPositions, GridPosition selected)
        {
            Show(board, positions, selected);
            foreach (var position in attackPositions)
                CreateMarker(board, position, attackColor);
        }

        public void ShowAttackPreview(TacticalBoard board, GridPosition selected,
            GridPosition impact, IEnumerable<GridPosition> knockbackDestinations,
            IEnumerable<GridPosition> collisionPositions, IEnumerable<GridPosition> voidPositions,
            bool targetTerrain = false, int terrainPreviewDamage = 0,
            IEnumerable<GridPosition> reachablePositions = null,
            IEnumerable<GridPosition> attackablePositions = null)
        {
            Clear();
            EnsureSprite();

            if (reachablePositions != null)
            {
                var fadedReachableColor = reachableColor;
                fadedReachableColor.a *= 0.65f;
                foreach (var position in reachablePositions)
                {
                    if (position == selected)
                        continue;
                    CreateMarker(board, position, fadedReachableColor);
                }
            }

            if (attackablePositions != null)
            {
                var fadedAttackColor = attackColor;
                fadedAttackColor.a *= 0.55f;
                foreach (var position in attackablePositions)
                {
                    if (position == impact)
                        continue;
                    CreateMarker(board, position, fadedAttackColor);
                }
            }

            if (targetTerrain)
            {
                CreateMarkerAtWorld(board.PositionToWorld(impact), board, impact,
                    impactColor, 0.72f);
                CreateWoodHealthBar(board, impact, terrainPreviewDamage);
            }
            else
                CreateMarker(board, impact, impactColor, 0.72f);

            foreach (var position in knockbackDestinations)
                CreateMarker(board, position, knockbackColor, 0.68f);
            foreach (var position in collisionPositions)
                CreateCrossMarker(board, position, collisionColor);
            foreach (var position in voidPositions)
                CreateCrossMarker(board, position, voidColor);
        }

        public void ShowMovePreview(TacticalBoard board, IEnumerable<GridPosition> reachablePositions,
            GridPosition selected, GridPosition destination)
        {
            Clear();
            EnsureSprite();
            var fadedReachableColor = reachableColor;
            fadedReachableColor.a *= 0.75f;
            foreach (var position in reachablePositions)
            {
                if (position == selected || position == destination)
                    continue;
                CreateMarker(board, position, fadedReachableColor);
            }
            CreateMarker(board, destination, knockbackColor);
        }

        public void ShowWoodHealth(TacticalBoard board, GridPosition position)
        {
            Clear();
            EnsureSprite();
            CreateMarkerAtWorld(
                board.PositionToWorld(position), board, position, selectedColor, 0.72f);
            CreateWoodHealthBar(board, position);
        }

        private void CreateWoodHealthBar(
            TacticalBoard board, GridPosition position, int previewDamage = 0)
        {
            var health = board.GetWoodHealth(position);
            if (health <= 0)
                return;

            var ratio = Mathf.Clamp01(health / (float)board.WoodMaxHealth);
            var damageRatio = Mathf.Min(Mathf.Max(0, previewDamage), health) /
                              (float)board.WoodMaxHealth;
            var remainingRatio = Mathf.Max(0f, ratio - damageRatio);
            var root = new GameObject($"Wood Health {position}");
            root.transform.SetParent(transform);
            root.transform.position = board.PositionToWorld(position) +
                                      Vector3.up * (board.Grid.cellSize.y * 0.65f);

            var background = new GameObject("Background");
            background.transform.SetParent(root.transform, false);
            background.transform.localScale = new Vector3(0.86f, 0.14f, 1f);
            var backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = markerSprite;
            backgroundRenderer.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            backgroundRenderer.sortingOrder = sortingOrder + 20;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(root.transform, false);
            fill.transform.localScale = new Vector3(0.8f * remainingRatio, 0.09f, 1f);
            fill.transform.localPosition = new Vector3(
                -0.4f + 0.4f * remainingRatio, 0f, 0f);
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = markerSprite;
            fillRenderer.color = ratio > 0.5f
                ? new Color(0.2f, 0.9f, 0.25f, 1f)
                : new Color(0.95f, 0.15f, 0.12f, 1f);
            fillRenderer.sortingOrder = sortingOrder + 21;

            if (damageRatio > 0f)
            {
                var damage = new GameObject("Predicted Damage");
                damage.transform.SetParent(root.transform, false);
                damage.transform.localScale = new Vector3(0.8f * damageRatio, 0.09f, 1f);
                damage.transform.localPosition = new Vector3(
                    -0.4f + 0.8f * (remainingRatio + damageRatio * 0.5f), 0f, 0f);
                var damageRenderer = damage.AddComponent<SpriteRenderer>();
                damageRenderer.sprite = markerSprite;
                damageRenderer.color = new Color(1f, 0.08f, 0.05f, 0.95f);
                damageRenderer.sortingOrder = sortingOrder + 22;
            }
            markers.Add(root);
        }

        public void ShowEnemyIntentPreview(
            TacticalBoard board,
            GridPosition selected,
            GridPosition moveDestination,
            bool willMove,
            GridPosition attackTarget,
            bool willAttack,
            IEnumerable<GridPosition> reachablePositions,
            IEnumerable<GridPosition> attackablePositions,
            IEnumerable<GridPosition> knockbackDestinations,
            IEnumerable<GridPosition> collisionPositions,
            IEnumerable<GridPosition> voidPositions)
        {
            Clear();
            EnsureSprite();

            var fadedReachableColor = reachableColor;
            fadedReachableColor.a *= 0.5f;
            foreach (var position in reachablePositions)
            {
                if (position != selected)
                    CreateMarker(board, position, fadedReachableColor);
            }

            var fadedAttackColor = attackColor;
            fadedAttackColor.a *= 0.45f;
            foreach (var position in attackablePositions)
                CreateMarker(board, position, fadedAttackColor);

            CreateMarker(board, selected, selectedColor, 0.72f);
            if (willMove)
                CreateMarker(board, moveDestination, knockbackColor, 0.82f);
            if (willAttack)
                CreateMarker(board, attackTarget, impactColor, 0.72f);
            foreach (var position in knockbackDestinations)
                CreateMarker(board, position, knockbackColor, 0.68f);
            foreach (var position in collisionPositions)
                CreateCrossMarker(board, position, collisionColor);
            foreach (var position in voidPositions)
                CreateCrossMarker(board, position, voidColor);
        }

        public void Clear()
        {
            foreach (var marker in markers)
                if (marker != null) Destroy(marker);
            markers.Clear();
        }

        private void EnsureSprite()
        {
            if (markerSprite != null) return;
            var texture = Texture2D.whiteTexture;
            markerSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void CreateMarker(TacticalBoard board, GridPosition position, Color color, float scale = 0.92f)
        {
            CreateMarkerAtWorld(
                GetMarkerWorldPosition(board, position), board, position, color, scale);
        }

        private void CreateMovePreviewGhost(TacticalBoard board, GridPosition destination, TacticalUnit unit)
        {
            if (unit == null)
            {
                CreateMarker(board, destination, knockbackColor);
                return;
            }

            var ghost = unit.CreateMovePreviewGhost(0.45f, sortingOrder + 30);
            if (ghost == null)
            {
                CreateMarker(board, destination, knockbackColor);
                return;
            }

            ghost.transform.SetParent(transform);
            ghost.transform.position = unit.GetPreviewStandingWorldPosition(destination);
            markers.Add(ghost);
        }

        private void CreateMarkerAtWorld(
            Vector3 worldPosition, TacticalBoard board, GridPosition position,
            Color color, float scale)
        {
            var marker = new GameObject($"Highlight {position}");
            marker.transform.SetParent(transform);
            // Board positions represent solid floor cells. Draw the marker in the
            // standing cell directly above the floor instead of inside the tile.
            marker.transform.position = worldPosition;
            marker.transform.localScale = board.Grid.cellSize * scale;
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            markers.Add(marker);
        }

        private void CreateCrossMarker(TacticalBoard board, GridPosition position, Color color)
        {
            var marker = new GameObject($"Preview Cross {position}");
            marker.transform.SetParent(transform);
            marker.transform.position = GetMarkerWorldPosition(board, position);
            CreateCrossBar(marker.transform, color, 45f);
            CreateCrossBar(marker.transform, color, -45f);
            markers.Add(marker);
        }

        private static Vector3 GetMarkerWorldPosition(
            TacticalBoard board, GridPosition position)
        {
            return board.PositionToStandingWorld(position);
        }

        private void CreateCrossBar(Transform parent, Color color, float angle)
        {
            var bar = new GameObject("Bar");
            bar.transform.SetParent(parent, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            bar.transform.localScale = new Vector3(0.78f, 0.14f, 1f);
            var renderer = bar.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder + 4;
        }
    }
}


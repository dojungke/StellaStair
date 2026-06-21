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
            IEnumerable<GridPosition> collisionPositions, IEnumerable<GridPosition> voidPositions)
        {
            Clear();
            EnsureSprite();
            CreateMarker(board, impact, impactColor, 0.72f);

            foreach (var position in knockbackDestinations)
                CreateMarker(board, position, knockbackColor, 0.68f);
            foreach (var position in collisionPositions)
                CreateCrossMarker(board, position, collisionColor);
            foreach (var position in voidPositions)
                CreateCrossMarker(board, position, voidColor);
        }

        public void ShowMovePreview(TacticalBoard board, GridPosition destination)
        {
            Clear();
            EnsureSprite();
            CreateMarker(board, destination, knockbackColor);
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
            var marker = new GameObject($"Highlight {position}");
            marker.transform.SetParent(transform);
            // Board positions represent solid floor cells. Draw the marker in the
            // standing cell directly above the floor instead of inside the tile.
            marker.transform.position = board.PositionToStandingWorld(position);
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
            marker.transform.position = board.PositionToStandingWorld(position);
            CreateCrossBar(marker.transform, color, 45f);
            CreateCrossBar(marker.transform, color, -45f);
            markers.Add(marker);
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

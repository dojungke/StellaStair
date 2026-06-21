using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Grid;
using UnityEngine;

namespace StellaStair.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DeploymentManager))]
    public sealed class EnemyIntentPresenter : MonoBehaviour
    {
        [SerializeField] private Color warningColor = new(1f, 0.08f, 0.05f, 0.9f);
        [SerializeField] private int sortingOrder = 35;
        [SerializeField, Min(0.1f)] private float markerSize = 0.65f;

        private static Sprite sharedSprite;
        private readonly List<GameObject> markers = new();
        private readonly List<SpriteRenderer> renderers = new();
        private DeploymentManager battle;

        private void Awake()
        {
            battle = GetComponent<DeploymentManager>();
            EnsureSprite();
        }

        private void OnEnable()
        {
            battle.EnemyIntentsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            battle.EnemyIntentsChanged -= Refresh;
            Clear();
        }

        private void Update()
        {
            var alpha = Mathf.Lerp(0.35f, warningColor.a, (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f);
            var color = warningColor;
            color.a = alpha;
            foreach (var renderer in renderers)
                if (renderer != null) renderer.color = color;
        }

        private void Refresh()
        {
            Clear();
            if (battle == null || battle.Board == null)
                return;

            foreach (var intent in battle.EnemyIntents)
                if (intent.WillAttack)
                    CreateCross(intent.TargetPosition);
        }

        private void CreateCross(GridPosition position)
        {
            var marker = new GameObject($"Enemy Attack Warning {position}");
            marker.transform.position = battle.Board.PositionToStandingWorld(position);
            CreateBar(marker.transform, 45f);
            CreateBar(marker.transform, -45f);
            markers.Add(marker);
        }

        private void CreateBar(Transform parent, float rotation)
        {
            var bar = new GameObject("Warning Bar");
            bar.transform.SetParent(parent, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            bar.transform.localScale = new Vector3(markerSize, 0.12f, 1f);
            var renderer = bar.AddComponent<SpriteRenderer>();
            renderer.sprite = sharedSprite;
            renderer.color = warningColor;
            renderer.sortingOrder = sortingOrder;
            renderers.Add(renderer);
        }

        private static void EnsureSprite()
        {
            if (sharedSprite != null) return;
            sharedSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);
            sharedSprite.name = "Runtime Enemy Intent Sprite";
        }

        private void Clear()
        {
            foreach (var marker in markers)
                if (marker != null) Destroy(marker);
            markers.Clear();
            renderers.Clear();
        }
    }
}

using UnityEngine;

namespace StellaStair.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalUnit), typeof(Collider2D))]
    public sealed class UnitHealthBar : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float width = 0.8f;
        [SerializeField, Min(0.02f)] private float height = 0.1f;
        [SerializeField, Min(0f)] private float verticalOffset = 0.2f;
        [SerializeField] private Color backgroundColor = new(0.08f, 0.08f, 0.08f, 0.9f);
        [SerializeField] private Color healthyColor = new(0.2f, 0.9f, 0.25f, 1f);
        [SerializeField] private Color woundedColor = new(1f, 0.72f, 0.1f, 1f);
        [SerializeField] private Color criticalColor = new(0.95f, 0.15f, 0.12f, 1f);
        [SerializeField] private Color previewDamageColor = new(1f, 0.12f, 0.08f, 0.95f);
        [SerializeField] private int sortingOrder = 40;

        private static Sprite sharedSprite;
        private TacticalUnit unit;
        private Collider2D bodyCollider;
        private Transform barRoot;
        private SpriteRenderer fillRenderer;
        private SpriteRenderer previewDamageRenderer;
        private float ratio = 1f;
        private int previewDamage;

        private void Awake()
        {
            unit = GetComponent<TacticalUnit>();
            bodyCollider = GetComponent<Collider2D>();
            CreateBar();
        }

        private void OnEnable()
        {
            unit.HealthChanged += OnHealthChanged;
            unit.Died += OnDied;
            Refresh(unit.CurrentHealth, unit.MaxHealth);
        }

        private void OnDisable()
        {
            unit.HealthChanged -= OnHealthChanged;
            unit.Died -= OnDied;
        }

        private void LateUpdate()
        {
            if (barRoot == null || bodyCollider == null)
                return;
            var bounds = bodyCollider.bounds;
            barRoot.position = new Vector3(bounds.center.x, bounds.max.y + verticalOffset, transform.position.z);
        }

        private void CreateBar()
        {
            EnsureSprite();
            var rootObject = new GameObject($"{name} Health Bar");
            barRoot = rootObject.transform;

            var background = new GameObject("Background");
            background.transform.SetParent(barRoot, false);
            var backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = sharedSprite;
            backgroundRenderer.color = backgroundColor;
            backgroundRenderer.sortingOrder = sortingOrder;
            background.transform.localScale = new Vector3(width + 0.06f, height + 0.06f, 1f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(barRoot, false);
            fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = sharedSprite;
            fillRenderer.sortingOrder = sortingOrder + 1;

            var preview = new GameObject("Predicted Damage");
            preview.transform.SetParent(barRoot, false);
            previewDamageRenderer = preview.AddComponent<SpriteRenderer>();
            previewDamageRenderer.sprite = sharedSprite;
            previewDamageRenderer.color = previewDamageColor;
            previewDamageRenderer.sortingOrder = sortingOrder + 2;
            ApplyVisual();
        }

        private static void EnsureSprite()
        {
            if (sharedSprite != null) return;
            sharedSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);
            sharedSprite.name = "Runtime Health Bar Sprite";
        }

        private void OnHealthChanged(TacticalUnit changedUnit, int current, int maximum) => Refresh(current, maximum);

        private void Refresh(int current, int maximum)
        {
            ratio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
            ApplyVisual();
        }

        public void SetPreviewDamage(int damage)
        {
            previewDamage = Mathf.Max(0, damage);
            ApplyVisual();
        }

        public void SetVisible(bool visible)
        {
            if (barRoot != null)
                barRoot.gameObject.SetActive(visible && unit.IsAlive);
        }

        private void ApplyVisual()
        {
            if (fillRenderer == null) return;
            fillRenderer.color = ratio > 0.6f ? healthyColor : ratio > 0.3f ? woundedColor : criticalColor;
            fillRenderer.transform.localScale = new Vector3(width * ratio, height, 1f);
            fillRenderer.transform.localPosition = new Vector3(-width * (1f - ratio) * 0.5f, 0f, 0f);

            if (previewDamageRenderer == null) return;
            var maximum = Mathf.Max(1, unit.MaxHealth);
            var damageRatio = Mathf.Min(previewDamage, unit.CurrentHealth) / (float)maximum;
            var predictedRatio = Mathf.Max(0f, ratio - damageRatio);
            previewDamageRenderer.enabled = damageRatio > 0f;
            previewDamageRenderer.transform.localScale = new Vector3(width * damageRatio, height, 1f);
            previewDamageRenderer.transform.localPosition = new Vector3(
                -width * 0.5f + width * (predictedRatio + damageRatio * 0.5f), 0f, 0f);
        }

        private void OnDied(TacticalUnit deadUnit)
        {
            if (barRoot != null)
                barRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (barRoot != null)
                Destroy(barRoot.gameObject);
        }
    }
}

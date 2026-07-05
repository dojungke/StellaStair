using UnityEngine;

namespace StellaStair.Units
{
    public enum AttackDistanceRule
    {
        Any,
        DistantOnly
    }

    [CreateAssetMenu(menuName = "Stella Stair/Unit Definition", fileName = "UnitDefinition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; } = "Unit";
        [field: SerializeField, Min(1)] public int MovementPoints { get; private set; } = 5;
        [field: SerializeField, Min(0.1f)] public float MoveSpeed { get; private set; } = 5f;
        [field: SerializeField, Min(1)] public int MaxHealth { get; private set; } = 10;
        [field: SerializeField, Min(1)] public int AttackDamage { get; private set; } = 3;
        [field: SerializeField] public AttackDistanceRule AttackDistanceRule { get; private set; } =
            AttackDistanceRule.Any;
        [field: SerializeField, Min(0)] public int MinimumAttackRange { get; private set; } = 0;
        [field: SerializeField, Min(1)] public int AttackRange { get; private set; } = 1;
        [field: SerializeField, Min(0)] public int VerticalAttackRange { get; private set; } = 1;
        [field: SerializeField, Min(0)] public int KnockbackDistance { get; private set; } = 0;
        [field: SerializeField] public bool CanPierceUnits { get; private set; } = false;
        [field: SerializeField, Min(0)] public int AreaKnockbackRadius { get; private set; } = 0;
        [field: SerializeField, Min(0)] public int AreaKnockbackDistance { get; private set; } = 0;
        [field: SerializeField] public Sprite UnitSprite { get; private set; }
        [field: SerializeField] public GameObject AnimationPrefab { get; private set; }
        [field: SerializeField] public RuntimeAnimatorController AnimationController { get; private set; }
        [field: SerializeField] public Vector3 AnimationLocalOffset { get; private set; } = Vector3.zero;
        [field: SerializeField] public Vector3 AnimationLocalScale { get; private set; } = Vector3.one;
        [field: SerializeField, Min(0.1f), Tooltip("Unit visual width in tile cells. 1 = one tile wide, 2 = two tiles wide. Aspect ratio is preserved.")]
        public float UnitWidthInCells { get; private set; } = 1f;
        [field: SerializeField, Min(0.1f), Tooltip("Unit collider/selection height in tile cells. 1 = one tile tall.")]
        public float UnitHeightInCells { get; private set; } = 1f;
        [field: SerializeField] public int DefaultFacingDirection { get; private set; } = 1;

        public void Configure(string displayName, int movementPoints, float moveSpeed,
            int maxHealth, int attackDamage, int attackRange, int verticalAttackRange = 1,
            int knockbackDistance = 0, bool canPierceUnits = false,
            int areaKnockbackRadius = 0, int areaKnockbackDistance = 0,
            int minimumAttackRange = 0,
            AttackDistanceRule attackDistanceRule = AttackDistanceRule.Any)
        {
            DisplayName = displayName;
            MovementPoints = Mathf.Max(1, movementPoints);
            MoveSpeed = Mathf.Max(0.1f, moveSpeed);
            MaxHealth = Mathf.Max(1, maxHealth);
            AttackDamage = Mathf.Max(1, attackDamage);
            AttackRange = Mathf.Max(1, attackRange);
            AttackDistanceRule = attackDistanceRule;
            MinimumAttackRange = Mathf.Clamp(minimumAttackRange, 0, AttackRange);
            VerticalAttackRange = Mathf.Max(0, verticalAttackRange);
            KnockbackDistance = Mathf.Max(0, knockbackDistance);
            CanPierceUnits = canPierceUnits;
            AreaKnockbackRadius = Mathf.Max(0, areaKnockbackRadius);
            AreaKnockbackDistance = Mathf.Max(0, areaKnockbackDistance);
        }
    }
}

using UnityEngine;

namespace StellaStair.Units
{
    [CreateAssetMenu(menuName = "Stella Stair/Unit Definition", fileName = "UnitDefinition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; } = "Unit";
        [field: SerializeField, Min(1)] public int MovementPoints { get; private set; } = 5;
        [field: SerializeField, Min(0.1f)] public float MoveSpeed { get; private set; } = 5f;
        [field: SerializeField, Min(1)] public int MaxHealth { get; private set; } = 10;
        [field: SerializeField, Min(1)] public int AttackDamage { get; private set; } = 3;
        [field: SerializeField, Min(1)] public int AttackRange { get; private set; } = 1;
        [field: SerializeField, Min(0)] public int VerticalAttackRange { get; private set; } = 1;
        [field: SerializeField, Min(0)] public int KnockbackDistance { get; private set; } = 0;
        [field: SerializeField] public bool CanPierceUnits { get; private set; } = false;
        [field: SerializeField, Min(0)] public int AreaKnockbackRadius { get; private set; } = 0;
        [field: SerializeField, Min(0)] public int AreaKnockbackDistance { get; private set; } = 0;

        public void Configure(string displayName, int movementPoints, float moveSpeed,
            int maxHealth, int attackDamage, int attackRange, int verticalAttackRange = 1,
            int knockbackDistance = 0, bool canPierceUnits = false,
            int areaKnockbackRadius = 0, int areaKnockbackDistance = 0)
        {
            DisplayName = displayName;
            MovementPoints = Mathf.Max(1, movementPoints);
            MoveSpeed = Mathf.Max(0.1f, moveSpeed);
            MaxHealth = Mathf.Max(1, maxHealth);
            AttackDamage = Mathf.Max(1, attackDamage);
            AttackRange = Mathf.Max(1, attackRange);
            VerticalAttackRange = Mathf.Max(0, verticalAttackRange);
            KnockbackDistance = Mathf.Max(0, knockbackDistance);
            CanPierceUnits = canPierceUnits;
            AreaKnockbackRadius = Mathf.Max(0, areaKnockbackRadius);
            AreaKnockbackDistance = Mathf.Max(0, areaKnockbackDistance);
        }
    }
}

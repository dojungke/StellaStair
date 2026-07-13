using System;
using System.Collections.Generic;
using UnityEngine;

namespace StellaStair.Units
{
    public enum AttackDistanceRule
    {
        Any,
        DistantOnly
    }

    [Serializable]
    public struct GridOffset
    {
        public int x;
        public int y;

        public GridOffset(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [Serializable]
    public sealed class AttackModeRangeDefinition
    {
        [SerializeField] private string attackMode = "Default";
        [SerializeField] private List<GridOffset> targetOffsets = new();
        [SerializeField] private List<GridOffset> effectOffsets = new();

        public string AttackMode => attackMode;
        public IReadOnlyList<GridOffset> TargetOffsets => targetOffsets;
        public IReadOnlyList<GridOffset> EffectOffsets => effectOffsets;
        public bool HasTargetOffsets => targetOffsets != null && targetOffsets.Count > 0;
        public bool HasEffectOffsets => effectOffsets != null && effectOffsets.Count > 0;

        public void Configure(string mode, IEnumerable<GridOffset> targets, IEnumerable<GridOffset> effects)
        {
            attackMode = string.IsNullOrWhiteSpace(mode) ? "Default" : mode.Trim();
            targetOffsets ??= new List<GridOffset>();
            effectOffsets ??= new List<GridOffset>();
            targetOffsets.Clear();
            effectOffsets.Clear();
            if (targets != null)
                targetOffsets.AddRange(targets);
            if (effects != null)
                effectOffsets.AddRange(effects);
        }
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
        [SerializeField] private List<GridOffset> attackTargetOffsets = new();
        [SerializeField] private List<GridOffset> attackEffectOffsets = new();
        [SerializeField] private List<AttackModeRangeDefinition> attackModeRanges = new();
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

        public IReadOnlyList<GridOffset> AttackTargetOffsets => attackTargetOffsets;
        public IReadOnlyList<GridOffset> AttackEffectOffsets => attackEffectOffsets;
        public IReadOnlyList<AttackModeRangeDefinition> AttackModeRanges => attackModeRanges;
        public bool HasCustomAttackTargetOffsets => attackTargetOffsets != null && attackTargetOffsets.Count > 0;
        public bool HasCustomAttackEffectOffsets => attackEffectOffsets != null && attackEffectOffsets.Count > 0;

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

        public void ClearAttackOffsetDefinitions()
        {
            attackTargetOffsets ??= new List<GridOffset>();
            attackEffectOffsets ??= new List<GridOffset>();
            attackModeRanges ??= new List<AttackModeRangeDefinition>();
            attackTargetOffsets.Clear();
            attackEffectOffsets.Clear();
            attackModeRanges.Clear();
        }
        public void ConfigureAttackOffsets(
            IEnumerable<GridOffset> targetOffsets,
            IEnumerable<GridOffset> effectOffsets)
        {
            ConfigureAttackOffsets("Default", targetOffsets, effectOffsets);
        }

        public void ConfigureAttackOffsets(
            string attackMode,
            IEnumerable<GridOffset> targetOffsets,
            IEnumerable<GridOffset> effectOffsets)
        {
            var normalizedMode = NormalizeAttackMode(attackMode);
            if (string.Equals(normalizedMode, "Default", StringComparison.OrdinalIgnoreCase))
            {
                attackTargetOffsets ??= new List<GridOffset>();
                attackEffectOffsets ??= new List<GridOffset>();
                attackTargetOffsets.Clear();
                attackEffectOffsets.Clear();
                if (targetOffsets != null)
                    attackTargetOffsets.AddRange(targetOffsets);
                if (effectOffsets != null)
                    attackEffectOffsets.AddRange(effectOffsets);
                return;
            }

            attackModeRanges ??= new List<AttackModeRangeDefinition>();
            var range = attackModeRanges.Find(candidate =>
                candidate != null && string.Equals(candidate.AttackMode, normalizedMode, StringComparison.OrdinalIgnoreCase));
            if (range == null)
            {
                range = new AttackModeRangeDefinition();
                attackModeRanges.Add(range);
            }
            range.Configure(normalizedMode, targetOffsets, effectOffsets);
        }

        public bool TryGetAttackModeRange(
            string attackMode, out IReadOnlyList<GridOffset> targetOffsets,
            out IReadOnlyList<GridOffset> effectOffsets)
        {
            targetOffsets = null;
            effectOffsets = null;
            var normalizedMode = NormalizeAttackMode(attackMode);
            if (string.Equals(normalizedMode, "Default", StringComparison.OrdinalIgnoreCase))
            {
                targetOffsets = attackTargetOffsets;
                effectOffsets = attackEffectOffsets;
                return HasCustomAttackTargetOffsets || HasCustomAttackEffectOffsets;
            }

            if (attackModeRanges == null)
                return false;
            foreach (var range in attackModeRanges)
            {
                if (range == null || !string.Equals(range.AttackMode, normalizedMode, StringComparison.OrdinalIgnoreCase))
                    continue;
                targetOffsets = range.TargetOffsets;
                effectOffsets = range.EffectOffsets;
                return range.HasTargetOffsets || range.HasEffectOffsets;
            }
            return false;
        }

        private static string NormalizeAttackMode(string attackMode)
        {
            return string.IsNullOrWhiteSpace(attackMode) ? "Default" : attackMode.Trim();
        }
    }
}
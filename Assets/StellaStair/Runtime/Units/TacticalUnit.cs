using System;
using System.Collections;
using System.Collections.Generic;
using StellaStair.Grid;
using UnityEngine;

namespace StellaStair.Units
{
    public enum UnitTeam { Player, Enemy, Neutral }

    [RequireComponent(typeof(Collider2D))]
    public sealed class TacticalUnit : MonoBehaviour
    {
        [SerializeField] private UnitDefinition definition;
        [SerializeField] private UnitTeam team;
        [SerializeField] private bool isCrate;
        [SerializeField] private bool isObjective;
        [SerializeField] private bool isExplosiveCrate;
        [SerializeField, Min(1)] private int crateMaxHealth = 2;
        [SerializeField, Min(1)] private int explosionDamage = 3;
        [SerializeField, Min(1)] private int fallbackMovementPoints = 5;
        [SerializeField, Min(0.1f)] private float fallbackMoveSpeed = 5f;
        [SerializeField, Min(1)] private int fallbackMaxHealth = 10;
        [SerializeField, Min(1)] private int currentLevel = 1;
        [SerializeField, Min(0)] private int currentExperience;
        [SerializeField, Min(1)] private int experienceToNextLevel = 3;
        [SerializeField, Min(1)] private int fallbackAttackDamage = 3;
        [SerializeField, Min(0)] private int bonusMaxHealth;
        [SerializeField, Min(0)] private int bonusAttackDamage;
        [SerializeField, Min(0)] private int bonusMovementPoints;
        [SerializeField, Min(0)] private int healthUpgradeCount;
        [SerializeField, Min(0)] private int attackUpgradeCount;
        [SerializeField, Min(0)] private int movementUpgradeCount;
        [SerializeField] private bool hasThrustAttack;
        [SerializeField] private TacticalAttackMode currentAttackMode = TacticalAttackMode.Default;
        [SerializeField, Min(0)] private int thrustFrontDamageBonus;
        [SerializeField, Min(0)] private int thrustBackDamageBonus;
        [SerializeField] private bool thrustHasKnockback;
        [SerializeField] private bool hasGuardianPassive;
        [SerializeField] private bool hasCouragePassive;
        [SerializeField] private bool hasPiercingArrowAttack;
        [SerializeField, Min(0)] private int piercingArrowDamageBonus;
        [SerializeField] private bool hasBowStrikeAttack;
        [SerializeField, Min(0)] private int bowStrikeDamageBonus;
        [SerializeField] private bool hasHarpoonAttack;
        [SerializeField] private bool hasAgilityPassive;
        [SerializeField] private bool agilityShieldAvailable;
        [SerializeField] private bool hasCoverPassive;
        [SerializeField] private bool coverUsedThisTurn;
        [SerializeField] private bool hasFireballAttack;
        [SerializeField, Min(0)] private int fireballDamageBonus;
        [SerializeField, Min(0)] private int fireballCooldownReduction;
        [SerializeField, Min(0)] private int fireballCooldownRemaining;
        [SerializeField] private bool hasIceSpikeAttack;
        [SerializeField, Min(0)] private int iceSpikeCooldownReduction;
        [SerializeField, Min(0)] private int iceSpikeCooldownRemaining;
        [SerializeField] private bool hasNatureFragranceAttack;
        [SerializeField, Min(0)] private int natureFragranceCooldownReduction;
        [SerializeField, Min(0)] private int natureFragranceHealBonus;
        [SerializeField, Min(0)] private int natureFragranceCooldownRemaining;
        [SerializeField] private bool hasArcaneAccelerationPassive;
        [SerializeField, Min(0)] private int arcaneAccelerationTurnCounter;
        [SerializeField] private bool arcaneAccelerationReady;
        [SerializeField] private bool arcaneAccelerationConsumedThisTurn;
        private string currentDamageSkillKey = string.Empty;
        private string lastAttackSkillKey = string.Empty;
        [SerializeField] private AttackDistanceRule fallbackAttackDistanceRule = AttackDistanceRule.Any;
        [SerializeField, Min(0)] private int fallbackMinimumAttackRange = 0;
        [SerializeField, Min(1)] private int fallbackAttackRange = 1;
        [SerializeField, Min(0)] private int fallbackVerticalAttackRange = 1;
        [SerializeField, Min(0)] private int fallbackKnockbackDistance = 0;
        [SerializeField] private bool fallbackCanPierceUnits = false;
        [SerializeField, Min(0)] private int fallbackAreaKnockbackRadius = 0;
        [SerializeField, Min(0)] private int fallbackAreaKnockbackDistance = 0;
        [SerializeField, Min(0f)] private float ladderEntryPause = 0.18f;
        [SerializeField, Min(0.05f)] private float collisionImpactDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float fallbackUnitWidthInCells = 1f;
        [SerializeField, Min(0.1f)] private float fallbackUnitHeightInCells = 1f;
        [SerializeField] private Transform animationRoot;
        [SerializeField] private bool hideSpriteRendererWhenAnimationPrefabExists = true;
        [SerializeField] private bool autoAlignAnimationToCollider = true;

        private TacticalBoard board;
        private BoxCollider2D bodyCollider;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer previewRenderer;
        private SpriteRenderer unitSpriteRenderer;
        private Coroutine attackPreviewBlinkRoutine;
        private Coroutine selectionHighlightRoutine;
        private Coroutine levelUpEffectRoutine;
        private Color previewOriginalColor;
        private Color selectionOriginalColor;
        private UnitHealthBar healthBar;
        private TacticalUnitAnimator unitAnimator;
        private GameObject animationPrefabInstance;
        private GameObject animationPrefabSource;
        private Sprite definitionSpriteSource;
        private readonly Dictionary<Transform, ShadowDirectionState> shadowDirectionStates = new();
        private static Sprite fallbackBodySprite;
        private bool isImpactReserved;
        private TacticalUnit reservedImpactTarget;
        public UnitDefinition Definition => definition;
        public UnitTeam Team => team;
        public bool IsCrate => isCrate;
        public bool IsObjective => isObjective;
        public bool IsExplosiveCrate => isExplosiveCrate;
        public GridPosition Position { get; private set; }
        public GridPosition TurnStartPosition { get; private set; }
        public bool IsPlaced { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsAttacking { get; private set; }
        public bool IsResolvingForcedMovement => IsMoving || isImpactReserved;
        public bool IsAlive => CurrentHealth > 0;
        public bool HasMoved => RemainingMovement < MovementPoints;
        public bool CanUndoMovement => IsAlive && IsPlaced && !IsMoving && !IsAttacking &&
            !HasAttacked && HasMoved && board != null && board.CanEnter(TurnStartPosition, this);
        public bool HasAttacked { get; private set; }
        public int CurrentHealth { get; private set; }
        public int CurrentLevel => currentLevel;
        public int CurrentExperience => currentExperience;
        public int ExperienceToNextLevel => experienceToNextLevel;
        public TacticalUnit LastDamageSource { get; private set; }
        public string LastDamageSkillKey { get; private set; } = string.Empty;
        public string ProgressKey => definition != null ? definition.name : name;
        public int RemainingMovement { get; private set; }
        public int MovementPoints => (definition != null ? definition.MovementPoints : fallbackMovementPoints) + bonusMovementPoints;
        public int MaxHealth => isCrate
            ? crateMaxHealth
            : (definition != null ? definition.MaxHealth : fallbackMaxHealth) + bonusMaxHealth;
        public int AttackDamage => (definition != null ? definition.AttackDamage : fallbackAttackDamage) + bonusAttackDamage;
        public int BasicAttackDamage => AttackDamage;
        public bool IsWizardDefaultAttackMode() => IsWizardUnit() && CurrentAttackMode == TacticalAttackMode.Default;
        public AttackDistanceRule AttackDistanceRule => definition != null
            ? definition.AttackDistanceRule
            : fallbackAttackDistanceRule;
        public int MinimumAttackRange => definition != null
            ? definition.MinimumAttackRange
            : fallbackMinimumAttackRange;
        public int AttackRange => definition != null ? definition.AttackRange : fallbackAttackRange;
        public int VerticalAttackRange => definition != null
            ? definition.VerticalAttackRange
            : fallbackVerticalAttackRange;
        public int KnockbackDistance => definition != null
            ? definition.KnockbackDistance
            : fallbackKnockbackDistance;
        public bool CanPierceUnits => definition != null
            ? definition.CanPierceUnits
            : fallbackCanPierceUnits;
        public int AreaKnockbackRadius => definition != null
            ? definition.AreaKnockbackRadius
            : fallbackAreaKnockbackRadius;
        public int AreaKnockbackDistance => definition != null
            ? definition.AreaKnockbackDistance
            : fallbackAreaKnockbackDistance;
public int ThrustFrontDamage => hasThrustAttack ? 1 + thrustFrontDamageBonus : 0;
        public int ThrustBackDamage => hasThrustAttack ? 1 + thrustBackDamageBonus : 0;
        public bool ThrustHasKnockback => hasThrustAttack && thrustHasKnockback;
        public int PiercingArrowDamage => hasPiercingArrowAttack ? 1 + piercingArrowDamageBonus : 0;
        public int BowStrikeDamage => hasBowStrikeAttack ? 1 + bowStrikeDamageBonus : 0;
        public int HarpoonDamage => hasHarpoonAttack ? 1 : 0;
        public int FireballDamage => hasFireballAttack ? 1 + fireballDamageBonus : 0;
        public int FireballCooldown => Mathf.Max(1, 2 - fireballCooldownReduction);
        public int IceSpikeDamage => hasIceSpikeAttack ? 1 : 0;
        public int IceSpikeCooldown => Mathf.Max(1, 3 - iceSpikeCooldownReduction);
        public int NatureFragranceHealAmount => hasNatureFragranceAttack ? 1 + natureFragranceHealBonus : 0;
        public int NatureFragranceCooldown => Mathf.Max(1, 2 - natureFragranceCooldownReduction);
        public bool HasMultipleAttackModes => GetAvailableAttackModeCount() > 1;
        public TacticalAttackMode SelectedAttackMode =>
            IsAttackModeUnlocked(currentAttackMode) ? currentAttackMode : TacticalAttackMode.Default;
        public TacticalAttackMode CurrentAttackMode => IsAttackModeAvailable(currentAttackMode) ? currentAttackMode : TacticalAttackMode.Default;
        public bool IsCurrentAttackModeOnCooldown =>
            IsAttackModeOnCooldown(SelectedAttackMode);
        public bool CanUseCurrentAttackMode => !IsCurrentAttackModeOnCooldown;
        public string CurrentAttackName => IsCurrentAttackModeOnCooldown
            ? $"{GetAttackName(SelectedAttackMode)} (\uCFE8\uD0C0\uC784 \uC911)"
            : GetAttackName(SelectedAttackMode);
        public bool HasCustomCurrentAttackModeEffectOffsets => HasCurrentAttackModeEffectOffsets(out _);
        public bool HasCustomAttackTargetOffsets => definition != null && definition.HasCustomAttackTargetOffsets;
        public bool HasCustomAttackEffectOffsets => definition != null && definition.HasCustomAttackEffectOffsets;
        public float UnitWidthInCells => definition != null
            ? definition.UnitWidthInCells
            : fallbackUnitWidthInCells;
        public float UnitHeightInCells => definition != null
            ? definition.UnitHeightInCells
            : fallbackUnitHeightInCells;
        public int DefaultFacingDirection => definition != null && definition.DefaultFacingDirection < 0
            ? -1
            : 1;
        public int FacingDirection { get; private set; } = 1;
        public event Action<TacticalUnit> MoveStarted;
        public event Action<TacticalUnit> MoveCompleted;
        public event Action<TacticalUnit, int, int> HealthChanged;
        public event Action<TacticalUnit, TacticalUnit, int> Healed;
        public event Action<TacticalUnit, string> AttackUsed;
        public event Func<TacticalUnit, string, GridPosition, IEnumerator> BeforeAttack;
        public event Action<TacticalUnit, int, int> ExperienceChanged;
        public event Action<TacticalUnit, int> LeveledUp;
        public event Action<TacticalUnit> Died;

        public enum TacticalAttackMode
        {
            Default,
            Thrust,
            PiercingArrow,
            BowStrike,
            Harpoon,
            Fireball,
            IceSpike,
            NatureFragrance
        }
        public enum LevelUpUpgradeType
        {
            MaxHealthPlusOne,
            AttackDamagePlusOne,
            MovementPlusOne,
            UnlockThrust,
            ThrustFrontDamagePlusOne,
            ThrustBackDamagePlusOne,
            ThrustKnockback,
            GuardianPassive,
            CouragePassive,
            UnlockPiercingArrow,
            PiercingArrowDamagePlusOne,
            UnlockBowStrike,
            BowStrikeDamagePlusOne,
            UnlockHarpoon,
            AgilityPassive,
            CoverPassive,
            UnlockFireball,
            FireballDamagePlusOne,
            FireballCooldownMinusOne,
            UnlockIceSpike,
            IceSpikeCooldownMinusOne,
            UnlockNatureFragrance,
            NatureFragranceCooldownMinusOne,
            NatureFragranceHealPlusOne,
            ArcaneAccelerationPassive
        }

        public readonly struct LevelUpUpgradeOption
        {
            public LevelUpUpgradeOption(LevelUpUpgradeType type, string title, string description)
            {
                Type = type;
                Title = title;
                Description = description;
            }

            public LevelUpUpgradeType Type { get; }
            public string Title { get; }
            public string Description { get; }
        }

        public struct UnitProgressSnapshot
        {
            public int CurrentLevel;
            public int CurrentExperience;
            public int ExperienceToNextLevel;
            public int BonusMaxHealth;
            public int BonusAttackDamage;
            public int BonusMovementPoints;
            public int HealthUpgradeCount;
            public int AttackUpgradeCount;
            public int MovementUpgradeCount;
            public bool HasThrustAttack;
            public int ThrustFrontDamageBonus;
            public int ThrustBackDamageBonus;
            public bool ThrustHasKnockback;
            public bool HasGuardianPassive;
            public bool HasCouragePassive;
            public bool HasPiercingArrowAttack;
            public int PiercingArrowDamageBonus;
            public bool HasBowStrikeAttack;
            public int BowStrikeDamageBonus;
            public bool HasHarpoonAttack;
            public bool HasAgilityPassive;
            public bool HasCoverPassive;
            public bool HasFireballAttack;
            public int FireballDamageBonus;
            public int FireballCooldownReduction;
            public int FireballCooldownRemaining;
            public bool HasIceSpikeAttack;
            public int IceSpikeCooldownReduction;
            public int IceSpikeCooldownRemaining;
            public bool HasNatureFragranceAttack;
            public int NatureFragranceCooldownReduction;
            public int NatureFragranceHealBonus;
            public int NatureFragranceCooldownRemaining;
            public bool HasArcaneAccelerationPassive;
            public int ArcaneAccelerationTurnCounter;
            public bool ArcaneAccelerationReady;
        }

        private void Awake()
        {
            // Ensure a minimum click area even if Unity serialized a tiny collider before a sprite was assigned.
            EnsureBodyComponents();
            ApplyUnitBodySize();
            CurrentHealth = MaxHealth;
            RemainingMovement = MovementPoints;
            healthBar = GetComponent<UnitHealthBar>();
            if (healthBar == null)
                healthBar = gameObject.AddComponent<UnitHealthBar>();
            RefreshAnimationRig();
        }

        private void EnsureBodyComponents()
        {
            bodyCollider = GetComponent<BoxCollider2D>();
            if (bodyCollider == null)
                bodyCollider = gameObject.AddComponent<BoxCollider2D>();

            bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer == null)
                bodyRenderer = gameObject.AddComponent<SpriteRenderer>();

            if (bodyRenderer.sprite == null)
                bodyRenderer.sprite = GetFallbackBodySprite();

            previewRenderer = bodyRenderer;
        }

        private static Sprite GetFallbackBodySprite()
        {
            if (fallbackBodySprite != null)
                return fallbackBodySprite;

            fallbackBodySprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f);
            fallbackBodySprite.name = "Fallback Unit Body";
            return fallbackBodySprite;
        }
        public void EnsureClickableBody()
        {
            EnsureBodyComponents();
            ApplyUnitBodySize();
            if (bodyCollider != null)
                bodyCollider.enabled = true;
            if (bodyRenderer != null && bodyRenderer.sprite == null)
                bodyRenderer.sprite = GetFallbackBodySprite();
        }
        public void Configure(UnitDefinition unitDefinition, UnitTeam unitTeam)
        {
            isCrate = false;
            isObjective = false;
            isExplosiveCrate = false;
            EnsureBodyComponents();
            definition = unitDefinition;
            team = unitTeam;
            if (!IsPlaced)
            {
                CurrentHealth = MaxHealth;
                RemainingMovement = MovementPoints;
            }
            ApplyUnitBodySize();
            RefreshAnimationRig();
        }

        private void RefreshAnimationRig()
        {
            var prefab = definition != null ? definition.AnimationPrefab : null;
            if (prefab == null)
            {
                if (animationPrefabInstance != null)
                    Destroy(animationPrefabInstance);
                animationPrefabInstance = null;
                animationPrefabSource = null;
                unitAnimator = GetComponentInChildren<TacticalUnitAnimator>();
                ApplyDefinitionSprite();
                ApplyUnitBodySize();
                if (previewRenderer != null)
                    previewRenderer.enabled = true;
                unitAnimator?.ApplyController(definition != null ? definition.AnimationController : null);
                unitAnimator?.Bind(this);
                SetFacingDirection(FacingDirection);
                return;
            }

            if (unitSpriteRenderer != null)
                unitSpriteRenderer.enabled = false;
            if (bodyRenderer != null)
            {
                previewRenderer = bodyRenderer;
                bodyRenderer.enabled = true;
            }

            if (animationPrefabSource != prefab)
            {
                if (animationPrefabInstance != null)
                    Destroy(animationPrefabInstance);

                var parent = animationRoot != null ? animationRoot : transform;
                animationPrefabInstance = Instantiate(prefab, parent);
                animationPrefabInstance.name = prefab.name;
                animationPrefabInstance.transform.localRotation = Quaternion.identity;
                animationPrefabSource = prefab;
            }

            ApplyAnimationLocalScale();
            ApplyAnimationLocalPosition();

            unitAnimator = animationPrefabInstance != null
                ? animationPrefabInstance.GetComponentInChildren<TacticalUnitAnimator>()
                : GetComponentInChildren<TacticalUnitAnimator>();

            if (unitAnimator == null && animationPrefabInstance != null)
                unitAnimator = animationPrefabInstance.AddComponent<TacticalUnitAnimator>();

            if (previewRenderer != null)
                previewRenderer.enabled = !hideSpriteRendererWhenAnimationPrefabExists;

            unitAnimator?.ApplyController(definition != null ? definition.AnimationController : null);
            unitAnimator?.Bind(this);
            SetFacingDirection(FacingDirection);
        }

        private void ApplyAnimationLocalScale()
        {
            if (animationPrefabInstance == null || definition == null)
                return;

            animationPrefabInstance.transform.localScale = Vector3.one;
            if (!TryGetAnimationRendererBounds(out var bounds) ||
                bounds.size.x <= 0.0001f || bounds.size.y <= 0.0001f)
            {
                animationPrefabInstance.transform.localScale = GetParentCompensatedScale(1f);
                return;
            }

            var targetSize = GetUnitWorldSize();
            var fitScale = targetSize.x / bounds.size.x;
            fitScale *= GetAnimationScaleMultiplier();
            animationPrefabInstance.transform.localScale = GetParentCompensatedScale(fitScale);
        }

        private float GetAnimationScaleMultiplier()
        {
            if (definition == null)
                return 1f;
            var scale = definition.AnimationLocalScale;
            if (!Mathf.Approximately(scale.x, 0f))
                return Mathf.Abs(scale.x);
            if (!Mathf.Approximately(scale.y, 0f))
                return Mathf.Abs(scale.y);
            return 1f;
        }

        private Vector3 GetParentCompensatedScale(float uniformScale)
        {
            var parent = animationPrefabInstance != null
                ? animationPrefabInstance.transform.parent
                : null;
            var parentScale = parent != null ? parent.lossyScale : Vector3.one;
            return new Vector3(
                SafeDivide(uniformScale, Mathf.Abs(parentScale.x)),
                SafeDivide(uniformScale, Mathf.Abs(parentScale.y)),
                SafeDivide(1f, Mathf.Abs(parentScale.z)));
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
        }

        private bool TryGetAnimationRendererBounds(out Bounds bounds)
        {
            bounds = default;
            if (animationPrefabInstance == null)
                return false;
            var renderers = animationPrefabInstance.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return false;
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private void ApplyUnitBodySize()
        {
            if (bodyCollider == null)
                return;
            var size = GetUnitWorldSize();
            bodyCollider.size = size;
            bodyCollider.offset = new Vector2(0f, size.y * 0.5f - 0.5f);

            if (previewRenderer == null || (definition != null && definition.AnimationPrefab != null))
                return;

            if (definition != null && definition.UnitSprite != null)
            {
                previewRenderer.drawMode = SpriteDrawMode.Simple;
                FitUnitSpriteRenderer(size);
                return;
            }

            if (CanResizeSpriteRenderer(previewRenderer.sprite))
            {
                previewRenderer.drawMode = SpriteDrawMode.Sliced;
                previewRenderer.size = size;
            }
        }
        private void ApplyDefinitionSprite()
        {
            var sprite = definition != null ? definition.UnitSprite : null;
            if (sprite == null)
            {
                if (bodyRenderer != null)
                {
                    if (bodyRenderer.sprite == null)
                        bodyRenderer.sprite = GetFallbackBodySprite();
                    bodyRenderer.enabled = true;
                    previewRenderer = bodyRenderer;
                }
                return;
            }

            var renderer = GetOrCreateUnitSpriteRenderer();
            if (renderer == null)
                return;

            if (bodyRenderer != null)
                bodyRenderer.enabled = false;

            previewRenderer = renderer;
            definitionSpriteSource = sprite;
            previewRenderer.sprite = sprite;
            previewRenderer.color = Color.white;
            previewRenderer.drawMode = SpriteDrawMode.Simple;
            previewRenderer.enabled = true;
        }

        private SpriteRenderer GetOrCreateUnitSpriteRenderer()
        {
            if (unitSpriteRenderer != null)
                return unitSpriteRenderer;

            var visualObject = new GameObject("Unit Sprite Visual");
            visualObject.transform.SetParent(transform, false);
            unitSpriteRenderer = visualObject.AddComponent<SpriteRenderer>();
            if (bodyRenderer != null)
            {
                unitSpriteRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                unitSpriteRenderer.sortingOrder = bodyRenderer.sortingOrder;
            }
            return unitSpriteRenderer;
        }

        private void FitUnitSpriteRenderer(Vector2 targetSize)
        {
            if (previewRenderer == null || previewRenderer.sprite == null || bodyCollider == null)
                return;

            previewRenderer.transform.localScale = GetSpriteFitScale(previewRenderer.sprite, targetSize);
            previewRenderer.transform.localRotation = Quaternion.identity;
            AlignUnitSpriteRendererToBody();
        }

        private void AlignUnitSpriteRendererToBody()
        {
            if (previewRenderer == null || previewRenderer != unitSpriteRenderer ||
                previewRenderer.sprite == null || bodyCollider == null)
                return;

            var spriteBounds = previewRenderer.sprite.bounds;
            var visualScale = previewRenderer.transform.localScale;
            var centerX = previewRenderer.flipX ? -spriteBounds.center.x : spriteBounds.center.x;
            var minY = spriteBounds.min.y;
            var colliderBottom = bodyCollider.offset.y - bodyCollider.size.y * 0.5f;

            previewRenderer.transform.localPosition = new Vector3(
                bodyCollider.offset.x - centerX * visualScale.x,
                colliderBottom - minY * visualScale.y,
                0f);
        }

        private static Vector3 GetSpriteFitScale(Sprite sprite, Vector2 targetSize)
        {
            if (sprite == null || sprite.bounds.size.x <= 0.0001f || sprite.bounds.size.y <= 0.0001f)
                return Vector3.one;

            var fitScale = targetSize.x / sprite.bounds.size.x;
            return new Vector3(fitScale, fitScale, 1f);
        }
        private static bool CanResizeSpriteRenderer(Sprite sprite)
        {
            if (sprite == null)
                return true;
            return sprite.vertices == null || sprite.vertices.Length <= 4;
        }

        private Vector2 GetUnitWorldSize()
        {
            var cellSize = board != null && board.Grid != null
                ? board.Grid.cellSize
                : Vector3.one;
            return new Vector2(
                Mathf.Max(0.1f, UnitWidthInCells) * Mathf.Abs(cellSize.x),
                Mathf.Max(0.1f, UnitHeightInCells) * Mathf.Abs(cellSize.y));
        }

        public bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            if (animationPrefabInstance != null && TryGetAnimationRendererBounds(out bounds))
                return true;

            if (previewRenderer != null && previewRenderer.enabled && previewRenderer.sprite != null)
            {
                bounds = previewRenderer.bounds;
                return true;
            }
            return false;
        }

        public Vector3 GetPreviewStandingWorldPosition(GridPosition position)
        {
            return GetStandingWorldPosition(position);
        }

        private SpriteRenderer GetActiveSpriteRenderer()
        {
            if (unitSpriteRenderer != null && unitSpriteRenderer.enabled && unitSpriteRenderer.sprite != null)
                return unitSpriteRenderer;
            if (previewRenderer != null && previewRenderer.enabled && previewRenderer.sprite != null)
                return previewRenderer;
            if (bodyRenderer != null && bodyRenderer.enabled && bodyRenderer.sprite != null)
                return bodyRenderer;
            return previewRenderer != null ? previewRenderer : bodyRenderer;
        }
        public GameObject CreateMovePreviewGhost(float alpha = 0.45f, int sortingOrderOverride = 40)
        {
            if (bodyCollider == null)
                bodyCollider = GetComponent<BoxCollider2D>();
            ApplyUnitBodySize();

            var ghostRoot = new GameObject($"{name} Move Preview Ghost");
            ghostRoot.transform.localRotation = Quaternion.identity;

            var prefab = definition != null ? definition.AnimationPrefab : null;
            if (prefab != null)
            {
                var instance = Instantiate(prefab, ghostRoot.transform);
                instance.name = prefab.name;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                FitGhostAnimationInstance(instance.transform);
                var ghostAnimator = instance.GetComponentInChildren<TacticalUnitAnimator>();
                var visualDirection = FacingDirection * DefaultFacingDirection;
                ghostAnimator?.SetFacing(visualDirection);
                ApplyGhostAnimationLocalPosition(instance.transform);
                ghostAnimator?.CaptureCurrentLocalPose();
                ApplyGhostRendererStyle(ghostRoot, alpha, sortingOrderOverride);
                DisableGhostPhysics(ghostRoot);
                return ghostRoot;
            }

            var visualRenderer = GetActiveSpriteRenderer();
            var sprite = definition != null && definition.UnitSprite != null
                ? definition.UnitSprite
                : visualRenderer != null ? visualRenderer.sprite : null;
            if (sprite == null)
            {
                Destroy(ghostRoot);
                return null;
            }

            var visual = new GameObject("Ghost Sprite");
            visual.transform.SetParent(ghostRoot.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.flipX = visualRenderer != null
                ? visualRenderer.flipX
                : FacingDirection * DefaultFacingDirection < 0;
            if (visualRenderer != null)
            {
                renderer.sortingLayerID = visualRenderer.sortingLayerID;
                renderer.sortingOrder = sortingOrderOverride;
                var color = visualRenderer.color;
                color.a = alpha;
                renderer.color = color;
            }
            else
            {
                renderer.sortingOrder = sortingOrderOverride;
                renderer.color = new Color(1f, 1f, 1f, alpha);
            }
            FitGhostSpriteRenderer(renderer);
            return ghostRoot;
        }

        private void FitGhostSpriteRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null)
                return;

            var targetSize = GetUnitWorldSize();
            renderer.transform.localScale = GetSpriteFitScale(renderer.sprite, targetSize);
            renderer.transform.localRotation = Quaternion.identity;

            var spriteBounds = renderer.sprite.bounds;
            var visualScale = renderer.transform.localScale;
            var centerX = renderer.flipX ? -spriteBounds.center.x : spriteBounds.center.x;
            var minY = spriteBounds.min.y;
            var colliderOffset = bodyCollider != null ? bodyCollider.offset : Vector2.zero;
            var colliderSize = bodyCollider != null ? bodyCollider.size : targetSize;
            var colliderBottom = colliderOffset.y - colliderSize.y * 0.5f;

            renderer.transform.localPosition = new Vector3(
                colliderOffset.x - centerX * visualScale.x,
                colliderBottom - minY * visualScale.y,
                0f);
        }

        private void FitGhostAnimationInstance(Transform instance)
        {
            if (instance == null)
                return;

            instance.localScale = Vector3.one;
            var fitScale = GetAnimationScaleMultiplier();
            if (TryGetRendererBounds(instance, out var bounds) &&
                bounds.size.x > 0.0001f && bounds.size.y > 0.0001f)
            {
                fitScale *= GetUnitWorldSize().x / bounds.size.x;
            }
            instance.localScale = new Vector3(fitScale, fitScale, 1f);
        }

        private void ApplyGhostAnimationLocalPosition(Transform instance)
        {
            if (instance == null || definition == null)
                return;

            instance.localPosition = Vector3.zero;
            var localPosition = definition.AnimationLocalOffset;
            if (autoAlignAnimationToCollider && bodyCollider != null)
                localPosition += GetAnimationAutoAlignOffset(instance);
            instance.localPosition = localPosition;
        }

        private Vector3 GetAnimationAutoAlignOffset(Transform visualRoot)
        {
            if (visualRoot == null || bodyCollider == null)
                return Vector3.zero;

            if (!TryGetRendererBounds(visualRoot, out var bounds))
                return Vector3.zero;

            var parent = visualRoot.parent != null ? visualRoot.parent : transform;
            var visualCenterLocal = parent.InverseTransformPoint(bounds.center);
            var visualBottomLocal = parent.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            var colliderBottom = bodyCollider.offset.y - bodyCollider.size.y * 0.5f;
            return new Vector3(
                bodyCollider.offset.x - visualCenterLocal.x,
                colliderBottom - visualBottomLocal.y,
                0f);
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return false;

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private void ApplyGhostRendererStyle(GameObject root, float alpha, int sortingOrderOverride)
        {
            if (root == null)
                return;

            var renderers = root.GetComponentsInChildren<SpriteRenderer>();
            foreach (var renderer in renderers)
            {
                if (previewRenderer != null)
                    renderer.sortingLayerID = previewRenderer.sortingLayerID;
                renderer.sortingOrder = sortingOrderOverride;
                var color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }
        }

        private static void DisableGhostPhysics(GameObject root)
        {
            if (root == null)
                return;

            var colliders2D = root.GetComponentsInChildren<Collider2D>();
            foreach (var collider2D in colliders2D)
                collider2D.enabled = false;

            var colliders = root.GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
                collider.enabled = false;

            var rigidbodies2D = root.GetComponentsInChildren<Rigidbody2D>();
            foreach (var rigidbody2D in rigidbodies2D)
                rigidbody2D.simulated = false;
        }
        private void ApplyAnimationLocalPosition()
        {
            if (animationPrefabInstance == null || definition == null)
                return;

            animationPrefabInstance.transform.localPosition = Vector3.zero;
            var localPosition = definition.AnimationLocalOffset;
            if (autoAlignAnimationToCollider && bodyCollider != null)
                localPosition += GetAnimationAutoAlignOffset();
            animationPrefabInstance.transform.localPosition = localPosition;
            if (unitAnimator != null && unitAnimator.transform == animationPrefabInstance.transform)
                unitAnimator.CaptureCurrentLocalPose();
        }

        private Vector3 GetAnimationAutoAlignOffset()
        {
            var renderers = animationPrefabInstance.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return Vector3.zero;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var parent = animationPrefabInstance.transform.parent != null
                ? animationPrefabInstance.transform.parent
                : transform;
            var visualCenterLocal = parent.InverseTransformPoint(bounds.center);
            var visualBottomLocal = parent.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            var colliderBottom = bodyCollider.offset.y - bodyCollider.size.y * 0.5f;
            return new Vector3(
                bodyCollider.offset.x - visualCenterLocal.x,
                colliderBottom - visualBottomLocal.y,
                0f);
        }

        private void ApplySpriteFacing(int visualDirection)
        {
            var flip = visualDirection < 0;
            if (previewRenderer != null)
                previewRenderer.flipX = flip;
            if (unitSpriteRenderer != null)
                unitSpriteRenderer.flipX = flip;
            if (bodyRenderer != null && bodyRenderer != unitSpriteRenderer)
                bodyRenderer.flipX = flip;
            AlignUnitSpriteRendererToBody();
        }
        private void ApplyShadowFacing(int visualDirection)
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer == previewRenderer || !IsShadowRenderer(renderer))
                    continue;

                if (!shadowDirectionStates.TryGetValue(renderer.transform, out var state))
                {
                    state = new ShadowDirectionState(
                        renderer.transform.localPosition,
                        renderer.transform.localScale,
                        renderer.flipX);
                    shadowDirectionStates[renderer.transform] = state;
                }

                var isAnimationShadow = animationPrefabInstance != null &&
                    renderer.transform.IsChildOf(animationPrefabInstance.transform);
                if (isAnimationShadow)
                {
                    renderer.flipX = state.InitialFlipX;
                    renderer.transform.localPosition = state.LocalPosition;
                }
                else
                {
                    var isDefaultVisualDirection = visualDirection == DefaultFacingDirection;
                    renderer.flipX = state.InitialFlipX ^ !isDefaultVisualDirection;
                    renderer.transform.localPosition = new Vector3(
                        state.LocalPosition.x * (isDefaultVisualDirection ? 1f : -1f),
                        state.LocalPosition.y,
                        state.LocalPosition.z);
                }
                renderer.transform.localScale = new Vector3(
                    Mathf.Abs(state.LocalScale.x),
                    state.LocalScale.y,
                    state.LocalScale.z);
            }
        }

        private static bool IsShadowRenderer(SpriteRenderer renderer)
        {
            var current = renderer.transform;
            while (current != null)
            {
                if (current.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.name.IndexOf("shade", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private readonly struct ShadowDirectionState
        {
            public ShadowDirectionState(Vector3 localPosition, Vector3 localScale, bool initialFlipX)
            {
                LocalPosition = localPosition;
                LocalScale = localScale;
                InitialFlipX = initialFlipX;
            }

            public Vector3 LocalPosition { get; }
            public Vector3 LocalScale { get; }
            public bool InitialFlipX { get; }
        }

        private void SetFacingDirection(int direction)
        {
            var normalized = direction < 0 ? -1 : 1;
            FacingDirection = normalized;
            var visualDirection = normalized * DefaultFacingDirection;
            unitAnimator?.SetFacing(visualDirection);
            if (animationPrefabInstance != null)
                ApplyAnimationLocalPosition();
            ApplySpriteFacing(visualDirection);
            ApplyShadowFacing(visualDirection);
        }

        public void FaceToward(GridPosition targetPosition)
        {
            if (targetPosition.X == Position.X)
                return;
            SetFacingDirection(targetPosition.X > Position.X ? 1 : -1);
        }

        public void RestoreFacingDirection(int direction)
        {
            SetFacingDirection(direction);
        }

        public bool TryPlace(TacticalBoard targetBoard, GridPosition position, bool requirePlayerZone)
        {
            if (IsMoving || targetBoard == null || requirePlayerZone && !targetBoard.IsPlayerDeploymentCell(position))
                return false;
            if (!targetBoard.TryOccupy(this, position))
                return false;

            board = targetBoard;
            Position = position;
            IsPlaced = true;
            ApplyUnitBodySize();
            transform.position = GetStandingWorldPosition(position);
            return true;
        }

        public bool TrySwapPlacementWith(TacticalUnit other, bool requirePlayerZone)
        {
            if (other == null || other == this ||
                !IsAlive || !other.IsAlive ||
                !IsPlaced || !other.IsPlaced ||
                IsMoving || other.IsMoving ||
                IsAttacking || other.IsAttacking ||
                board == null || board != other.board)
                return false;

            if (requirePlayerZone &&
                (!board.IsPlayerDeploymentCell(Position) ||
                 !board.IsPlayerDeploymentCell(other.Position)))
                return false;

            var ownPosition = Position;
            var otherPosition = other.Position;
            if (!board.TrySwapOccupants(this, other))
                return false;

            Position = otherPosition;
            other.Position = ownPosition;
            transform.position = GetStandingWorldPosition(Position);
            other.transform.position = other.GetStandingWorldPosition(other.Position);
            return true;
        }

        public void PrepareUnoccupiedSpawnFall(
            TacticalBoard targetBoard, GridPosition impactPosition, Vector3 startWorldPosition)
        {
            board = targetBoard;
            Position = impactPosition;
            IsPlaced = true;
            ApplyUnitBodySize();
            transform.position = startWorldPosition;
        }

        public void BeginExternalForcedMovement()
        {
            if (IsMoving)
                return;
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
        }

        public void EndExternalForcedMovement()
        {
            if (!IsMoving)
                return;
            IsMoving = false;
            unitAnimator?.PlayIdle();
            MoveCompleted?.Invoke(this);
        }

        public bool TryMoveTo(GridPosition destination, bool allowLadders = true)
        {
            if (isCrate || !IsAlive || !IsPlaced || IsMoving || IsAttacking || isImpactReserved ||
                HasAttacked && Team == UnitTeam.Player ||
                RemainingMovement <= 0 || destination == Position)
                return false;
            if (!GridPathfinder.TryFindPath(board, Position, destination, RemainingMovement, this, out var path, allowLadders))
                return false;

            // Reserve before animation so two commands cannot claim the same destination.
            if (!board.TryOccupy(this, destination))
                return false;

            RemainingMovement = Mathf.Max(0, RemainingMovement - path.Count);
            if (destination.X != Position.X)
                SetFacingDirection(destination.X > Position.X ? 1 : -1);
            StartCoroutine(MoveRoutine(path, destination, CalculateMovementFallDamage(path)));
            return true;
        }

        private IEnumerator MoveRoutine(
            IReadOnlyList<GridPosition> path, GridPosition destination, int fallDamage = 0)
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var speed = definition != null ? definition.MoveSpeed : fallbackMoveSpeed;
            var previous = Position;
            var guardianTriggers = new HashSet<TacticalUnit>();

            foreach (var step in path)
            {
                var target = GetStandingWorldPosition(step);
                if (board.IsLadderConnection(previous, step))
                    yield return MoveLadderRoutine(previous, step, target, speed);
                else
                    yield return MoveStepRoutine(target, speed);
                ApplyGuardianPassivesForMovementStep(step, guardianTriggers);
                if (!IsAlive)
                    yield break;
                previous = step;
            }

            Position = destination;
            IsMoving = false;
            unitAnimator?.PlayIdle();
            MoveCompleted?.Invoke(this);
            if (fallDamage > 0)
                TakeDamage(fallDamage);
        }

        private void ApplyGuardianPassivesForMovementStep(
            GridPosition step, HashSet<TacticalUnit> triggeredGuardians)
        {
            if (board == null || triggeredGuardians == null || !IsAlive)
                return;

            foreach (var guardian in board.GetOccupantsInRange(step, 1))
            {
                if (guardian == null || guardian == this || !guardian.hasGuardianPassive ||
                    guardian.Team == Team || triggeredGuardians.Contains(guardian))
                    continue;

                triggeredGuardians.Add(guardian);
                TakeDamage(1, guardian);
                if (!IsAlive)
                    return;
            }
        }
        private IEnumerator MoveLadderRoutine(
            GridPosition from, GridPosition to, Vector3 target, float speed)
        {
            var start = transform.position;
            if (!board.TryGetLadderWorldX(from, to, out var ladderX))
                ladderX = start.x;
            var ladderEntry = new Vector3(ladderX, start.y, start.z);
            var ladderExit = new Vector3(ladderX, target.y, target.z);

            // Move to the actual ladder column before changing height, then step
            // sideways onto the destination platform.
            yield return MoveLadderSegment(start, ladderEntry, speed);
            if (target.y > start.y && ladderEntryPause > 0f)
                yield return new WaitForSeconds(ladderEntryPause);
            yield return MoveLadderSegment(ladderEntry, ladderExit, speed);
            yield return MoveLadderSegment(ladderExit, target, speed);
        }

        private IEnumerator MoveLadderSegment(Vector3 start, Vector3 target, float speed)
        {
            var horizontalDelta = target.x - start.x;
            if (Mathf.Abs(horizontalDelta) > 0.01f)
                SetFacingDirection(horizontalDelta > 0f ? 1 : -1);

            var distance = Vector3.Distance(start, target);
            if (distance < 0.001f)
                yield break;
            var duration = Mathf.Max(0.05f, distance / (speed * 0.75f));
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            transform.position = target;
        }

        public int CalculateMovementFallDamage(IReadOnlyList<GridPosition> path)
        {
            var damage = 0;
            var previous = Position;
            foreach (var step in path)
            {
                if (!board.IsLadderConnection(previous, step))
                {
                    var drop = previous.Y - step.Y;
                    damage += Mathf.Max(0, drop - 1);
                }
                previous = step;
            }
            return Mathf.Min(MaxHealth, damage);
        }

        public List<LevelUpUpgradeOption> GetLevelUpUpgradeChoices(int count = 3)
        {
            var candidates = new List<LevelUpUpgradeOption>();
            var isKnight = IsKnightUnit();
            var isArcher = IsArcherUnit();
            var isWizard = IsWizardUnit();
            if (!isKnight && !isArcher && !isWizard)
                return candidates;

            if (healthUpgradeCount < GetMaxHealthUpgradeCount())
                candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.MaxHealthPlusOne, "Max Health +1", "Increase max health and current health by 1."));
            if (attackUpgradeCount < 2)
                candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.AttackDamagePlusOne, "Basic Attack Damage +1", "Increase basic attack damage by 1."));
            if (movementUpgradeCount < GetMaxMovementUpgradeCount())
                candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.MovementPlusOne, "Move Range +1", "Increase movement range by 1."));

            if (isKnight)
            {
                if (!hasThrustAttack)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.UnlockThrust, "Unlock Thrust", "Attack 2 horizontal cells for 1 damage each."));
                if (hasThrustAttack && thrustFrontDamageBonus < 1)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.ThrustFrontDamagePlusOne, "Thrust Front Damage +1", "Increase first-cell thrust damage by 1."));
                if (hasThrustAttack && thrustBackDamageBonus < 1)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.ThrustBackDamagePlusOne, "Thrust Back Damage +1", "Increase second-cell thrust damage by 1."));
                if (hasThrustAttack && !thrustHasKnockback)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.ThrustKnockback, "Thrust Knockback", "Thrust pushes hit units by 1 cell."));
                if (!hasGuardianPassive)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.GuardianPassive, "Passive: Guardian", "Enemies moving through nearby cells take 1 damage."));
                if (!hasCouragePassive)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.CouragePassive, "Passive: Courage", "Recover 1 health when killing an enemy."));
            }
            else if (isArcher)
            {
                if (!hasPiercingArrowAttack)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.UnlockPiercingArrow, "Piercing Arrow", "Attack a 5-cell line and damage every unit on the path for 1."));
                if (hasPiercingArrowAttack && piercingArrowDamageBonus < 2)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.PiercingArrowDamagePlusOne, "Piercing Arrow Damage +1", "Increase piercing arrow damage by 1."));
                if (!hasBowStrikeAttack)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.UnlockBowStrike, "Bow Strike", "Hit an adjacent enemy and push it 2 cells."));
                if (hasBowStrikeAttack && bowStrikeDamageBonus < 1)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.BowStrikeDamagePlusOne, "Bow Strike Damage +1", "Increase bow strike damage by 1."));
                if (!hasHarpoonAttack)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.UnlockHarpoon, "Harpoon", "Deal 1 damage and pull a horizontal target 1 cell."));
                if (!hasAgilityPassive)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.AgilityPassive, "Passive: Agility", "Gain one damage immunity charge at battle start."));
                if (!hasCoverPassive)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.CoverPassive, "Passive: Cover Fire", "Once per turn, shoot an enemy for 1 before it attacks an ally in basic range."));
            }
            else if (isWizard)
            {
                if (!hasFireballAttack)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.UnlockFireball, "Fireball", "Deal fire damage in a 3x3 area. Cooldown 2 turns."));
                if (hasFireballAttack && fireballDamageBonus < 1)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.FireballDamagePlusOne, "Fireball Damage +1", "Increase fireball damage by 1."));
                if (hasFireballAttack && fireballCooldownReduction < 1)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.FireballCooldownMinusOne, "Fireball Cooldown -1", "Reduce fireball cooldown by 1 turn."));
                if (!hasIceSpikeAttack)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.UnlockIceSpike, "Ice Spike", "Deal 1 damage to a unit, or create a 1 HP ice tile on empty ground. Cooldown 3 turns."));
                if (hasIceSpikeAttack && iceSpikeCooldownReduction < 1)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.IceSpikeCooldownMinusOne, "Ice Spike Cooldown -1", "Reduce ice spike cooldown by 1 turn."));
                if (!hasNatureFragranceAttack)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.UnlockNatureFragrance, "Nature Fragrance", "Heal a unit by 1. Cooldown 2 turns."));
                if (hasNatureFragranceAttack && natureFragranceCooldownReduction < 1)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.NatureFragranceCooldownMinusOne, "Nature Fragrance Cooldown -1", "Reduce nature fragrance cooldown by 1 turn."));
                if (hasNatureFragranceAttack && natureFragranceHealBonus < 1)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.NatureFragranceHealPlusOne, "Nature Fragrance Heal +1", "Increase nature fragrance healing by 1."));
                if (!hasArcaneAccelerationPassive)
                    candidates.Add(new LevelUpUpgradeOption(LevelUpUpgradeType.ArcaneAccelerationPassive, "Passive: Arcane Acceleration", "Every third turn, the first attack does not end this unit's action."));
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var swapIndex = UnityEngine.Random.Range(i, candidates.Count);
                var temporary = candidates[i];
                candidates[i] = candidates[swapIndex];
                candidates[swapIndex] = temporary;
            }

            if (candidates.Count > count)
                candidates.RemoveRange(count, candidates.Count - count);
            return candidates;
        }

        public void ApplyLevelUpUpgrade(LevelUpUpgradeType type)
        {
            switch (type)
            {
                case LevelUpUpgradeType.MaxHealthPlusOne:
                    if (healthUpgradeCount >= GetMaxHealthUpgradeCount()) return;
                    healthUpgradeCount++;
                    bonusMaxHealth++;
                    CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + 1);
                    HealthChanged?.Invoke(this, CurrentHealth, MaxHealth);
                    break;
                case LevelUpUpgradeType.AttackDamagePlusOne:
                    if (attackUpgradeCount >= 2) return;
                    attackUpgradeCount++;
                    bonusAttackDamage++;
                    break;
                case LevelUpUpgradeType.MovementPlusOne:
                    if (movementUpgradeCount >= GetMaxMovementUpgradeCount()) return;
                    movementUpgradeCount++;
                    bonusMovementPoints++;
                    RemainingMovement++;
                    break;
                case LevelUpUpgradeType.UnlockThrust:
                    if (hasThrustAttack) return;
                    hasThrustAttack = true;
                    currentAttackMode = TacticalAttackMode.Thrust;
                    break;
                case LevelUpUpgradeType.ThrustFrontDamagePlusOne:
                    if (!hasThrustAttack || thrustFrontDamageBonus >= 1) return;
                    thrustFrontDamageBonus++;
                    break;
                case LevelUpUpgradeType.ThrustBackDamagePlusOne:
                    if (!hasThrustAttack || thrustBackDamageBonus >= 1) return;
                    thrustBackDamageBonus++;
                    break;
                case LevelUpUpgradeType.ThrustKnockback:
                    if (!hasThrustAttack || thrustHasKnockback) return;
                    thrustHasKnockback = true;
                    break;
                case LevelUpUpgradeType.GuardianPassive:
                    if (hasGuardianPassive) return;
                    hasGuardianPassive = true;
                    break;
                case LevelUpUpgradeType.CouragePassive:
                    if (hasCouragePassive) return;
                    hasCouragePassive = true;
                    break;
                case LevelUpUpgradeType.UnlockPiercingArrow:
                    if (hasPiercingArrowAttack) return;
                    hasPiercingArrowAttack = true;
                    currentAttackMode = TacticalAttackMode.PiercingArrow;
                    break;
                case LevelUpUpgradeType.PiercingArrowDamagePlusOne:
                    if (!hasPiercingArrowAttack || piercingArrowDamageBonus >= 2) return;
                    piercingArrowDamageBonus++;
                    break;
                case LevelUpUpgradeType.UnlockBowStrike:
                    if (hasBowStrikeAttack) return;
                    hasBowStrikeAttack = true;
                    currentAttackMode = TacticalAttackMode.BowStrike;
                    break;
                case LevelUpUpgradeType.BowStrikeDamagePlusOne:
                    if (!hasBowStrikeAttack || bowStrikeDamageBonus >= 1) return;
                    bowStrikeDamageBonus++;
                    break;
                case LevelUpUpgradeType.UnlockHarpoon:
                    if (hasHarpoonAttack) return;
                    hasHarpoonAttack = true;
                    currentAttackMode = TacticalAttackMode.Harpoon;
                    break;
                case LevelUpUpgradeType.AgilityPassive:
                    if (hasAgilityPassive) return;
                    hasAgilityPassive = true;
                    agilityShieldAvailable = true;
                    break;
                case LevelUpUpgradeType.CoverPassive:
                    if (hasCoverPassive) return;
                    hasCoverPassive = true;
                    coverUsedThisTurn = false;
                    break;
                case LevelUpUpgradeType.UnlockFireball:
                    if (hasFireballAttack) return;
                    hasFireballAttack = true;
                    fireballCooldownRemaining = 0;
                    currentAttackMode = TacticalAttackMode.Fireball;
                    break;
                case LevelUpUpgradeType.FireballDamagePlusOne:
                    if (!hasFireballAttack || fireballDamageBonus >= 1) return;
                    fireballDamageBonus++;
                    break;
                case LevelUpUpgradeType.FireballCooldownMinusOne:
                    if (!hasFireballAttack || fireballCooldownReduction >= 1) return;
                    fireballCooldownReduction++;
                    fireballCooldownRemaining = Mathf.Min(fireballCooldownRemaining, FireballCooldown);
                    break;
                case LevelUpUpgradeType.UnlockIceSpike:
                    if (hasIceSpikeAttack) return;
                    hasIceSpikeAttack = true;
                    iceSpikeCooldownRemaining = 0;
                    currentAttackMode = TacticalAttackMode.IceSpike;
                    break;
                case LevelUpUpgradeType.IceSpikeCooldownMinusOne:
                    if (!hasIceSpikeAttack || iceSpikeCooldownReduction >= 1) return;
                    iceSpikeCooldownReduction++;
                    iceSpikeCooldownRemaining = Mathf.Min(iceSpikeCooldownRemaining, IceSpikeCooldown);
                    break;
                case LevelUpUpgradeType.UnlockNatureFragrance:
                    if (hasNatureFragranceAttack) return;
                    hasNatureFragranceAttack = true;
                    natureFragranceCooldownRemaining = 0;
                    currentAttackMode = TacticalAttackMode.NatureFragrance;
                    break;
                case LevelUpUpgradeType.NatureFragranceCooldownMinusOne:
                    if (!hasNatureFragranceAttack || natureFragranceCooldownReduction >= 1) return;
                    natureFragranceCooldownReduction++;
                    natureFragranceCooldownRemaining = Mathf.Min(natureFragranceCooldownRemaining, NatureFragranceCooldown);
                    break;
                case LevelUpUpgradeType.NatureFragranceHealPlusOne:
                    if (!hasNatureFragranceAttack || natureFragranceHealBonus >= 1) return;
                    natureFragranceHealBonus++;
                    break;
                case LevelUpUpgradeType.ArcaneAccelerationPassive:
                    if (hasArcaneAccelerationPassive) return;
                    hasArcaneAccelerationPassive = true;
                    arcaneAccelerationTurnCounter = 0;
                    arcaneAccelerationReady = false;
                    arcaneAccelerationConsumedThisTurn = false;
                    break;
            }
        }
        public void TryTriggerCourageHeal()
        {
            if (!hasCouragePassive || !IsAlive || CurrentHealth >= MaxHealth)
                return;
            var previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + 1);
            HealthChanged?.Invoke(this, CurrentHealth, MaxHealth);
            Healed?.Invoke(this, this, CurrentHealth - previousHealth);
        }


        public UnitProgressSnapshot CaptureProgress()
        {
            return new UnitProgressSnapshot
            {
                CurrentLevel = currentLevel,
                CurrentExperience = currentExperience,
                ExperienceToNextLevel = experienceToNextLevel,
                BonusMaxHealth = bonusMaxHealth,
                BonusAttackDamage = bonusAttackDamage,
                BonusMovementPoints = bonusMovementPoints,
                HealthUpgradeCount = healthUpgradeCount,
                AttackUpgradeCount = attackUpgradeCount,
                MovementUpgradeCount = movementUpgradeCount,
                HasThrustAttack = hasThrustAttack,
                ThrustFrontDamageBonus = thrustFrontDamageBonus,
                ThrustBackDamageBonus = thrustBackDamageBonus,
ThrustHasKnockback = thrustHasKnockback,
                HasGuardianPassive = hasGuardianPassive,
                HasCouragePassive = hasCouragePassive,
                HasPiercingArrowAttack = hasPiercingArrowAttack,
                PiercingArrowDamageBonus = piercingArrowDamageBonus,
                HasBowStrikeAttack = hasBowStrikeAttack,
                BowStrikeDamageBonus = bowStrikeDamageBonus,
                HasHarpoonAttack = hasHarpoonAttack,
                HasAgilityPassive = hasAgilityPassive,
                HasCoverPassive = hasCoverPassive,
                HasFireballAttack = hasFireballAttack,
                FireballDamageBonus = fireballDamageBonus,
                FireballCooldownReduction = fireballCooldownReduction,
                FireballCooldownRemaining = fireballCooldownRemaining,
                HasIceSpikeAttack = hasIceSpikeAttack,
                IceSpikeCooldownReduction = iceSpikeCooldownReduction,
                IceSpikeCooldownRemaining = iceSpikeCooldownRemaining,
                HasNatureFragranceAttack = hasNatureFragranceAttack,
                NatureFragranceCooldownReduction = natureFragranceCooldownReduction,
                NatureFragranceHealBonus = natureFragranceHealBonus,
                NatureFragranceCooldownRemaining = natureFragranceCooldownRemaining,
                HasArcaneAccelerationPassive = hasArcaneAccelerationPassive,
                ArcaneAccelerationTurnCounter = arcaneAccelerationTurnCounter,
                ArcaneAccelerationReady = arcaneAccelerationReady
            };
        }

        public void ApplyProgress(UnitProgressSnapshot snapshot)
        {
            currentLevel = Mathf.Max(1, snapshot.CurrentLevel);
            currentExperience = Mathf.Max(0, snapshot.CurrentExperience);
            experienceToNextLevel = Mathf.Max(1, snapshot.ExperienceToNextLevel);
            bonusMaxHealth = Mathf.Max(0, snapshot.BonusMaxHealth);
            bonusAttackDamage = Mathf.Max(0, snapshot.BonusAttackDamage);
            bonusMovementPoints = Mathf.Max(0, snapshot.BonusMovementPoints);
            healthUpgradeCount = Mathf.Clamp(snapshot.HealthUpgradeCount, 0, GetMaxHealthUpgradeCount());
            attackUpgradeCount = Mathf.Clamp(snapshot.AttackUpgradeCount, 0, 2);
            movementUpgradeCount = Mathf.Clamp(snapshot.MovementUpgradeCount, 0, GetMaxMovementUpgradeCount());
            hasThrustAttack = snapshot.HasThrustAttack;
            thrustFrontDamageBonus = Mathf.Clamp(snapshot.ThrustFrontDamageBonus, 0, 1);
            thrustBackDamageBonus = Mathf.Clamp(snapshot.ThrustBackDamageBonus, 0, 1);
            thrustHasKnockback = snapshot.ThrustHasKnockback;
            hasGuardianPassive = snapshot.HasGuardianPassive;
            hasCouragePassive = snapshot.HasCouragePassive;
            hasPiercingArrowAttack = snapshot.HasPiercingArrowAttack;
            piercingArrowDamageBonus = Mathf.Clamp(snapshot.PiercingArrowDamageBonus, 0, 2);
            hasBowStrikeAttack = snapshot.HasBowStrikeAttack;
            bowStrikeDamageBonus = Mathf.Clamp(snapshot.BowStrikeDamageBonus, 0, 1);
            hasHarpoonAttack = snapshot.HasHarpoonAttack;
            hasAgilityPassive = snapshot.HasAgilityPassive;
            agilityShieldAvailable = hasAgilityPassive;
            hasCoverPassive = snapshot.HasCoverPassive;
            hasFireballAttack = snapshot.HasFireballAttack;
            fireballDamageBonus = Mathf.Clamp(snapshot.FireballDamageBonus, 0, 1);
            fireballCooldownReduction = Mathf.Clamp(snapshot.FireballCooldownReduction, 0, 1);
            fireballCooldownRemaining = Mathf.Clamp(snapshot.FireballCooldownRemaining, 0, FireballCooldown);
            hasIceSpikeAttack = snapshot.HasIceSpikeAttack;
            iceSpikeCooldownReduction = Mathf.Clamp(snapshot.IceSpikeCooldownReduction, 0, 1);
            iceSpikeCooldownRemaining = Mathf.Clamp(snapshot.IceSpikeCooldownRemaining, 0, IceSpikeCooldown);
            hasNatureFragranceAttack = snapshot.HasNatureFragranceAttack;
            natureFragranceCooldownReduction = Mathf.Clamp(snapshot.NatureFragranceCooldownReduction, 0, 1);
            natureFragranceHealBonus = Mathf.Clamp(snapshot.NatureFragranceHealBonus, 0, 1);
            natureFragranceCooldownRemaining = Mathf.Clamp(snapshot.NatureFragranceCooldownRemaining, 0, NatureFragranceCooldown);
            hasArcaneAccelerationPassive = snapshot.HasArcaneAccelerationPassive;
            arcaneAccelerationTurnCounter = Mathf.Max(0, snapshot.ArcaneAccelerationTurnCounter);
            arcaneAccelerationReady = snapshot.ArcaneAccelerationReady;
            coverUsedThisTurn = false;
            arcaneAccelerationConsumedThisTurn = false;
            currentAttackMode = IsAttackModeAvailable(currentAttackMode) ? currentAttackMode : TacticalAttackMode.Default;
            CurrentHealth = MaxHealth;
            RemainingMovement = MovementPoints;
            HealthChanged?.Invoke(this, CurrentHealth, MaxHealth);
            ExperienceChanged?.Invoke(this, currentExperience, experienceToNextLevel);
        }

        private bool IsKnightUnit()
        {
            var definitionName = definition != null ? definition.DisplayName : string.Empty;
            return ContainsUnitName("Knight", definitionName) || ContainsUnitName("\uAE30\uC0AC", definitionName);
        }

        private bool IsArcherUnit()
        {
            var definitionName = definition != null ? definition.DisplayName : string.Empty;
            return ContainsUnitName("Archer", definitionName) || ContainsUnitName("\uAD81\uC218", definitionName);
        }

        private bool IsWizardUnit()
        {
            var definitionName = definition != null ? definition.DisplayName : string.Empty;
            return ContainsUnitName("Wizard", definitionName) || ContainsUnitName("Mage", definitionName) ||
                   ContainsUnitName("\uB9C8\uBC95\uC0AC", definitionName);
        }

        private int GetMaxHealthUpgradeCount() => IsArcherUnit() || IsWizardUnit() ? 2 : 3;

        private int GetMaxMovementUpgradeCount() => IsArcherUnit() ? 3 : 2;

        public bool CycleAttackMode()
        {
            if (!HasMultipleAttackModes)
            {
                currentAttackMode = TacticalAttackMode.Default;
                return false;
            }

            var modes = GetAvailableAttackModes();
            var currentIndex = modes.IndexOf(SelectedAttackMode);
            currentAttackMode = modes[(currentIndex + 1) % modes.Count];
            return true;
        }

        private List<TacticalAttackMode> GetAvailableAttackModes()
        {
            var modes = new List<TacticalAttackMode> { TacticalAttackMode.Default };
            if (hasThrustAttack) modes.Add(TacticalAttackMode.Thrust);
            if (hasPiercingArrowAttack) modes.Add(TacticalAttackMode.PiercingArrow);
            if (hasBowStrikeAttack) modes.Add(TacticalAttackMode.BowStrike);
            if (hasHarpoonAttack) modes.Add(TacticalAttackMode.Harpoon);
            if (hasFireballAttack) modes.Add(TacticalAttackMode.Fireball);
            if (hasIceSpikeAttack) modes.Add(TacticalAttackMode.IceSpike);
            if (hasNatureFragranceAttack) modes.Add(TacticalAttackMode.NatureFragrance);
            return modes;
        }

        private int GetAvailableAttackModeCount() => GetAvailableAttackModes().Count;

        private bool IsAttackModeUnlocked(TacticalAttackMode mode)
        {
            return mode == TacticalAttackMode.Default ||
                   mode == TacticalAttackMode.Thrust && hasThrustAttack ||
                   mode == TacticalAttackMode.PiercingArrow && hasPiercingArrowAttack ||
                   mode == TacticalAttackMode.BowStrike && hasBowStrikeAttack ||
                   mode == TacticalAttackMode.Harpoon && hasHarpoonAttack ||
                   mode == TacticalAttackMode.Fireball && hasFireballAttack ||
                   mode == TacticalAttackMode.IceSpike && hasIceSpikeAttack ||
                   mode == TacticalAttackMode.NatureFragrance && hasNatureFragranceAttack;
        }

        private bool IsAttackModeOnCooldown(TacticalAttackMode mode)
        {
            return mode == TacticalAttackMode.Fireball && fireballCooldownRemaining > 0 ||
                   mode == TacticalAttackMode.IceSpike && iceSpikeCooldownRemaining > 0 ||
                   mode == TacticalAttackMode.NatureFragrance && natureFragranceCooldownRemaining > 0;
        }

        private bool IsAttackModeAvailable(TacticalAttackMode mode)
        {
            return mode == TacticalAttackMode.Default ||
                   mode == TacticalAttackMode.Thrust && hasThrustAttack ||
                   mode == TacticalAttackMode.PiercingArrow && hasPiercingArrowAttack ||
                   mode == TacticalAttackMode.BowStrike && hasBowStrikeAttack ||
                   mode == TacticalAttackMode.Harpoon && hasHarpoonAttack ||
                   mode == TacticalAttackMode.Fireball && hasFireballAttack && fireballCooldownRemaining <= 0 ||
                   mode == TacticalAttackMode.IceSpike && hasIceSpikeAttack && iceSpikeCooldownRemaining <= 0 ||
                   mode == TacticalAttackMode.NatureFragrance && hasNatureFragranceAttack && natureFragranceCooldownRemaining <= 0;
        }

        private string GetAttackName(TacticalAttackMode mode)
        {
            switch (mode)
            {
                case TacticalAttackMode.Thrust:
                    return "\uCC0C\uB974\uAE30";
                case TacticalAttackMode.PiercingArrow:
                    return "\uAD00\uD1B5\uC0B4";
                case TacticalAttackMode.BowStrike:
                    return "\uD65C\uCE58\uAE30";
                case TacticalAttackMode.Harpoon:
                    return "\uC791\uC0B4";
                case TacticalAttackMode.Fireball:
                    return "\uD654\uC5FC\uAD6C";
                case TacticalAttackMode.IceSpike:
                    return "\uC5BC\uC74C\uC1C4\uAE30";
                case TacticalAttackMode.NatureFragrance:
                    return "\uC790\uC5F0\uC758 \uD5A5\uAE30";
                default:
                    return GetDefaultAttackName();
            }
        }
        private string GetDefaultAttackName()
        {
            var definitionName = definition != null ? definition.DisplayName : string.Empty;
            if (ContainsUnitName("Knight", definitionName) || ContainsUnitName("\uAE30\uC0AC", definitionName))
                return "\uBC30\uAE30";
            if (ContainsUnitName("Archer", definitionName) || ContainsUnitName("\uAD81\uC218", definitionName))
                return "\uC815\uC870\uC900";
            if (ContainsUnitName("Wizard", definitionName) || ContainsUnitName("Mage", definitionName) ||
                ContainsUnitName("\uB9C8\uBC95\uC0AC", definitionName))
                return "\uB9C8\uB825\uD0C4";
            return "\uACF5\uACA9";
        }

        private bool ContainsUnitName(string value, string definitionName)
        {
            return definitionName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        public bool IsThrustAttackPosition(GridPosition origin, GridPosition target)
        {
            if (CurrentAttackMode != TacticalAttackMode.Thrust)
                return false;
            return HasCurrentAttackModeTargetOffsets(out var targetOffsets)
                ? IsAttackModeTargetOffset(origin, target, targetOffsets)
                : target.Y == origin.Y && Mathf.Abs(target.X - origin.X) == 2;
        }

        public bool IsPiercingArrowAttackPosition(GridPosition origin, GridPosition target)
        {
            if (CurrentAttackMode != TacticalAttackMode.PiercingArrow)
                return false;
            return HasCurrentAttackModeTargetOffsets(out var targetOffsets)
                ? IsAttackModeTargetOffset(origin, target, targetOffsets)
                : IsLineAttackPosition(origin, target, 5);
        }

        public bool IsBowStrikeAttackPosition(GridPosition origin, GridPosition target)
        {
            if (CurrentAttackMode != TacticalAttackMode.BowStrike)
                return false;
            return HasCurrentAttackModeTargetOffsets(out var targetOffsets)
                ? IsAttackModeTargetOffset(origin, target, targetOffsets)
                : origin.ManhattanDistance(target) == 1;
        }

        public bool IsHarpoonAttackPosition(GridPosition origin, GridPosition target)
        {
            if (CurrentAttackMode != TacticalAttackMode.Harpoon)
                return false;
            return HasCurrentAttackModeTargetOffsets(out var targetOffsets)
                ? IsAttackModeTargetOffset(origin, target, targetOffsets)
                : target.Y == origin.Y && Mathf.Abs(target.X - origin.X) >= 1 && Mathf.Abs(target.X - origin.X) <= AttackRange;
        }

        public bool IsFireballAttackPosition(GridPosition origin, GridPosition target)
        {
            return IsRangedSpecialAttackPosition(
                TacticalAttackMode.Fireball, hasFireballAttack, origin, target);
        }

        public bool IsIceSpikeAttackPosition(GridPosition origin, GridPosition target)
        {
            return IsRangedSpecialAttackPosition(
                TacticalAttackMode.IceSpike, hasIceSpikeAttack, origin, target);
        }

        public bool IsNatureFragranceAttackPosition(GridPosition origin, GridPosition target)
        {
            if (!IsRangedSpecialAttackPosition(
                    TacticalAttackMode.NatureFragrance, hasNatureFragranceAttack, origin, target) ||
                board == null)
                return false;
            return board.TryGetOccupant(target, out var targetUnit) &&
                   targetUnit != null && targetUnit != this && targetUnit.IsAlive;
        }

        private bool IsRangedSpecialAttackPosition(
            TacticalAttackMode mode, bool unlocked, GridPosition origin, GridPosition target)
        {
            if (CurrentAttackMode != mode || !unlocked)
                return false;
            return HasCurrentAttackModeTargetOffsets(out var targetOffsets)
                ? IsAttackModeTargetOffset(origin, target, targetOffsets)
                : IsDefaultAttackTargetReachable(origin, target);
        }

        private bool HasCurrentAttackModeTargetOffsets(out IReadOnlyList<GridOffset> targetOffsets)
        {
            targetOffsets = null;
            return CurrentAttackMode != TacticalAttackMode.Default &&
                   definition != null &&
                   definition.TryGetAttackModeRange(CurrentAttackMode.ToString(), out targetOffsets, out _) &&
                   targetOffsets != null && targetOffsets.Count > 0;
        }

        private bool HasCurrentAttackModeEffectOffsets(out IReadOnlyList<GridOffset> effectOffsets)
        {
            effectOffsets = null;
            return CurrentAttackMode != TacticalAttackMode.Default &&
                   definition != null &&
                   definition.TryGetAttackModeRange(CurrentAttackMode.ToString(), out _, out effectOffsets) &&
                   effectOffsets != null && effectOffsets.Count > 0;
        }

        private static bool IsAttackModeTargetOffset(
            GridPosition origin, GridPosition target, IReadOnlyList<GridOffset> targetOffsets)
        {
            var deltaX = target.X - origin.X;
            var deltaY = target.Y - origin.Y;
            foreach (var offset in targetOffsets)
                if (offset.x == deltaX && offset.y == deltaY)
                    return true;
            return false;
        }

        private bool IsSpecialAttackTarget(GridPosition origin, GridPosition target)
        {
            return IsThrustAttackPosition(origin, target) ||
                   IsPiercingArrowAttackPosition(origin, target) ||
                   IsBowStrikeAttackPosition(origin, target) ||
                   IsHarpoonAttackPosition(origin, target) ||
                   IsFireballAttackPosition(origin, target) ||
                   IsIceSpikeAttackPosition(origin, target) ||
                   IsNatureFragranceAttackPosition(origin, target);
        }

        private bool IsDefaultAttackTargetReachable(GridPosition origin, GridPosition target)
        {
            if (target == origin)
                return false;
            if (HasCustomAttackTargetOffsets)
                return IsCustomAttackTargetOffset(
                    Mathf.Abs(target.X - origin.X), Mathf.Abs(target.Y - origin.Y),
                    target.X - origin.X, target.Y - origin.Y);
            return IsPositionInAttackRange(origin, target) &&
                   (CanPierceUnits || board == null || !board.HasUnitBetween(origin, target, this));
        }

        private IEnumerable<GridPosition> GetDefaultAttackTargetPositions(GridPosition origin)
        {
            if (HasCustomAttackTargetOffsets)
            {
                foreach (var offset in definition.AttackTargetOffsets)
                {
                    var position = new GridPosition(origin.X + offset.x, origin.Y + offset.y);
                    if (IsDefaultAttackTargetReachable(origin, position))
                        yield return position;
                }
                yield break;
            }

            if (board == null)
                yield break;
            foreach (var position in board.GetCellsInAttackRange(origin, AttackRange, VerticalAttackRange))
                if (IsDefaultAttackTargetReachable(origin, position))
                    yield return position;
        }

        private static bool IsLineAttackPosition(GridPosition origin, GridPosition target, int range)
        {
            var horizontal = Mathf.Abs(target.X - origin.X);
            var vertical = Mathf.Abs(target.Y - origin.Y);
            return horizontal + vertical >= 1 && horizontal + vertical <= range &&
                   (horizontal == 0 || vertical == 0);
        }

        public void BeginBattle()
        {
            agilityShieldAvailable = hasAgilityPassive;
            coverUsedThisTurn = false;
            fireballCooldownRemaining = 0;
            iceSpikeCooldownRemaining = 0;
            natureFragranceCooldownRemaining = 0;
            arcaneAccelerationTurnCounter = 0;
            arcaneAccelerationReady = false;
            arcaneAccelerationConsumedThisTurn = false;
        }

        public void BeginTurn()
        {
            RemainingMovement = MovementPoints;
            HasAttacked = false;
            coverUsedThisTurn = false;
            arcaneAccelerationConsumedThisTurn = false;
            if (fireballCooldownRemaining > 0) fireballCooldownRemaining--;
            if (iceSpikeCooldownRemaining > 0) iceSpikeCooldownRemaining--;
            if (natureFragranceCooldownRemaining > 0) natureFragranceCooldownRemaining--;
            if (hasArcaneAccelerationPassive)
            {
                arcaneAccelerationTurnCounter++;
                if (arcaneAccelerationTurnCounter >= 3)
                {
                    arcaneAccelerationTurnCounter = 0;
                    arcaneAccelerationReady = true;
                }
            }
            if (IsPlaced)
                TurnStartPosition = Position;
        }

        private const string BasicAttackSkillKey = "BasicAttack";

        private static string GetSkillKey(TacticalAttackMode attackMode)
        {
            return attackMode == TacticalAttackMode.Default ? BasicAttackSkillKey : attackMode.ToString();
        }
        private void NotifyAttackUsed(TacticalAttackMode attackMode)
        {
            var skillKey = GetSkillKey(attackMode);
            if (string.IsNullOrWhiteSpace(skillKey))
                return;
            AttackUsed?.Invoke(this, skillKey);
        }


        private void StartAttackCooldown(TacticalAttackMode mode)
        {
            switch (mode)
            {
                case TacticalAttackMode.Fireball:
                    fireballCooldownRemaining = FireballCooldown;
                    break;
                case TacticalAttackMode.IceSpike:
                    iceSpikeCooldownRemaining = IceSpikeCooldown;
                    break;
                case TacticalAttackMode.NatureFragrance:
                    natureFragranceCooldownRemaining = NatureFragranceCooldown;
                    break;
            }
        }

        private void ConsumeAttackAction()
        {
            if (hasArcaneAccelerationPassive && arcaneAccelerationReady &&
                !arcaneAccelerationConsumedThisTurn)
            {
                arcaneAccelerationConsumedThisTurn = true;
                arcaneAccelerationReady = false;
                HasAttacked = false;
                return;
            }
            HasAttacked = true;
        }
        public bool TryUndoMovement()
        {
            if (!CanUndoMovement)
                return false;
            if (!GridPathfinder.TryFindPath(
                    board, Position, TurnStartPosition, int.MaxValue, this, out var path))
                return false;
            if (!board.TryOccupy(this, TurnStartPosition))
                return false;

            RemainingMovement = MovementPoints;
            if (TurnStartPosition.X != Position.X)
                SetFacingDirection(TurnStartPosition.X > Position.X ? 1 : -1);
            StartCoroutine(MoveRoutine(path, TurnStartPosition));
            return true;
        }

        public IEnumerable<GridPosition> GetAttackTargetPositions(GridPosition origin)
        {
            var yielded = new HashSet<GridPosition>();

            if (CurrentAttackMode != TacticalAttackMode.Default)
            {
                foreach (var position in GetSpecialAttackPositions(origin))
                {
                    if (position == origin || !yielded.Add(position))
                        continue;
                    if (IsPositionAttackableFrom(origin, position))
                        yield return position;
                }
                yield break;
            }

            if (HasCustomAttackTargetOffsets)
            {
                foreach (var offset in definition.AttackTargetOffsets)
                {
                    var position = new GridPosition(origin.X + offset.x, origin.Y + offset.y);
                    if (position == origin || !yielded.Add(position))
                        continue;
                    if (IsPositionAttackableFrom(origin, position))
                        yield return position;
                }
            }
            else if (board != null)
            {
                foreach (var position in board.GetCellsInAttackRange(
                             origin, AttackRange, VerticalAttackRange))
                {
                    if (position == origin || !yielded.Add(position))
                        continue;
                    if (IsPositionAttackableFrom(origin, position))
                        yield return position;
                }
            }
        }
        public IEnumerable<GridPosition> GetAttackEffectPositions(GridPosition targetPosition)
        {
            if (!HasCustomAttackEffectOffsets)
            {
                yield return targetPosition;
                yield break;
            }

            var yielded = new HashSet<GridPosition>();
            foreach (var offset in definition.AttackEffectOffsets)
            {
                var position = new GridPosition(targetPosition.X + offset.x, targetPosition.Y + offset.y);
                if (yielded.Add(position))
                    yield return position;
            }
        }
        public IEnumerable<GridPosition> GetSpecialAttackPositions(GridPosition origin)
        {
            if (HasCurrentAttackModeTargetOffsets(out var targetOffsets))
            {
                foreach (var offset in targetOffsets)
                    yield return new GridPosition(origin.X + offset.x, origin.Y + offset.y);
                yield break;
            }

            if (CurrentAttackMode == TacticalAttackMode.Thrust && hasThrustAttack)
            {
                yield return new GridPosition(origin.X - 2, origin.Y);
                yield return new GridPosition(origin.X + 2, origin.Y);
            }
            else if (CurrentAttackMode == TacticalAttackMode.PiercingArrow && hasPiercingArrowAttack)
            {
                foreach (var position in GetLinePositions(origin, 5, includeVertical: true))
                    yield return position;
            }
            else if (CurrentAttackMode == TacticalAttackMode.BowStrike && hasBowStrikeAttack)
            {
                yield return new GridPosition(origin.X - 1, origin.Y);
                yield return new GridPosition(origin.X + 1, origin.Y);
                yield return new GridPosition(origin.X, origin.Y - 1);
                yield return new GridPosition(origin.X, origin.Y + 1);
            }
            else if (CurrentAttackMode == TacticalAttackMode.Harpoon && hasHarpoonAttack)
            {
                foreach (var position in GetLinePositions(origin, AttackRange, includeVertical: false))
                    yield return position;
            }
            else if (CurrentAttackMode == TacticalAttackMode.Fireball && hasFireballAttack ||
                     CurrentAttackMode == TacticalAttackMode.IceSpike && hasIceSpikeAttack ||
                     CurrentAttackMode == TacticalAttackMode.NatureFragrance && hasNatureFragranceAttack)
            {
                foreach (var position in GetDefaultAttackTargetPositions(origin))
                    yield return position;
            }
        }

        public IEnumerable<GridPosition> GetSpecialAttackEffectPositions(GridPosition targetPosition)
        {
            if (CurrentAttackMode == TacticalAttackMode.Fireball && hasFireballAttack)
            {
                foreach (var position in GetAreaPositions(targetPosition, 1))
                    yield return position;
                yield break;
            }

            if (HasCurrentAttackModeEffectOffsets(out var effectOffsets))
            {
                var yielded = new HashSet<GridPosition>();
                foreach (var offset in effectOffsets)
                {
                    var position = new GridPosition(targetPosition.X + offset.x, targetPosition.Y + offset.y);
                    if (yielded.Add(position))
                        yield return position;
                }
                yield break;
            }

            yield return targetPosition;
        }

        private static IEnumerable<GridPosition> GetLinePositions(GridPosition origin, int range, bool includeVertical)
        {
            for (var distance = 1; distance <= range; distance++)
            {
                yield return new GridPosition(origin.X - distance, origin.Y);
                yield return new GridPosition(origin.X + distance, origin.Y);
                if (!includeVertical)
                    continue;
                yield return new GridPosition(origin.X, origin.Y - distance);
                yield return new GridPosition(origin.X, origin.Y + distance);
            }
        }
        public bool CanAttack(TacticalUnit target, bool allowFriendlyFire = false)
        {
            return !isCrate && IsAlive && IsPlaced && !IsMoving && !IsAttacking &&
                   !isImpactReserved && !HasAttacked &&
                   target != null && target != this && target.IsAlive && target.IsPlaced &&
                   (allowFriendlyFire || target.Team != Team) &&
                   IsPositionAttackableFrom(Position, target.Position);
        }

        public bool CanAttackPosition(GridPosition targetPosition)
        {
            return !isCrate && IsAlive && IsPlaced && !IsMoving && !IsAttacking &&
                   !isImpactReserved && !HasAttacked &&
                   targetPosition != Position && IsPositionAttackableFrom(Position, targetPosition);
        }

        public bool IsPositionAttackableFrom(GridPosition origin, GridPosition target)
        {
            if (IsSpecialAttackTarget(origin, target))
                return true;
            return IsPositionInAttackRange(origin, target) &&
                   (CanPierceUnits || board == null || !board.HasUnitBetween(origin, target, this));
        }

        public bool IsPositionInAttackRange(GridPosition origin, GridPosition target)
        {
            var horizontalDistance = Mathf.Abs(origin.X - target.X);
            var verticalDistance = Mathf.Abs(origin.Y - target.Y);
            if (HasCustomAttackTargetOffsets)
                return IsCustomAttackTargetOffset(horizontalDistance, verticalDistance, target.X - origin.X, target.Y - origin.Y);

            var distance = Mathf.Max(horizontalDistance, verticalDistance);
            var minimumDistance = AttackDistanceRule == AttackDistanceRule.DistantOnly
                ? MinimumAttackRange
                : 0;
            return distance >= minimumDistance &&
                   horizontalDistance <= AttackRange &&
                   verticalDistance <= VerticalAttackRange;
        }

        private bool IsCustomAttackTargetOffset(
            int horizontalDistance, int verticalDistance, int deltaX, int deltaY)
        {
            if (horizontalDistance == 0 && verticalDistance == 0 || definition == null)
                return false;

            foreach (var offset in definition.AttackTargetOffsets)
                if (offset.x == deltaX && offset.y == deltaY)
                    return true;
            return false;
        }

        public bool TryAttack(TacticalUnit target, bool allowFriendlyFire = false)
        {
            if (!CanAttack(target, allowFriendlyFire))
                return false;

            return TryAttackPosition(target.Position, allowFriendlyFire);
        }

        public bool TryAttackPosition(GridPosition targetPosition, bool allowFriendlyFire = false)
        {
            if (!CanAttackPosition(targetPosition))
                return false;
            if (board.TryGetOccupant(targetPosition, out var occupant) &&
                (occupant == this || !allowFriendlyFire && occupant.Team == Team))
                return false;

            var attackMode = CurrentAttackMode;
            StartAttackCooldown(attackMode);
            ConsumeAttackAction();
            if (targetPosition.X != Position.X)
                SetFacingDirection(targetPosition.X > Position.X ? 1 : -1);
            StartCoroutine(AttackRoutine(targetPosition, allowFriendlyFire, false, attackMode));
            return true;
        }

        public bool TryAttackWoodPosition(GridPosition targetPosition)
        {
            if (isCrate || !IsAlive || !IsPlaced || IsMoving || IsAttacking ||
                isImpactReserved || HasAttacked || board == null ||
                !board.IsWoodTile(targetPosition))
                return false;
            if (targetPosition != Position && !CanAttackPosition(targetPosition))
                return false;

            var attackMode = CurrentAttackMode;
            StartAttackCooldown(attackMode);
            ConsumeAttackAction();
            if (targetPosition.X != Position.X)
                SetFacingDirection(targetPosition.X > Position.X ? 1 : -1);
            StartCoroutine(AttackRoutine(targetPosition, true, true, attackMode));
            return true;
        }

        public bool Heal(int amount, TacticalUnit source = null)
        {
            if (!IsAlive || amount <= 0 || CurrentHealth >= MaxHealth)
                return false;

            var previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            var healedAmount = CurrentHealth - previousHealth;
            HealthChanged?.Invoke(this, CurrentHealth, MaxHealth);
            if (healedAmount > 0)
                Healed?.Invoke(this, source, healedAmount);
            return healedAmount > 0;
        }

        public void TakeDamage(int amount, TacticalUnit source = null)
        {
            if (!IsAlive || amount <= 0)
                return;

if (hasAgilityPassive && agilityShieldAvailable)
            {
                agilityShieldAvailable = false;
                HealthChanged?.Invoke(this, CurrentHealth, MaxHealth);
                return;
            }

            if (source != null && source != this)
            {
                LastDamageSource = source;
                LastDamageSkillKey = !string.IsNullOrWhiteSpace(source.currentDamageSkillKey)
                    ? source.currentDamageSkillKey
                    : source.lastAttackSkillKey ?? string.Empty;
            }
            unitAnimator?.PlayHit();
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            HealthChanged?.Invoke(this, CurrentHealth, MaxHealth);
            if (CurrentHealth > 0)
                return;

            SetSelectedHighlighted(false);
            SetAttackPreviewed(false);
            var deathPosition = Position;
            unitAnimator?.PlayDeath();
            if (board != null)
                board.RemoveOccupancy(this);
            if (isExplosiveCrate && board != null)
                board.Detonate(deathPosition, this, explosionDamage, source);
            Died?.Invoke(this);
            gameObject.SetActive(false);
        }

        public bool DebugLevelUp()
        {
            if (!IsAlive || isCrate || isObjective)
                return false;

            GainExperience(Mathf.Max(1, experienceToNextLevel - currentExperience));
            return true;
        }

        public void GainExperience(int amount)
        {
            if (amount <= 0 || isCrate || isObjective)
                return;

            currentExperience += amount;
            while (currentExperience >= experienceToNextLevel)
            {
                currentExperience -= experienceToNextLevel;
                currentLevel++;
                LeveledUp?.Invoke(this, currentLevel);
                unitAnimator?.PlayLevelUp();
                PlayLevelUpEffect();
            }
            ExperienceChanged?.Invoke(this, currentExperience, experienceToNextLevel);
        }

        private void PlayLevelUpEffect()
        {
            if (levelUpEffectRoutine != null)
                StopCoroutine(levelUpEffectRoutine);
            levelUpEffectRoutine = StartCoroutine(LevelUpEffectRoutine());
        }

        private IEnumerator LevelUpEffectRoutine()
        {
            if (previewRenderer == null)
                yield break;

            var originalScale = transform.localScale;
            var originalColor = previewRenderer.color;
            const float duration = 0.42f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pulse = Mathf.Sin(t * Mathf.PI);
                transform.localScale = Vector3.Scale(
                    originalScale, new Vector3(1f + pulse * 0.18f, 1f + pulse * 0.18f, 1f));
                previewRenderer.color = Color.Lerp(
                    originalColor, new Color(1f, 0.95f, 0.25f, originalColor.a), pulse * 0.75f);
                yield return null;
            }

            transform.localScale = originalScale;
            previewRenderer.color = originalColor;
            levelUpEffectRoutine = null;
        }
        public void SetAttackPreviewed(bool previewed)
        {
            if (attackPreviewBlinkRoutine != null)
            {
                StopCoroutine(attackPreviewBlinkRoutine);
                attackPreviewBlinkRoutine = null;
            }

            if (previewRenderer == null)
                return;

            if (!previewed)
            {
                previewRenderer.color = previewOriginalColor;
                return;
            }

            previewOriginalColor = previewRenderer.color;
            attackPreviewBlinkRoutine = StartCoroutine(AttackPreviewBlinkRoutine());
        }

        public void SetSelectedHighlighted(bool highlighted)
        {
            if (selectionHighlightRoutine != null)
            {
                StopCoroutine(selectionHighlightRoutine);
                selectionHighlightRoutine = null;
            }

            if (previewRenderer == null)
                return;

            if (!highlighted)
            {
                previewRenderer.color = selectionOriginalColor;
                return;
            }

            selectionOriginalColor = previewRenderer.color;
            selectionHighlightRoutine = StartCoroutine(SelectionHighlightRoutine());
        }

        public void SetPreviewDamage(int damage) => healthBar?.SetPreviewDamage(damage);

        public void SetHealthBarVisible(bool visible) =>
            healthBar?.SetVisible(!isCrate || visible);

        private IEnumerator AttackPreviewBlinkRoutine()
        {
            while (true)
            {
                var pulse = (Mathf.Sin(Time.unscaledTime * 9f) + 1f) * 0.5f;
                var color = Color.Lerp(previewOriginalColor, Color.white, pulse * 0.55f);
                color.a = Mathf.Lerp(0.35f, previewOriginalColor.a, pulse);
                previewRenderer.color = color;
                yield return null;
            }
        }

        private IEnumerator SelectionHighlightRoutine()
        {
            while (true)
            {
                var pulse = (Mathf.Sin(Time.unscaledTime * 7f) + 1f) * 0.5f;
                previewRenderer.color = Color.Lerp(
                    selectionOriginalColor,
                    new Color(1f, 0.95f, 0.35f, selectionOriginalColor.a),
                    0.25f + pulse * 0.35f);
                yield return null;
            }
        }

        private void ApplyThrustAttack(GridPosition targetPosition, bool allowFriendlyFire)
        {
            if (board == null)
                return;

            var direction = targetPosition.X > Position.X ? 1 : -1;
            var front = new GridPosition(Position.X + direction, Position.Y);
            var back = new GridPosition(Position.X + direction * 2, Position.Y);
            ApplyThrustDamage(front, ThrustFrontDamage, direction, allowFriendlyFire);
            ApplyThrustDamage(back, ThrustBackDamage, direction, allowFriendlyFire);
        }

        private void ApplyThrustDamage(
            GridPosition position, int damage, int direction, bool allowFriendlyFire)
        {
            if (damage <= 0 || board == null)
                return;

            if (board.TryGetOccupant(position, out var target) && target != null &&
                target != this && target.IsAlive && (allowFriendlyFire || target.Team != Team))
            {
                if (!TryDealAttackDamage(target, damage, allowFriendlyFire))
                    return;
                if (ThrustHasKnockback && target.IsAlive)
                    target.TryKnockbackInDirection(direction, 1, damageSource: this);
            }
            else if (board.IsWoodTile(position))
            {
                board.DamageWood(position, damage, this);
            }
        }

        private IEnumerator ApplyAttackEffectCell(
            GridPosition position, bool allowFriendlyFire, bool attackWoodOnly)
        {
            if (!attackWoodOnly && board.TryGetOccupant(position, out var target) &&
                target != this && target.IsAlive && (allowFriendlyFire || target.Team != Team))
            {
                yield return ApplyAttackDamageToUnit(target, allowFriendlyFire);
            }
            else if (board.IsWoodTile(position))
            {
                board.DamageWood(position, BasicAttackDamage, this);
            }
        }

private IEnumerator ApplyAttackDamageToUnit(TacticalUnit target, bool allowFriendlyFire)
        {
            if (target == null || target == this || !target.IsAlive ||
                !allowFriendlyFire && target.Team == Team)
                yield break;

            if (TryTriggerCoverFire(target))
                yield break;

            // Keep a lethally-hit target alive until knockback resolves so it can
            // still collide with a wall or another unit before dying.
            var deferLethalDamage = KnockbackDistance > 0 &&
                                    BasicAttackDamage >= target.CurrentHealth;
            if (!deferLethalDamage)
                target.TakeDamage(BasicAttackDamage, this);

            if (target.IsAlive && KnockbackDistance > 0 &&
                target.TryKnockbackFrom(this, KnockbackDistance))
            {
                while (board.HasUnitsResolvingForcedMovement())
                    yield return null;
            }

            if (deferLethalDamage && target != null && target.IsAlive)
                target.TakeDamage(BasicAttackDamage, this);
        }

        private bool TryDealAttackDamage(TacticalUnit target, int damage, bool allowFriendlyFire)
        {
            if (target == null || target == this || !target.IsAlive || damage <= 0 ||
                !allowFriendlyFire && target.Team == Team)
                return false;
            if (TryTriggerCoverFire(target))
                return false;
            target.TakeDamage(damage, this);
            return target != null && target.IsAlive;
        }
        private bool TryTriggerCoverFire(TacticalUnit target)
        {
            if (target == null || target.Team == Team || board == null)
                return false;

            foreach (var ally in board.GetOccupantsInRange(Position, 999))
            {
                if (ally == null || ally == this || ally == target || !ally.IsAlive ||
                    ally.Team != target.Team || !ally.hasCoverPassive || ally.coverUsedThisTurn)
                    continue;
                if (!ally.IsPositionInAttackRange(ally.Position, Position))
                    continue;
                if (!ally.CanPierceUnits && board.HasUnitBetween(ally.Position, Position, ally))
                    continue;

                ally.coverUsedThisTurn = true;
                TakeDamage(1, ally);
                return !IsAlive;
            }

            return false;
        }
        private IEnumerator ApplyPiercingArrowAttack(GridPosition targetPosition, bool allowFriendlyFire)
        {
            if (HasCurrentAttackModeEffectOffsets(out _))
            {
                foreach (var position in GetSpecialAttackEffectPositions(targetPosition))
                {
                    if (board.TryGetOccupant(position, out var target) && target != null && target != this &&
                        target.IsAlive && (allowFriendlyFire || target.Team != Team))
                        TryDealAttackDamage(target, PiercingArrowDamage, allowFriendlyFire);
                    if (board.IsWoodTile(position))
                        board.DamageWood(position, PiercingArrowDamage, this);
                }
                yield break;
            }

            var direction = GetLineDirection(Position, targetPosition);
            for (var i = 1; i <= 5; i++)
            {
                var position = new GridPosition(Position.X + direction.x * i, Position.Y + direction.y * i);
                if (board.TryGetOccupant(position, out var target) && target != null && target != this &&
                    target.IsAlive && (allowFriendlyFire || target.Team != Team))
                    TryDealAttackDamage(target, PiercingArrowDamage, allowFriendlyFire);
                if (board.IsWoodTile(position))
                    board.DamageWood(position, PiercingArrowDamage, this);
            }
            yield break;
        }

        private IEnumerator ApplyBowStrikeAttack(GridPosition targetPosition, bool allowFriendlyFire)
        {
            if (board.TryGetOccupant(targetPosition, out var target) && target != null && target != this &&
                target.IsAlive && (allowFriendlyFire || target.Team != Team))
            {
                var direction = target.Position.X == Position.X ? FacingDirection : target.Position.X > Position.X ? 1 : -1;
                TryDealAttackDamage(target, BowStrikeDamage, allowFriendlyFire);
                if (target.IsAlive && target.TryKnockbackInDirection(direction, 2, damageSource: this))
                    while (board.HasUnitsResolvingForcedMovement())
                        yield return null;
            }
        }

        private IEnumerator ApplyHarpoonAttack(GridPosition targetPosition, bool allowFriendlyFire)
        {
            if (board.TryGetOccupant(targetPosition, out var target) && target != null && target != this &&
                target.IsAlive && (allowFriendlyFire || target.Team != Team))
            {
                var direction = target.Position.X > Position.X ? -1 : 1;
                TryDealAttackDamage(target, HarpoonDamage, allowFriendlyFire);
                if (target.IsAlive && target.TryKnockbackInDirection(direction, 1, damageSource: this))
                    while (board.HasUnitsResolvingForcedMovement())
                        yield return null;
            }
        }

        private IEnumerator ApplyFireballAttack(GridPosition targetPosition, bool allowFriendlyFire)
        {
            foreach (var position in GetAreaPositions(targetPosition, 1))
            {
                if (board.TryGetOccupant(position, out var target) && target != null && target != this &&
                    target.IsAlive && (allowFriendlyFire || target.Team != Team))
                    TryDealAttackDamage(target, FireballDamage, allowFriendlyFire);
                else if (board.IsWoodTile(position))
                    board.DamageWood(position, FireballDamage, this);
            }
            yield break;
        }

        private IEnumerator ApplyIceSpikeAttack(GridPosition targetPosition, bool allowFriendlyFire)
        {
            if (board.TryGetOccupant(targetPosition, out var target) && target != null && target != this &&
                target.IsAlive && (allowFriendlyFire || target.Team != Team))
            {
                TryDealAttackDamage(target, IceSpikeDamage, allowFriendlyFire);
                yield break;
            }

            SpawnIceBox(targetPosition);
            yield break;
        }

        private IEnumerator ApplyNatureFragranceAttack(GridPosition targetPosition)
        {
            if (board.TryGetOccupant(targetPosition, out var target) && target != null && target != this &&
                target.IsAlive)
                target.Heal(NatureFragranceHealAmount, this);
            yield break;
        }

        private void SpawnIceBox(GridPosition targetPosition)
        {
            if (board == null || board.TryGetOccupant(targetPosition, out _))
                return;

            var iceObject = new GameObject(
                $"Ice Box {targetPosition.X},{targetPosition.Y}",
                typeof(SpriteRenderer), typeof(BoxCollider2D));
            iceObject.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            var renderer = iceObject.GetComponent<SpriteRenderer>();
            renderer.sprite = GetFallbackBodySprite();
            renderer.color = new Color(0.45f, 0.9f, 1f, 0.95f);
            renderer.sortingOrder = 18;
            iceObject.GetComponent<BoxCollider2D>().size = Vector2.one;
            var ice = iceObject.AddComponent<TacticalUnit>();
            ice.ConfigureAsCrate(1, false, 0);
            ice.RestoreFacingDirection(FacingDirection);
            if (!board.TryOccupySpawnedObstacle(ice, targetPosition))
            {
                Destroy(iceObject);
                return;
            }

            ice.board = board;
            ice.Position = targetPosition;
            ice.IsPlaced = true;
            ice.ApplyUnitBodySize();
            ice.transform.position = ice.GetStandingWorldPosition(targetPosition);
            if (!board.IsWalkable(targetPosition))
                ice.TryFallAfterSupportDestroyed(this);
        }

        private static IEnumerable<GridPosition> GetAreaPositions(GridPosition center, int radius)
        {
            for (var x = center.X - radius; x <= center.X + radius; x++)
                for (var y = center.Y - radius; y <= center.Y + radius; y++)
                    yield return new GridPosition(x, y);
        }

        private static Vector2Int GetLineDirection(GridPosition origin, GridPosition target)
        {
            var dx = Math.Sign(target.X - origin.X);
            var dy = Math.Sign(target.Y - origin.Y);
            if (Mathf.Abs(target.X - origin.X) >= Mathf.Abs(target.Y - origin.Y))
                dy = 0;
            else
                dx = 0;
            return new Vector2Int(dx, dy);
        }

        private IEnumerator AttackRoutine(
            GridPosition targetPosition, bool allowFriendlyFire, bool attackWoodOnly, TacticalAttackMode attackMode)
        {
            IsAttacking = true;
            var previousDamageSkillKey = currentDamageSkillKey;
            currentDamageSkillKey = GetSkillKey(attackMode);
            lastAttackSkillKey = currentDamageSkillKey;
            var attackStartPosition = Position;
            var origin = transform.position;
            var direction = targetPosition.X == Position.X
                ? FacingDirection
                : targetPosition.X > Position.X ? 1 : -1;
            var lunge = origin + Vector3.right * (0.18f * direction);
            SetFacingDirection(direction);
            var beforeAttack = BeforeAttack?.Invoke(this, GetSkillKey(attackMode), targetPosition);
            if (beforeAttack != null)
                yield return beforeAttack;
            NotifyAttackUsed(attackMode);
            unitAnimator?.PlayAttack();

            yield return MoveTransform(origin, lunge, 0.1f);
            var areaTargets = new List<TacticalUnit>();
            if (attackMode == TacticalAttackMode.Default && IsWizardUnit() && AreaKnockbackRadius > 0 && AreaKnockbackDistance > 0)
            {
                if (attackWoodOnly)
                {
                    for (var xOffset = -1; xOffset <= 1; xOffset += 2)
                    {
                        var sidePosition = new GridPosition(
                            targetPosition.X + xOffset, targetPosition.Y);
                        if (board.TryGetOccupant(sidePosition, out var sideUnit) &&
                            sideUnit != null && sideUnit != this)
                            areaTargets.Add(sideUnit);
                    }
                }
                else
                {
                    foreach (var nearby in board.GetOccupantsInRange(
                                 targetPosition, AreaKnockbackRadius))
                    {
                        if (nearby.Position.X != targetPosition.X)
                            areaTargets.Add(nearby);
                    }
                }
            }

            var directlyAffectedPositions = new HashSet<GridPosition>();
            if (!attackWoodOnly && attackMode == TacticalAttackMode.Thrust && hasThrustAttack)
            {
                ApplyThrustAttack(targetPosition, allowFriendlyFire);
            }
            else if (!attackWoodOnly && attackMode == TacticalAttackMode.PiercingArrow && hasPiercingArrowAttack)
            {
                if (HasCustomCurrentAttackModeEffectOffsets)
                {
                    foreach (var position in GetSpecialAttackEffectPositions(targetPosition))
                        directlyAffectedPositions.Add(position);
                }
                else
                {
                    var lineDirection = GetLineDirection(Position, targetPosition);
                    foreach (var position in GetLinePositions(Position, 5, includeVertical: true))
                        if (GetLineDirection(Position, position) == lineDirection)
                            directlyAffectedPositions.Add(position);
                }
                yield return ApplyPiercingArrowAttack(targetPosition, allowFriendlyFire);
            }
            else if (!attackWoodOnly && attackMode == TacticalAttackMode.BowStrike && hasBowStrikeAttack)
            {
                directlyAffectedPositions.Add(targetPosition);
                yield return ApplyBowStrikeAttack(targetPosition, allowFriendlyFire);
            }
            else if (!attackWoodOnly && attackMode == TacticalAttackMode.Harpoon && hasHarpoonAttack)
            {
                directlyAffectedPositions.Add(targetPosition);
                yield return ApplyHarpoonAttack(targetPosition, allowFriendlyFire);
            }
            else if (!attackWoodOnly && attackMode == TacticalAttackMode.Fireball && hasFireballAttack)
            {
                foreach (var position in GetAreaPositions(targetPosition, 1))
                    directlyAffectedPositions.Add(position);
                yield return ApplyFireballAttack(targetPosition, allowFriendlyFire);
            }
            else if (!attackWoodOnly && attackMode == TacticalAttackMode.IceSpike && hasIceSpikeAttack)
            {
                directlyAffectedPositions.Add(targetPosition);
                yield return ApplyIceSpikeAttack(targetPosition, allowFriendlyFire);
            }
            else if (!attackWoodOnly && attackMode == TacticalAttackMode.NatureFragrance && hasNatureFragranceAttack)
            {
                directlyAffectedPositions.Add(targetPosition);
                yield return ApplyNatureFragranceAttack(targetPosition);
            }
            else if (attackMode == TacticalAttackMode.Default && HasCustomAttackEffectOffsets)
            {
                foreach (var effectPosition in GetAttackEffectPositions(targetPosition))
                {
                    if (IsWizardDefaultAttackMode() && effectPosition != targetPosition)
                        continue;
                    directlyAffectedPositions.Add(effectPosition);
                    yield return ApplyAttackEffectCell(effectPosition, allowFriendlyFire, attackWoodOnly);
                }
            }
            else if (!attackWoodOnly &&
                board.TryGetOccupant(targetPosition, out var target) && target != this &&
                target.IsAlive && (allowFriendlyFire || target.Team != Team))
            {
                directlyAffectedPositions.Add(targetPosition);
                yield return ApplyAttackDamageToUnit(target, allowFriendlyFire);
            }
            else if (attackWoodOnly)
            {
                directlyAffectedPositions.Add(targetPosition);
                board.DamageWood(targetPosition, BasicAttackDamage, this);
            }
            var startedAreaKnockback = false;
            foreach (var nearby in areaTargets)
            {
                if (nearby == null || !nearby.IsAlive ||
                    directlyAffectedPositions.Contains(nearby.Position) ||
                    attackWoodOnly && nearby == this)
                    continue;
                var radialDirection = nearby.Position.X == targetPosition.X
                    ? FacingDirection
                    : nearby.Position.X > targetPosition.X ? 1 : -1;
                if (nearby.TryKnockbackInDirection(
                        radialDirection, AreaKnockbackDistance, allowWhileAttacking: nearby == this,
                        damageSource: this))
                    startedAreaKnockback = true;
            }

            // Start every radial knockback in the same frame so both sides move
            // together, then wait for all falling and chained pushes to finish.
            if (startedAreaKnockback)
                while (board.HasUnitsResolvingForcedMovement())
                    yield return null;

            var returnPosition = Position == attackStartPosition
                ? origin
                : GetStandingWorldPosition(Position);
            yield return MoveTransform(transform.position, returnPosition, 0.1f);
            currentDamageSkillKey = previousDamageSkillKey;
            IsAttacking = false;
            if (!IsMoving)
                unitAnimator?.PlayIdle();
        }

        public bool TryKnockbackFrom(TacticalUnit source, int distance)
        {
            if (isObjective || !IsAlive || !IsPlaced || IsMoving || IsAttacking || source == null || distance <= 0)
                return false;

            var direction = Position.X == source.Position.X
                ? source.FacingDirection
                : Position.X > source.Position.X ? 1 : -1;
            return TryKnockbackInDirection(direction, distance, damageSource: source);
        }

        public bool TryFallAfterSupportDestroyed(TacticalUnit damageSource = null)
        {
            if (!IsAlive || !IsPlaced || IsMoving || board == null)
                return false;

            var result = board.ResolveVerticalFall(
                Position, this, out var landing, out var fallDistance, out var blocker);
            var fallDamage = Mathf.Min(MaxHealth, Mathf.Max(0, fallDistance - 1));

            if (result == KnockbackLandingType.Landing)
            {
                if (!board.TryOccupy(this, landing))
                    return false;
                StartCoroutine(SupportDestroyedFallRoutine(landing, fallDamage, damageSource));
                return true;
            }

            if (result == KnockbackLandingType.Collision && blocker != null && blocker.IsAlive)
            {
                if (blocker.isImpactReserved)
                    return false;
                var pushResult = board.ResolveKnockbackLanding(
                    blocker.Position, FacingDirection, blocker, out _, out _, out _);
                if (pushResult == KnockbackLandingType.Landing)
                {
                    board.RemoveOccupancy(this);
                    blocker.isImpactReserved = true;
                    reservedImpactTarget = blocker;
                    StartCoroutine(FallPushCollisionRoutine(
                        new List<GridPosition>(), landing, blocker,
                        FacingDirection, fallDamage, damageSource));
                }
                else
                {
                    board.RemoveOccupancy(this);
                    StartCoroutine(FallCollisionRoutine(
                        new List<GridPosition>(), landing, blocker,
                        blocker.CurrentHealth, CurrentHealth, damageSource));
                }
                return true;
            }

            board.RemoveOccupancy(this);
            StartCoroutine(SupportDestroyedVoidFallRoutine(damageSource));
            return true;
        }

        public bool TryKnockbackInDirection(
            int direction, int distance, bool allowWhileAttacking = false,
            bool suppressFallDamage = false, TacticalUnit damageSource = null)
        {
            if (isObjective || !IsAlive || !IsPlaced || IsMoving || isImpactReserved ||
                (IsAttacking && !allowWhileAttacking) || distance <= 0)
                return false;
            direction = direction < 0 ? -1 : 1;
            var path = new List<GridPosition>();
            var current = Position;
            var fallDamage = 0;

            for (var i = 0; i < distance; i++)
            {
                var result = board.ResolveKnockbackLanding(
                    current, direction, this, out var next, out var fallDistance, out var blocker);

                if (result == KnockbackLandingType.Collision)
                {
                    // When landing on another unit from above, push that unit one
                    // tile in the knockback direction. If it has nowhere to go,
                    // both units deal their current health to the other on impact.
                    if (fallDistance > 0 && blocker != null && blocker.IsAlive)
                    {
                        if (blocker.isImpactReserved)
                            return false;

                        var pushResult = board.ResolveKnockbackLanding(
                            blocker.Position, direction, blocker, out _, out _, out _);
                        if (pushResult == KnockbackLandingType.Landing)
                        {
                            var impactFallDamage = suppressFallDamage
                                ? 0
                                : Mathf.Min(
                                    MaxHealth,
                                    fallDamage + Mathf.Max(0, fallDistance - 1));
                            blocker.isImpactReserved = true;
                            reservedImpactTarget = blocker;
                            StartCoroutine(FallPushCollisionRoutine(
                                path, next, blocker, direction, impactFallDamage, damageSource));
                            return true;
                        }

                        var upperDamage = blocker.CurrentHealth;
                        var lowerDamage = CurrentHealth;
                        StartCoroutine(FallCollisionRoutine(
                            path, next, blocker, upperDamage, lowerDamage, damageSource));
                        return true;
                    }

                    if (blocker != null)
                    {
                        if (blocker.isImpactReserved)
                            return false;
                        blocker.isImpactReserved = true;
                        reservedImpactTarget = blocker;
                    }

                    if (path.Count > 0 && !board.TryOccupy(this, current))
                    {
                        ReleaseImpactReservation();
                        return false;
                    }

                    var horizontalImpactPosition = blocker != null
                        ? blocker.Position
                        : new GridPosition(current.X + direction, current.Y);
                    StartCoroutine(HorizontalCollisionRoutine(
                        path, current, horizontalImpactPosition, blocker, damageSource));
                    return true;
                }

                if (result == KnockbackLandingType.Void)
                {
                    if (board != null)
                        board.RemoveOccupancy(this);
                    StartCoroutine(FallIntoVoidRoutine(path, direction, damageSource));
                    return true;
                }

                path.Add(next);
                current = next;
                fallDamage += Mathf.Max(0, fallDistance - 1);
            }

            if (path.Count == 0 || !board.TryOccupy(this, current))
                return false;

            var resolvedFallDamage = suppressFallDamage
                ? 0
                : Mathf.Min(MaxHealth, fallDamage);
            StartCoroutine(KnockbackMoveRoutine(path, current, resolvedFallDamage, damageSource));
            return true;
        }

        private IEnumerator FallPushCollisionRoutine(
            IReadOnlyList<GridPosition> path,
            GridPosition collisionPosition,
            TacticalUnit lowerUnit,
            int direction,
            int fallDamage,
            TacticalUnit damageSource)
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var speed = definition != null ? definition.MoveSpeed : fallbackMoveSpeed;

            foreach (var step in path)
                yield return MoveStepRoutine(GetStandingWorldPosition(step), speed, false);

            yield return MoveStepRoutine(GetStandingWorldPosition(collisionPosition), speed, false);
            yield return PlayCollisionImpactRoutine(lowerUnit);

            ReleaseImpactReservation();
            var pushed = lowerUnit != null && lowerUnit.IsAlive &&
                         lowerUnit.TryKnockbackInDirection(
                             direction, 1, suppressFallDamage: true, damageSource: damageSource ?? this);
            if (!pushed)
            {
                var upperDamage = lowerUnit != null ? lowerUnit.CurrentHealth : 0;
                var lowerDamage = CurrentHealth;
                if (lowerUnit != null && lowerUnit.IsAlive)
                    lowerUnit.TakeDamage(lowerDamage, damageSource ?? this);
                TakeDamage(upperDamage, damageSource ?? lowerUnit);
            }
            else
            {
                if (board.TryOccupy(this, collisionPosition))
                    Position = collisionPosition;
                if (fallDamage > 0 && lowerUnit.IsAlive)
                    lowerUnit.TakeDamage(fallDamage, damageSource ?? this);
                TakeDamage(fallDamage, damageSource);
            }

            if (!IsAlive)
                yield break;

            if (Position != collisionPosition)
                yield return MoveStepRoutine(GetStandingWorldPosition(Position), speed, false);

            IsMoving = false;
            unitAnimator?.PlayIdle();
            MoveCompleted?.Invoke(this);
        }

        private void ReleaseImpactReservation()
        {
            if (reservedImpactTarget != null)
                reservedImpactTarget.isImpactReserved = false;
            reservedImpactTarget = null;
        }

        private IEnumerator FallCollisionRoutine(
            IReadOnlyList<GridPosition> path,
            GridPosition collisionPosition,
            TacticalUnit lowerUnit,
            int upperDamage,
            int lowerDamage,
            TacticalUnit damageSource)
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var speed = definition != null ? definition.MoveSpeed : fallbackMoveSpeed;

            foreach (var step in path)
                yield return MoveStepRoutine(GetStandingWorldPosition(step), speed, false);

            var impactWorld = GetStandingWorldPosition(collisionPosition);
            yield return MoveStepRoutine(impactWorld, speed, false);
            yield return PlayCollisionImpactRoutine(lowerUnit);

            if (lowerUnit != null && lowerUnit.IsAlive)
                lowerUnit.TakeDamage(lowerDamage, damageSource ?? this);
            TakeDamage(upperDamage, damageSource ?? lowerUnit);

            if (!IsAlive)
                yield break;

            if ((lowerUnit == null || !lowerUnit.IsAlive) &&
                board.TryOccupy(this, collisionPosition))
            {
                Position = collisionPosition;
            }
            else
            {
                yield return MoveStepRoutine(GetStandingWorldPosition(Position), speed, false);
            }

            IsMoving = false;
            unitAnimator?.PlayIdle();
            MoveCompleted?.Invoke(this);
        }

        private IEnumerator PlayCollisionImpactRoutine(TacticalUnit lowerUnit)
        {
            if (lowerUnit == null)
                yield break;

            var upperScale = transform.localScale;
            var lowerScale = lowerUnit.transform.localScale;
            var upperPosition = transform.position;
            var lowerPosition = lowerUnit.transform.position;
            var duration = Mathf.Max(0.05f, collisionImpactDuration);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pulse = Mathf.Sin(t * Mathf.PI);
                var shake = Mathf.Sin(t * Mathf.PI * 6f) * pulse;

                transform.localScale = Vector3.Scale(
                    upperScale, new Vector3(1f + 0.16f * pulse, 1f - 0.2f * pulse, 1f));
                lowerUnit.transform.localScale = Vector3.Scale(
                    lowerScale, new Vector3(1f + 0.24f * pulse, 1f - 0.3f * pulse, 1f));
                transform.position = upperPosition + Vector3.up *
                    (board.Grid.cellSize.y * 0.06f * pulse);
                lowerUnit.transform.position = lowerPosition + Vector3.right *
                    (board.Grid.cellSize.x * 0.04f * shake);
                yield return null;
            }

            transform.localScale = upperScale;
            lowerUnit.transform.localScale = lowerScale;
            transform.position = upperPosition;
            lowerUnit.transform.position = lowerPosition;
        }

        private IEnumerator HorizontalCollisionRoutine(
            IReadOnlyList<GridPosition> path,
            GridPosition returnPosition,
            GridPosition collisionPosition,
            TacticalUnit blockingUnit,
            TacticalUnit damageSource)
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var speed = definition != null ? definition.MoveSpeed : fallbackMoveSpeed;

            foreach (var step in path)
                yield return MoveStepRoutine(GetStandingWorldPosition(step), speed, false);

            var standingWorld = GetStandingWorldPosition(returnPosition);
            var collisionWorld = GetStandingWorldPosition(collisionPosition);
            var contactWorld = Vector3.Lerp(standingWorld, collisionWorld, 0.38f);
            yield return MoveTransform(transform.position, contactWorld, 0.08f);
            yield return PlayHorizontalImpactRoutine(blockingUnit);

            ReleaseImpactReservation();
            if (blockingUnit != null && blockingUnit.IsAlive)
                blockingUnit.TakeDamage(1, damageSource ?? this);
            TakeDamage(1, damageSource ?? blockingUnit);

            if (!IsAlive)
                yield break;

            yield return MoveTransform(transform.position, standingWorld, 0.1f);
            Position = returnPosition;
            IsMoving = false;
            unitAnimator?.PlayIdle();
            MoveCompleted?.Invoke(this);
        }

        private IEnumerator PlayHorizontalImpactRoutine(TacticalUnit blockingUnit)
        {
            var moverScale = transform.localScale;
            var blockerScale = blockingUnit != null
                ? blockingUnit.transform.localScale
                : Vector3.one;
            var moverPosition = transform.position;
            var blockerPosition = blockingUnit != null
                ? blockingUnit.transform.position
                : Vector3.zero;
            var duration = Mathf.Max(0.05f, collisionImpactDuration);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pulse = Mathf.Sin(t * Mathf.PI);
                var shake = Mathf.Sin(t * Mathf.PI * 6f) * pulse;

                transform.localScale = Vector3.Scale(
                    moverScale, new Vector3(1f - 0.22f * pulse, 1f + 0.14f * pulse, 1f));
                transform.position = moverPosition + Vector3.right *
                    (board.Grid.cellSize.x * 0.035f * shake * -FacingDirection);

                if (blockingUnit != null)
                {
                    blockingUnit.transform.localScale = Vector3.Scale(
                        blockerScale, new Vector3(1f - 0.18f * pulse, 1f + 0.1f * pulse, 1f));
                    blockingUnit.transform.position = blockerPosition + Vector3.right *
                        (board.Grid.cellSize.x * 0.03f * shake * FacingDirection);
                }
                yield return null;
            }

            transform.localScale = moverScale;
            transform.position = moverPosition;
            if (blockingUnit != null)
            {
                blockingUnit.transform.localScale = blockerScale;
                blockingUnit.transform.position = blockerPosition;
            }
        }

        public void ConfigureAsCrate(
            int maxHealth = 2, bool explosive = false, int damage = 3)
        {
            isCrate = true;
            isObjective = false;
            isExplosiveCrate = explosive;
            definition = null;
            team = UnitTeam.Neutral;
            crateMaxHealth = Mathf.Max(1, maxHealth);
            explosionDamage = Mathf.Max(1, damage);
            if (!IsPlaced)
            {
                CurrentHealth = MaxHealth;
                RemainingMovement = 0;
            }
            SetHealthBarVisible(false);
        }

        public void ConfigureAsObjective(int maxHealth = 8)
        {
            isCrate = true;
            isObjective = true;
            isExplosiveCrate = false;
            definition = null;
            team = UnitTeam.Neutral;
            crateMaxHealth = Mathf.Max(1, maxHealth);
            explosionDamage = 0;
            if (!IsPlaced)
            {
                CurrentHealth = MaxHealth;
                RemainingMovement = 0;
            }
            SetHealthBarVisible(false);
        }

        private IEnumerator KnockbackMoveRoutine(
            IReadOnlyList<GridPosition> path,
            GridPosition destination,
            int fallDamage,
            TacticalUnit damageSource)
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var speed = definition != null ? definition.MoveSpeed : fallbackMoveSpeed;

            foreach (var step in path)
                yield return MoveStepRoutine(GetStandingWorldPosition(step), speed, false);

            Position = destination;
            IsMoving = false;
            unitAnimator?.PlayIdle();
            MoveCompleted?.Invoke(this);
            if (fallDamage > 0)
                TakeDamage(fallDamage, damageSource);
        }

        private IEnumerator SupportDestroyedFallRoutine(
            GridPosition destination, int fallDamage, TacticalUnit damageSource)
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var speed = definition != null ? definition.MoveSpeed : fallbackMoveSpeed;
            yield return MoveStepRoutine(GetStandingWorldPosition(destination), speed, false);
            Position = destination;
            IsMoving = false;
            unitAnimator?.PlayIdle();
            MoveCompleted?.Invoke(this);
            if (fallDamage > 0)
                TakeDamage(fallDamage, damageSource);
        }

        private IEnumerator SupportDestroyedVoidFallRoutine(TacticalUnit damageSource)
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var start = transform.position;
            var target = start + Vector3.down * (board.Grid.cellSize.y * 4f);
            yield return MoveTransform(start, target, 0.55f);
            IsMoving = false;
            TakeDamage(MaxHealth, damageSource);
        }

        private IEnumerator FallIntoVoidRoutine(IReadOnlyList<GridPosition> path, int direction, TacticalUnit damageSource)
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var speed = definition != null ? definition.MoveSpeed : fallbackMoveSpeed;
            foreach (var step in path)
                yield return MoveStepRoutine(GetStandingWorldPosition(step), speed, false);

            var start = transform.position;
            var target = start + new Vector3(
                board.Grid.cellSize.x * direction,
                -board.Grid.cellSize.y * 4f,
                0f);
            yield return MoveTransform(start, target, 0.55f);
            IsMoving = false;
            TakeDamage(MaxHealth, damageSource);
        }

        private IEnumerator MoveTransform(Vector3 from, Vector3 to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            transform.position = to;
        }

        private IEnumerator MoveStepRoutine(Vector3 target, float speed, bool updateFacing = true)
        {
            var start = transform.position;
            var distance = Vector3.Distance(start, target);
            var duration = Mathf.Max(0.05f, distance / speed);
            var elapsed = 0f;
            var heightDelta = target.y - start.y;
            var horizontalDelta = target.x - start.x;
            if (updateFacing && Mathf.Abs(horizontalDelta) > 0.01f)
                SetFacingDirection(horizontalDelta > 0f ? 1 : -1);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var horizontalT = Mathf.SmoothStep(0f, 1f, t);
                float verticalT;

                if (heightDelta > 0.01f)
                {
                    // Step up: raise vertically before completing the horizontal move.
                    verticalT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.65f));
                }
                else if (heightDelta < -0.01f)
                {
                    // Step down: move forward first, then land smoothly.
                    verticalT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f));
                }
                else
                {
                    verticalT = horizontalT;
                }

                transform.position = new Vector3(
                    Mathf.LerpUnclamped(start.x, target.x, horizontalT),
                    Mathf.LerpUnclamped(start.y, target.y, verticalT),
                    Mathf.LerpUnclamped(start.z, target.z, horizontalT));
                yield return null;
            }

            transform.position = target;
        }

        private Vector3 GetStandingWorldPosition(GridPosition position)
        {
            var cellCenter = board.PositionToWorld(position);
            var cellTop = board.Grid.cellSize.y * 0.5f;
            var localBottom = bodyCollider.offset.y - bodyCollider.size.y * 0.5f;
            var pivotToBottom = -localBottom * Mathf.Abs(transform.lossyScale.y);
            return cellCenter + Vector3.up * (cellTop + pivotToBottom);
        }

        private void OnDestroy()
        {
            ReleaseImpactReservation();
            if (previewRenderer != null && attackPreviewBlinkRoutine != null)
                previewRenderer.color = previewOriginalColor;
            if (board != null)
                board.RemoveOccupancy(this);
        }

        private void OnDisable() => ReleaseImpactReservation();
    }
}

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
        public int RemainingMovement { get; private set; }
        public int MovementPoints => definition != null ? definition.MovementPoints : fallbackMovementPoints;
        public int MaxHealth => isCrate
            ? crateMaxHealth
            : definition != null ? definition.MaxHealth : fallbackMaxHealth;
        public int AttackDamage => definition != null ? definition.AttackDamage : fallbackAttackDamage;
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
        public event Action<TacticalUnit, int, int> ExperienceChanged;
        public event Action<TacticalUnit, int> LeveledUp;
        public event Action<TacticalUnit> Died;

        private void Awake()
        {
            // Ensure a minimum click area even if Unity serialized a tiny collider before a sprite was assigned.
            bodyCollider = GetComponent<BoxCollider2D>();
            bodyRenderer = GetComponent<SpriteRenderer>();
            previewRenderer = bodyRenderer;
            ApplyUnitBodySize();
            CurrentHealth = MaxHealth;
            RemainingMovement = MovementPoints;
            healthBar = GetComponent<UnitHealthBar>();
            if (healthBar == null)
                healthBar = gameObject.AddComponent<UnitHealthBar>();
            RefreshAnimationRig();
        }

        public void Configure(UnitDefinition unitDefinition, UnitTeam unitTeam)
        {
            isCrate = false;
            isObjective = false;
            isExplosiveCrate = false;
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

        private void SetFacingDirection(int direction)
        {
            var normalized = direction < 0 ? -1 : 1;
            FacingDirection = normalized;
            var visualDirection = normalized * DefaultFacingDirection;
            unitAnimator?.SetFacing(visualDirection);
            if (animationPrefabInstance != null)
                ApplyAnimationLocalPosition();
            if (previewRenderer != null)
                previewRenderer.flipX = visualDirection < 0;
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

        public bool TryMoveTo(GridPosition destination)
        {
            if (isCrate || !IsAlive || !IsPlaced || IsMoving || IsAttacking || isImpactReserved ||
                HasAttacked && Team == UnitTeam.Player ||
                RemainingMovement <= 0 || destination == Position)
                return false;
            if (!GridPathfinder.TryFindPath(board, Position, destination, RemainingMovement, this, out var path))
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

            foreach (var step in path)
            {
                var target = GetStandingWorldPosition(step);
                if (board.IsLadderConnection(previous, step))
                    yield return MoveLadderRoutine(previous, step, target, speed);
                else
                    yield return MoveStepRoutine(target, speed);
                previous = step;
            }

            Position = destination;
            IsMoving = false;
            unitAnimator?.PlayIdle();
            MoveCompleted?.Invoke(this);
            if (fallDamage > 0)
                TakeDamage(fallDamage);
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

        public void BeginTurn()
        {
            RemainingMovement = MovementPoints;
            HasAttacked = false;
            if (IsPlaced)
                TurnStartPosition = Position;
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
            return IsPositionInAttackRange(origin, target) &&
                   (CanPierceUnits || board == null || !board.HasUnitBetween(origin, target, this));
        }

        public bool IsPositionInAttackRange(GridPosition origin, GridPosition target)
        {
            var horizontalDistance = Mathf.Abs(origin.X - target.X);
            var verticalDistance = Mathf.Abs(origin.Y - target.Y);
            var distance = Mathf.Max(horizontalDistance, verticalDistance);
            var minimumDistance = AttackDistanceRule == AttackDistanceRule.DistantOnly
                ? MinimumAttackRange
                : 0;
            return distance >= minimumDistance &&
                   horizontalDistance <= AttackRange &&
                   verticalDistance <= VerticalAttackRange;
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

            HasAttacked = true;
            if (targetPosition.X != Position.X)
                SetFacingDirection(targetPosition.X > Position.X ? 1 : -1);
            StartCoroutine(AttackRoutine(targetPosition, allowFriendlyFire, false));
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

            HasAttacked = true;
            if (targetPosition.X != Position.X)
                SetFacingDirection(targetPosition.X > Position.X ? 1 : -1);
            StartCoroutine(AttackRoutine(targetPosition, true, true));
            return true;
        }

        public void TakeDamage(int amount, TacticalUnit source = null)
        {
            if (!IsAlive || amount <= 0)
                return;

            if (source != null && source != this)
                LastDamageSource = source;
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
                board.Detonate(deathPosition, this, explosionDamage);
            Died?.Invoke(this);
            gameObject.SetActive(false);
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

        private IEnumerator AttackRoutine(
            GridPosition targetPosition, bool allowFriendlyFire, bool attackWoodOnly)
        {
            IsAttacking = true;
            var attackStartPosition = Position;
            var origin = transform.position;
            var direction = targetPosition.X == Position.X
                ? FacingDirection
                : targetPosition.X > Position.X ? 1 : -1;
            var lunge = origin + Vector3.right * (0.18f * direction);
            SetFacingDirection(direction);
            unitAnimator?.PlayAttack();

            yield return MoveTransform(origin, lunge, 0.1f);
            var areaTargets = new List<TacticalUnit>();
            if (AreaKnockbackRadius > 0 && AreaKnockbackDistance > 0)
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

            if (!attackWoodOnly &&
                board.TryGetOccupant(targetPosition, out var target) && target != this &&
                target.IsAlive && (allowFriendlyFire || target.Team != Team))
            {
                // Keep a lethally-hit target alive until knockback resolves so it can
                // still collide with a wall or another unit before dying.
                var deferLethalDamage = KnockbackDistance > 0 &&
                                        AttackDamage >= target.CurrentHealth;
                if (!deferLethalDamage)
                    target.TakeDamage(AttackDamage, this);

                if (target.IsAlive && KnockbackDistance > 0 &&
                    target.TryKnockbackFrom(this, KnockbackDistance))
                {
                    while (board.HasUnitsResolvingForcedMovement())
                        yield return null;
                }

                if (deferLethalDamage && target != null && target.IsAlive)
                    target.TakeDamage(AttackDamage, this);
            }
            else if (attackWoodOnly)
            {
                board.DamageWood(targetPosition, AttackDamage);
            }

            var startedAreaKnockback = false;
            foreach (var nearby in areaTargets)
            {
                if (nearby == null || !nearby.IsAlive || attackWoodOnly && nearby == this)
                    continue;
                var radialDirection = nearby.Position.X == targetPosition.X
                    ? FacingDirection
                    : nearby.Position.X > targetPosition.X ? 1 : -1;
                if (nearby.TryKnockbackInDirection(
                        radialDirection, AreaKnockbackDistance, allowWhileAttacking: nearby == this))
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
            return TryKnockbackInDirection(direction, distance);
        }

        public bool TryFallAfterSupportDestroyed()
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
                StartCoroutine(SupportDestroyedFallRoutine(landing, fallDamage));
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
                    blocker.isImpactReserved = true;
                    reservedImpactTarget = blocker;
                    StartCoroutine(FallPushCollisionRoutine(
                        new List<GridPosition>(), landing, blocker,
                        FacingDirection, fallDamage));
                }
                else
                {
                    StartCoroutine(FallCollisionRoutine(
                        new List<GridPosition>(), landing, blocker,
                        blocker.CurrentHealth, CurrentHealth));
                }
                return true;
            }

            board.RemoveOccupancy(this);
            StartCoroutine(SupportDestroyedVoidFallRoutine());
            return true;
        }

        public bool TryKnockbackInDirection(
            int direction, int distance, bool allowWhileAttacking = false,
            bool suppressFallDamage = false)
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
                                path, next, blocker, direction, impactFallDamage));
                            return true;
                        }

                        var upperDamage = blocker.CurrentHealth;
                        var lowerDamage = CurrentHealth;
                        StartCoroutine(FallCollisionRoutine(
                            path, next, blocker, upperDamage, lowerDamage));
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
                        path, current, horizontalImpactPosition, blocker));
                    return true;
                }

                if (result == KnockbackLandingType.Void)
                {
                    if (board != null)
                        board.RemoveOccupancy(this);
                    StartCoroutine(FallIntoVoidRoutine(path, direction));
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
            StartCoroutine(KnockbackMoveRoutine(path, current, resolvedFallDamage));
            return true;
        }

        private IEnumerator FallPushCollisionRoutine(
            IReadOnlyList<GridPosition> path,
            GridPosition collisionPosition,
            TacticalUnit lowerUnit,
            int direction,
            int fallDamage)
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
                             direction, 1, suppressFallDamage: true);
            if (!pushed)
            {
                var upperDamage = lowerUnit != null ? lowerUnit.CurrentHealth : 0;
                var lowerDamage = CurrentHealth;
                if (lowerUnit != null && lowerUnit.IsAlive)
                    lowerUnit.TakeDamage(lowerDamage, this);
                TakeDamage(upperDamage, lowerUnit);
            }
            else
            {
                if (board.TryOccupy(this, collisionPosition))
                    Position = collisionPosition;
                if (fallDamage > 0 && lowerUnit.IsAlive)
                    lowerUnit.TakeDamage(fallDamage, this);
                TakeDamage(fallDamage);
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
            int lowerDamage)
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
                lowerUnit.TakeDamage(lowerDamage, this);
            TakeDamage(upperDamage, lowerUnit);

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
            TacticalUnit blockingUnit)
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
                blockingUnit.TakeDamage(1, this);
            TakeDamage(1, blockingUnit);

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
            int fallDamage)
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
                TakeDamage(fallDamage);
        }

        private IEnumerator SupportDestroyedFallRoutine(
            GridPosition destination, int fallDamage)
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
                TakeDamage(fallDamage);
        }

        private IEnumerator SupportDestroyedVoidFallRoutine()
        {
            IsMoving = true;
            unitAnimator?.PlayMove();
            MoveStarted?.Invoke(this);
            var start = transform.position;
            var target = start + Vector3.down * (board.Grid.cellSize.y * 4f);
            yield return MoveTransform(start, target, 0.55f);
            IsMoving = false;
            TakeDamage(MaxHealth);
        }

        private IEnumerator FallIntoVoidRoutine(IReadOnlyList<GridPosition> path, int direction)
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
            TakeDamage(MaxHealth);
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

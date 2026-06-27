using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Grid;
using StellaStair.Presentation;
using StellaStair.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StellaStair.Input
{
    public sealed class TacticalInputController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private DeploymentManager deployment;
        [SerializeField] private GridHighlighter highlighter;
        [SerializeField] private LayerMask unitLayerMask = ~0;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button moveUndoButton;
        [SerializeField] private Button turnButton;

        private TacticalUnit selected;
        private IReadOnlyDictionary<GridPosition, int> reachable;
        private readonly HashSet<GridPosition> attackablePositions = new();
        private TMP_Text attackButtonLabel;
        private TMP_Text moveUndoButtonLabel;
        private TMP_Text turnButtonLabel;
        private GridPosition? pendingAttackTarget;
        private bool pendingAttackTargetsWood;
        private readonly HashSet<GridPosition> previewKnockbackDestinations = new();
        private readonly HashSet<GridPosition> previewCollisionPositions = new();
        private readonly HashSet<GridPosition> previewVoidPositions = new();
        private readonly List<TacticalUnit> previewAffectedUnits = new();
        private readonly Dictionary<TacticalUnit, int> previewDamageByUnit = new();
        private GridPosition? pendingMoveDestination;
        private int pendingMoveFallDamage;
        private DeploymentManager subscribedDeployment;

        public bool IsAttackMode { get; private set; }
        public bool CanUseAttackButton => deployment != null &&
            deployment.Phase == BattlePhase.PlayerTurn && selected != null &&
            selected.Team == UnitTeam.Player && selected.IsAlive &&
            !selected.IsMoving && !selected.HasAttacked;

        public void Configure(Camera camera, DeploymentManager manager, GridHighlighter gridHighlighter)
        {
            worldCamera = camera;
            deployment = manager;
            highlighter = gridHighlighter;
            SubscribeToEnemyIntents();
        }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera != null && worldCamera.GetComponent<TacticalCameraPan>() == null)
                worldCamera.gameObject.AddComponent<TacticalCameraPan>();
            BindSceneButtonsIfMissing();
            if (attackButton != null)
            {
                attackButton.onClick.AddListener(HandlePrimaryButton);
                attackButtonLabel = attackButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (moveUndoButton != null)
            {
                moveUndoButton.onClick.AddListener(HandleSecondaryButton);
                moveUndoButtonLabel = moveUndoButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (turnButton != null)
            {
                turnButton.onClick.AddListener(HandleTurnButton);
                turnButtonLabel = turnButton.GetComponentInChildren<TMP_Text>(true);
            }
            RefreshActionButtons();
            SubscribeToEnemyIntents();
        }

        private void BindSceneButtonsIfMissing()
        {
            if (attackButton != null && moveUndoButton != null && turnButton != null)
                return;

            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            if (buttons.Length < 3)
                return;

            Button rightmost = null;
            Button secondRightmost = null;
            Button leftmost = null;
            foreach (var button in buttons)
            {
                if (leftmost == null || GetButtonX(button) < GetButtonX(leftmost))
                    leftmost = button;
                if (rightmost == null || GetButtonX(button) > GetButtonX(rightmost))
                {
                    secondRightmost = rightmost;
                    rightmost = button;
                }
                else if (secondRightmost == null || GetButtonX(button) > GetButtonX(secondRightmost))
                {
                    secondRightmost = button;
                }
            }

            attackButton ??= rightmost;
            moveUndoButton ??= secondRightmost;
            turnButton ??= leftmost;
        }

        private static float GetButtonX(Button button)
        {
            return button.transform.position.x;
        }

        private void OnDestroy()
        {
            if (subscribedDeployment != null)
                subscribedDeployment.EnemyIntentsChanged -= OnEnemyIntentsChanged;
            if (attackButton != null)
                attackButton.onClick.RemoveListener(HandlePrimaryButton);
            if (moveUndoButton != null)
                moveUndoButton.onClick.RemoveListener(HandleSecondaryButton);
            if (turnButton != null)
                turnButton.onClick.RemoveListener(HandleTurnButton);
        }

        private void SubscribeToEnemyIntents()
        {
            if (subscribedDeployment == deployment)
                return;
            if (subscribedDeployment != null)
                subscribedDeployment.EnemyIntentsChanged -= OnEnemyIntentsChanged;
            subscribedDeployment = deployment;
            if (subscribedDeployment != null)
                subscribedDeployment.EnemyIntentsChanged += OnEnemyIntentsChanged;
        }

        private void OnEnemyIntentsChanged()
        {
            if (selected != null && selected.Team == UnitTeam.Enemy)
                RefreshSelectionDisplay();
        }

        public void ToggleAttackMode()
        {
            if (!CanUseAttackButton)
                return;

            if (!IsAttackMode)
            {
                IsAttackMode = true;
                ClearAttackPreview();
            }
            else if (pendingAttackTarget.HasValue)
            {
                var target = pendingAttackTarget.Value;
                var attacked = pendingAttackTargetsWood
                    ? selected.TryAttackWoodPosition(target)
                    : selected.TryAttackPosition(target, true);
                if (attacked)
                {
                    ClearSelection();
                    return;
                }
                ClearAttackPreview();
            }
            else
            {
                IsAttackMode = false;
            }
            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        public void HandlePrimaryButton()
        {
            if (pendingMoveDestination.HasValue)
            {
                ConfirmPendingMove();
                return;
            }
            ToggleAttackMode();
        }

        public void UndoSelectedMovement()
        {
            if (selected == null || !selected.TryUndoMovement())
                return;
            IsAttackMode = false;
            highlighter.Clear();
            RefreshActionButtons();
        }

        public void HandleSecondaryButton()
        {
            if (pendingMoveDestination.HasValue)
            {
                CancelPendingMove();
                return;
            }
            if (IsAttackMode)
            {
                CancelAttackMode();
                return;
            }
            UndoSelectedMovement();
        }

        public void HandleTurnButton()
        {
            if (deployment == null)
                return;
            if (deployment.Phase == BattlePhase.Deployment && deployment.StartBattle())
            {
                ClearSelection();
                return;
            }
            if (deployment.Phase == BattlePhase.PlayerTurn && deployment.EndPlayerTurn())
                ClearSelection();
        }

        private void CancelAttackMode()
        {
            IsAttackMode = false;
            ClearAttackPreview();
            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        private void Update()
        {
            RefreshActionButtons();
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame &&
                deployment.Phase == BattlePhase.Deployment && deployment.StartBattle())
            {
                ClearSelection();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame &&
                deployment.EndPlayerTurn())
            {
                ClearSelection();
                return;
            }

            if (Mouse.current == null || worldCamera == null)
                return;

            var cameraPan = worldCamera.GetComponent<TacticalCameraPan>();
            var leftClick = Mouse.current.leftButton.wasReleasedThisFrame &&
                            (cameraPan == null || !cameraPan.SuppressLeftClickThisFrame);
            var rightClick = Mouse.current.rightButton.wasPressedThisFrame;
            if (!leftClick && !rightClick)
                return;

            var screen = Mouse.current.position.ReadValue();
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            var world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            var cell = deployment.Board.StandingWorldToPosition(world);
            var terrainCell = deployment.Board.WorldToPosition(world);
            if (IsAttackMode)
            {
                if (deployment.Board.IsWoodTile(terrainCell))
                    cell = terrainCell;
            }

            if (rightClick)
            {
                if (pendingMoveDestination.HasValue)
                {
                    CancelPendingMove();
                }
                else if (IsAttackMode)
                {
                    if (pendingAttackTarget.HasValue)
                        ClearAttackPreview();
                    else
                        IsAttackMode = false;
                    RefreshSelectionDisplay();
                    RefreshActionButtons();
                }
                else if (deployment.Phase == BattlePhase.PlayerTurn && selected != null &&
                         selected.Team == UnitTeam.Player &&
                         selected.TryUndoMovement())
                {
                    ClearSelection();
                }
                else if (selected != null && selected.IsCrate)
                {
                    ClearSelection();
                }
                else if (selected == null)
                {
                    highlighter.Clear();
                }
                return;
            }

            var hit = Physics2D.OverlapPoint(world, unitLayerMask);
            if (IsAttackMode && deployment.Board.IsWoodTile(terrainCell) &&
                attackablePositions.Contains(terrainCell))
            {
                SelectWoodAttackPreview(terrainCell);
                return;
            }
            if (!IsAttackMode && deployment.Board.IsWoodTile(terrainCell))
            {
                ClearSelection();
                highlighter.ShowWoodHealth(deployment.Board, terrainCell);
                return;
            }
            if (hit != null && hit.TryGetComponent<TacticalUnit>(out var unit))
            {
                if (IsAttackMode && selected != null && attackablePositions.Contains(unit.Position) &&
                    unit != selected)
                {
                    SelectAttackPreview(unit.Position);
                }
                else if (!IsAttackMode)
                {
                    Select(unit);
                }
                return;
            }

            if (selected == null || selected.IsMoving)
                return;

            // Highlights and units occupy the standing cell directly above a floor tile.
            // Convert that clicked standing cell back to its logical floor position.
            if (deployment.Phase == BattlePhase.Deployment)
            {
                if (deployment.TryDeploy(selected, cell))
                    Select(selected);
            }
            else if (deployment.Phase == BattlePhase.PlayerTurn)
            {
                if (IsAttackMode && attackablePositions.Contains(cell))
                {
                    SelectAttackPreview(cell);
                }
                else if (!IsAttackMode && reachable != null && reachable.ContainsKey(cell) &&
                         cell != selected.Position)
                {
                    SelectMovePreview(cell);
                }
            }
        }

        private void Select(TacticalUnit unit)
        {
            if (deployment.Phase != BattlePhase.Deployment && deployment.Phase != BattlePhase.PlayerTurn)
            {
                ClearSelection();
                return;
            }
            if (selected != unit)
            {
                ClearMovePreview();
                if (selected != null)
                {
                    if (selected.IsCrate)
                        selected.SetHealthBarVisible(false);
                    selected.MoveCompleted -= OnSelectedMoveCompleted;
                    selected.Died -= OnSelectedDied;
                }
                unit.MoveCompleted -= OnSelectedMoveCompleted;
                unit.MoveCompleted += OnSelectedMoveCompleted;
                unit.Died -= OnSelectedDied;
                unit.Died += OnSelectedDied;
            }
            selected = unit;
            if (deployment.Phase == BattlePhase.Deployment)
            {
                if (unit.Team != UnitTeam.Player)
                {
                    ClearSelection();
                    return;
                }
                var positions = new List<GridPosition>();
                foreach (var position in deployment.Board.GetPlayerDeploymentCells())
                {
                    if (deployment.Board.CanEnter(position, unit))
                        positions.Add(position);
                }
                highlighter.Show(deployment.Board, positions, unit.IsPlaced ? unit.Position : null);
            }
            else
            {
                if (deployment.Phase != BattlePhase.PlayerTurn || !unit.IsPlaced || !unit.IsAlive) return;
                IsAttackMode = false;
                ClearAttackPreview();
                ClearMovePreview();
                RefreshSelectionDisplay();
            }
        }

        private void OnSelectedMoveCompleted(TacticalUnit unit)
        {
            if (unit != selected)
                return;
            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        private void OnSelectedDied(TacticalUnit unit)
        {
            if (unit == selected)
                ClearSelection();
        }

        private void RefreshSelectionDisplay()
        {
            if (selected == null || deployment.Phase != BattlePhase.PlayerTurn)
                return;

            if (pendingMoveDestination.HasValue)
            {
                highlighter.ShowMovePreview(deployment.Board, pendingMoveDestination.Value);
                return;
            }

            if (selected.IsCrate)
            {
                selected.SetHealthBarVisible(true);
                reachable = null;
                attackablePositions.Clear();
                highlighter.Clear();
                return;
            }

            if (selected.Team == UnitTeam.Enemy)
            {
                ClearAttackPreview();
                reachable = GridPathfinder.FindReachable(
                    deployment.Board, selected.Position, selected.MovementPoints, selected);
                attackablePositions.Clear();
                foreach (var origin in reachable.Keys)
                {
                    foreach (var position in deployment.Board.GetCellsInAttackRange(
                                 origin, selected.AttackRange, selected.VerticalAttackRange))
                    {
                        if (selected.IsPositionAttackableFrom(origin, position))
                            attackablePositions.Add(position);
                    }
                }
                if (deployment.TryGetEnemyIntent(selected, out var intent))
                {
                    if (intent.WillAttack)
                        BuildAttackPreview(
                            selected, intent.TargetPosition, intent.AttackOrigin);
                    highlighter.ShowEnemyIntentPreview(
                        deployment.Board, selected.Position,
                        intent.MoveDestination, intent.WillMove,
                        intent.TargetPosition, intent.WillAttack,
                        reachable.Keys, attackablePositions,
                        previewKnockbackDestinations,
                        previewCollisionPositions, previewVoidPositions);
                }
                else
                {
                    highlighter.Show(
                        deployment.Board, new[] { selected.Position }, selected.Position);
                }
                return;
            }

            reachable = selected.RemainingMovement <= 0 || selected.HasAttacked
                ? new Dictionary<GridPosition, int> { [selected.Position] = 0 }
                : GridPathfinder.FindReachable(
                    deployment.Board, selected.Position, selected.RemainingMovement, selected);
            attackablePositions.Clear();
            if (!selected.HasAttacked)
            {
                foreach (var position in deployment.Board.GetCellsInAttackRange(
                             selected.Position, selected.AttackRange, selected.VerticalAttackRange))
                {
                    if (selected.CanAttackPosition(position))
                        attackablePositions.Add(position);
                }
                if (deployment.Board.IsWoodTile(selected.Position))
                    attackablePositions.Add(selected.Position);
            }

            if (IsAttackMode)
            {
                if (pendingAttackTarget.HasValue)
                {
                    highlighter.ShowAttackPreview(
                        deployment.Board, selected.Position, pendingAttackTarget.Value,
                        previewKnockbackDestinations,
                        previewCollisionPositions, previewVoidPositions,
                        pendingAttackTargetsWood,
                        pendingAttackTargetsWood ? selected.AttackDamage : 0);
                }
                else
                {
                    highlighter.Show(deployment.Board,
                        new[] { selected.Position }, attackablePositions, selected.Position);
                }
            }
            else
            {
                var hasMoveDestination = false;
                foreach (var position in reachable.Keys)
                {
                    if (position == selected.Position)
                        continue;
                    hasMoveDestination = true;
                    break;
                }

                if (hasMoveDestination)
                    highlighter.Show(deployment.Board, reachable.Keys, selected.Position);
                else
                    highlighter.Clear();
            }
        }

        private void ClearSelection()
        {
            if (selected != null)
            {
                if (selected.IsCrate)
                    selected.SetHealthBarVisible(false);
                selected.MoveCompleted -= OnSelectedMoveCompleted;
                selected.Died -= OnSelectedDied;
            }
            ClearMovePreview();
            selected = null;
            reachable = null;
            IsAttackMode = false;
            ClearAttackPreview();
            attackablePositions.Clear();
            highlighter.Clear();
            RefreshActionButtons();
        }

        private void SelectMovePreview(GridPosition destination)
        {
            if (selected == null || selected.Team != UnitTeam.Player ||
                selected.IsMoving || selected.HasAttacked)
                return;
            if (!GridPathfinder.TryFindPath(
                    deployment.Board, selected.Position, destination,
                    selected.RemainingMovement, selected, out var path))
                return;

            ClearMovePreview();
            pendingMoveDestination = destination;
            pendingMoveFallDamage = selected.CalculateMovementFallDamage(path);
            selected.SetPreviewDamage(pendingMoveFallDamage);
            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        private void ConfirmPendingMove()
        {
            if (selected == null || !pendingMoveDestination.HasValue)
                return;
            var destination = pendingMoveDestination.Value;
            ClearMovePreview();
            if (selected.TryMoveTo(destination))
            {
                highlighter.Clear();
                RefreshActionButtons();
            }
            else
            {
                RefreshSelectionDisplay();
            }
        }

        private void CancelPendingMove()
        {
            ClearMovePreview();
            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        private void ClearMovePreview()
        {
            if (selected != null)
                selected.SetPreviewDamage(0);
            pendingMoveDestination = null;
            pendingMoveFallDamage = 0;
        }

        private void RefreshActionButtons()
        {
            var playerSelection = deployment != null && deployment.Phase == BattlePhase.PlayerTurn &&
                                  selected != null && selected.Team == UnitTeam.Player && selected.IsAlive;

            if (attackButton != null)
            {
                var showAttackButton = playerSelection && !selected.HasAttacked &&
                                       (pendingMoveDestination.HasValue ||
                                        !IsAttackMode || pendingAttackTarget.HasValue);
                attackButton.gameObject.SetActive(showAttackButton);
                attackButton.interactable = pendingMoveDestination.HasValue
                    ? selected != null && !selected.IsMoving
                    : CanUseAttackButton;
                if (attackButtonLabel != null)
                    attackButtonLabel.text = pendingMoveDestination.HasValue
                        ? "MOVE CONFIRM"
                        : IsAttackMode ? "ATK CONFIRM" : "ATK";
            }

            if (moveUndoButton != null)
            {
                var showMoveUndo = playerSelection && selected.HasMoved &&
                                   !selected.HasAttacked && !selected.IsMoving && !IsAttackMode;
                var showAttackCancel = playerSelection && IsAttackMode;
                var showMoveCancel = playerSelection && pendingMoveDestination.HasValue;
                moveUndoButton.gameObject.SetActive(showMoveUndo || showAttackCancel || showMoveCancel);
                moveUndoButton.interactable = showMoveCancel || showAttackCancel ||
                                              showMoveUndo && selected.CanUndoMovement;
                if (moveUndoButtonLabel != null)
                    moveUndoButtonLabel.text = showMoveCancel
                        ? "MOVE CANCEL"
                        : showAttackCancel ? "ATK CANCEL" : "Move Reset";
            }

            if (turnButton != null && deployment != null)
            {
                var deploymentPhase = deployment.Phase == BattlePhase.Deployment;
                var playerTurn = deployment.Phase == BattlePhase.PlayerTurn;
                turnButton.gameObject.SetActive(deploymentPhase || playerTurn);
                turnButton.interactable = deploymentPhase
                    ? deployment.CanStartBattle()
                    : playerTurn && !deployment.Board.HasUnitsResolvingForcedMovement();
                if (turnButtonLabel != null)
                    turnButtonLabel.text = deploymentPhase ? "DEPLOY CONFIRM" : "TURN END";
            }
        }

        private void SelectAttackPreview(GridPosition targetPosition)
        {
            if (selected == null || !attackablePositions.Contains(targetPosition))
                return;

            ClearAttackPreview();
            pendingAttackTarget = targetPosition;
            pendingAttackTargetsWood = false;

            BuildAttackPreview(selected, targetPosition);

            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        private void SelectWoodAttackPreview(GridPosition targetPosition)
        {
            if (selected == null || !attackablePositions.Contains(targetPosition) ||
                !deployment.Board.IsWoodTile(targetPosition))
                return;

            ClearAttackPreview();
            pendingAttackTarget = targetPosition;
            pendingAttackTargetsWood = true;
            BuildAttackPreview(selected, targetPosition, attackWoodOnly: true);
            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        private void BuildAttackPreview(
            TacticalUnit attacker,
            GridPosition targetPosition,
            GridPosition? attackOrigin = null,
            bool attackWoodOnly = false)
        {
            if (attacker == null)
                return;

            var origin = attackOrigin ?? attacker.Position;

            TacticalUnit affectedUnit = null;
            if (!attackWoodOnly)
            {
                if (deployment.Board.TryGetOccupant(targetPosition, out var foundUnit))
                    affectedUnit = foundUnit;
            }
            if (affectedUnit != null && affectedUnit != attacker)
            {
                AddPreviewAffectedUnit(affectedUnit);
                AddPreviewDamage(affectedUnit, attacker.AttackDamage);

                if (attacker.KnockbackDistance > 0)
                {
                    var primaryDirection = affectedUnit.Position.X == origin.X
                        ? attacker.FacingDirection
                        : affectedUnit.Position.X > origin.X ? 1 : -1;
                    PreviewKnockback(affectedUnit, primaryDirection, attacker.KnockbackDistance);
                }

            }

            if (attacker.AreaKnockbackRadius > 0 && attacker.AreaKnockbackDistance > 0)
            {
                if (attackWoodOnly)
                {
                    for (var xOffset = -1; xOffset <= 1; xOffset += 2)
                    {
                        var sidePosition = new GridPosition(
                            targetPosition.X + xOffset, targetPosition.Y - 1);
                        if (!deployment.Board.TryGetOccupant(sidePosition, out var sideUnit) ||
                            sideUnit == null || sideUnit == attacker)
                            continue;
                        AddPreviewAffectedUnit(sideUnit);
                        PreviewKnockback(
                            sideUnit, xOffset, attacker.AreaKnockbackDistance);
                    }
                }
                else
                {
                    foreach (var nearby in deployment.Board.GetOccupantsInRange(
                                 targetPosition, attacker.AreaKnockbackRadius))
                    {
                        if (nearby == affectedUnit || nearby.Position.X == targetPosition.X)
                            continue;
                        AddPreviewAffectedUnit(nearby);
                        var radialDirection = nearby.Position.X == targetPosition.X
                            ? attacker.FacingDirection
                            : nearby.Position.X > targetPosition.X ? 1 : -1;
                        PreviewKnockback(
                            nearby, radialDirection, attacker.AreaKnockbackDistance);
                    }
                }
            }
        }

        private void ClearAttackPreview()
        {
            foreach (var unit in previewAffectedUnits)
            {
                if (unit == null) continue;
                unit.SetAttackPreviewed(false);
                unit.SetPreviewDamage(0);
                if (unit.IsCrate && unit != selected)
                    unit.SetHealthBarVisible(false);
            }
            previewAffectedUnits.Clear();
            previewDamageByUnit.Clear();
            pendingAttackTarget = null;
            pendingAttackTargetsWood = false;
            previewKnockbackDestinations.Clear();
            previewCollisionPositions.Clear();
            previewVoidPositions.Clear();
        }

        private void PreviewKnockback(TacticalUnit affectedUnit, int direction, int distance)
        {
            var current = affectedUnit.Position;
            var knockbackFallDamage = 0;
            for (var i = 0; i < distance; i++)
            {
                var result = deployment.Board.ResolveKnockbackLanding(
                    current, direction, affectedUnit, out var landing,
                    out var fallDistance, out var blockingUnit);

                if (result == KnockbackLandingType.Collision)
                {
                    if (fallDistance > 0 && blockingUnit != null)
                    {
                        var pushResult = deployment.Board.ResolveKnockbackLanding(
                            blockingUnit.Position, direction, blockingUnit,
                            out var pushedLanding, out _, out _);
                        AddPreviewAffectedUnit(blockingUnit);
                        if (pushResult == KnockbackLandingType.Landing)
                        {
                            var impactFallDamage = Mathf.Min(
                                affectedUnit.MaxHealth,
                                knockbackFallDamage + Mathf.Max(0, fallDistance - 1));
                            AddPreviewDamage(
                                affectedUnit, impactFallDamage - knockbackFallDamage);
                            knockbackFallDamage = impactFallDamage;
                            AddPreviewDamage(blockingUnit, impactFallDamage);
                            previewKnockbackDestinations.Add(pushedLanding);
                            previewKnockbackDestinations.Add(landing);
                        }
                        else
                        {
                            AddPreviewDamage(blockingUnit, affectedUnit.CurrentHealth);
                            AddPreviewDamage(affectedUnit, blockingUnit.CurrentHealth);
                            previewCollisionPositions.Add(blockingUnit.Position);
                        }
                        return;
                    }

                    if (blockingUnit != null)
                    {
                        AddPreviewAffectedUnit(blockingUnit);
                        AddPreviewDamage(blockingUnit, 1);
                    }
                    AddPreviewDamage(affectedUnit, 1);
                    previewCollisionPositions.Add(blockingUnit != null
                        ? blockingUnit.Position
                        : new GridPosition(current.X + direction, current.Y));
                    return;
                }
                if (result == KnockbackLandingType.Void)
                {
                    AddPreviewDamage(affectedUnit, affectedUnit.CurrentHealth);
                    previewVoidPositions.Add(new GridPosition(current.X + direction, current.Y));
                    return;
                }

                current = landing;
                var stepFallDamage = Mathf.Max(0, fallDistance - 1);
                var cappedFallDamage = Mathf.Min(
                    affectedUnit.MaxHealth, knockbackFallDamage + stepFallDamage);
                AddPreviewDamage(affectedUnit, cappedFallDamage - knockbackFallDamage);
                knockbackFallDamage = cappedFallDamage;
                previewKnockbackDestinations.Add(current);
            }
        }

        private void AddPreviewAffectedUnit(TacticalUnit unit)
        {
            if (unit == null || previewAffectedUnits.Contains(unit))
                return;
            previewAffectedUnits.Add(unit);
            if (unit.IsCrate)
                unit.SetHealthBarVisible(true);
            unit.SetAttackPreviewed(true);
        }

        private void AddPreviewDamage(TacticalUnit unit, int damage)
        {
            if (unit == null || damage <= 0)
                return;
            AddPreviewAffectedUnit(unit);
            previewDamageByUnit.TryGetValue(unit, out var currentDamage);
            var totalDamage = currentDamage + damage;
            previewDamageByUnit[unit] = totalDamage;
            unit.SetPreviewDamage(totalDamage);
        }
    }
}

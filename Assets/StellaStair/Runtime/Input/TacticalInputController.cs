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

        private TacticalUnit selected;
        private IReadOnlyDictionary<GridPosition, int> reachable;
        private readonly HashSet<GridPosition> attackablePositions = new();
        private TMP_Text attackButtonLabel;
        private TMP_Text moveUndoButtonLabel;
        private GridPosition? pendingAttackTarget;
        private readonly HashSet<GridPosition> previewKnockbackDestinations = new();
        private readonly HashSet<GridPosition> previewCollisionPositions = new();
        private readonly HashSet<GridPosition> previewVoidPositions = new();
        private readonly List<TacticalUnit> previewAffectedUnits = new();
        private readonly Dictionary<TacticalUnit, int> previewDamageByUnit = new();
        private GridPosition? pendingMoveDestination;
        private int pendingMoveFallDamage;

        public bool IsAttackMode { get; private set; }
        public bool CanUseAttackButton => deployment != null &&
            deployment.Phase == BattlePhase.PlayerTurn && selected != null &&
            selected.IsAlive && !selected.IsMoving && !selected.HasAttacked;

        public void Configure(Camera camera, DeploymentManager manager, GridHighlighter gridHighlighter)
        {
            worldCamera = camera;
            deployment = manager;
            highlighter = gridHighlighter;
        }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
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
            RefreshActionButtons();
        }

        private void BindSceneButtonsIfMissing()
        {
            if (attackButton != null && moveUndoButton != null)
                return;

            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            if (buttons.Length < 2)
                return;

            Button rightmost = null;
            Button secondRightmost = null;
            foreach (var button in buttons)
            {
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
        }

        private static float GetButtonX(Button button)
        {
            return button.transform is RectTransform rect ? rect.anchoredPosition.x : 0f;
        }

        private void OnDestroy()
        {
            if (attackButton != null)
                attackButton.onClick.RemoveListener(HandlePrimaryButton);
            if (moveUndoButton != null)
                moveUndoButton.onClick.RemoveListener(HandleSecondaryButton);
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
                if (selected.TryAttackPosition(target, true))
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

            var leftClick = Mouse.current.leftButton.wasPressedThisFrame;
            var rightClick = Mouse.current.rightButton.wasPressedThisFrame;
            if (!leftClick && !rightClick)
                return;

            var screen = Mouse.current.position.ReadValue();
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            var world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            var cell = deployment.Board.StandingWorldToPosition(world);

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
                         selected.TryUndoMovement())
                {
                    ClearSelection();
                }
                return;
            }

            var hit = Physics2D.OverlapPoint(world, unitLayerMask);
            if (hit != null && hit.TryGetComponent<TacticalUnit>(out var unit))
            {
                if (IsAttackMode && selected != null && attackablePositions.Contains(unit.Position) &&
                    unit != selected)
                {
                    SelectAttackPreview(unit.Position);
                }
                else if (!IsAttackMode && unit.Team == UnitTeam.Player)
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
                var positions = new List<GridPosition>();
                foreach (var position in deployment.Board.GetPlayerDeploymentCells())
                {
                    if (deployment.Board.IsPlayerDeploymentCell(position) && deployment.Board.CanEnter(position, unit))
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
            }

            if (IsAttackMode)
            {
                if (pendingAttackTarget.HasValue)
                {
                    highlighter.ShowAttackPreview(
                        deployment.Board, selected.Position, pendingAttackTarget.Value,
                        previewKnockbackDestinations,
                        previewCollisionPositions, previewVoidPositions);
                }
                else
                {
                    highlighter.Show(deployment.Board,
                        new[] { selected.Position }, attackablePositions, selected.Position);
                }
            }
            else
            {
                highlighter.Show(deployment.Board, reachable.Keys, selected.Position);
            }
        }

        private void ClearSelection()
        {
            if (selected != null)
            {
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
            if (selected == null || selected.IsMoving || selected.HasAttacked)
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
        }

        private void SelectAttackPreview(GridPosition targetPosition)
        {
            if (selected == null || !attackablePositions.Contains(targetPosition))
                return;

            ClearAttackPreview();
            pendingAttackTarget = targetPosition;

            if (deployment.Board.TryGetOccupant(targetPosition, out var affectedUnit) &&
                affectedUnit != null && affectedUnit != selected)
            {
                AddPreviewAffectedUnit(affectedUnit);
                AddPreviewDamage(affectedUnit, selected.AttackDamage);

                if (selected.KnockbackDistance > 0)
                {
                    var primaryDirection = affectedUnit.Position.X == selected.Position.X
                        ? selected.FacingDirection
                        : affectedUnit.Position.X > selected.Position.X ? 1 : -1;
                    PreviewKnockback(affectedUnit, primaryDirection, selected.KnockbackDistance);
                }

            }

            if (selected.AreaKnockbackRadius > 0 && selected.AreaKnockbackDistance > 0)
            {
                foreach (var nearby in deployment.Board.GetOccupantsInRange(
                             targetPosition, selected.AreaKnockbackRadius))
                {
                    if (nearby == affectedUnit)
                        continue;
                    AddPreviewAffectedUnit(nearby);
                    var radialDirection = nearby.Position.X == targetPosition.X
                        ? selected.FacingDirection
                        : nearby.Position.X > targetPosition.X ? 1 : -1;
                    PreviewKnockback(nearby, radialDirection, selected.AreaKnockbackDistance);
                }
            }

            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        private void ClearAttackPreview()
        {
            foreach (var unit in previewAffectedUnits)
            {
                if (unit == null) continue;
                unit.SetAttackPreviewed(false);
                unit.SetPreviewDamage(0);
            }
            previewAffectedUnits.Clear();
            previewDamageByUnit.Clear();
            pendingAttackTarget = null;
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

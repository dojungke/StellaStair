using System;
using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Grid;
using StellaStair.Presentation;
using StellaStair.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
        [SerializeField] private Button attackChangeButton;
        [SerializeField] private Button moveUndoButton;
        [SerializeField] private Button turnButton;

        private TacticalUnit selected;
        private IReadOnlyDictionary<GridPosition, int> reachable;
        private readonly HashSet<GridPosition> attackablePositions = new();
        private TMP_Text attackButtonLabel;
        private TMP_Text attackChangeButtonLabel;
        private TMP_Text moveUndoButtonLabel;
        private TMP_Text turnButtonLabel;
        private GridPosition? pendingAttackTarget;
        private bool pendingAttackTargetsWood;
        private readonly HashSet<GridPosition> previewKnockbackDestinations = new();
        private readonly Dictionary<TacticalUnit, GridPosition> previewKnockbackGhostDestinations = new();
        private readonly HashSet<GridPosition> previewCollisionPositions = new();
        private readonly HashSet<GridPosition> previewVoidPositions = new();
        private readonly HashSet<GridPosition> previewEffectPositions = new();
        private readonly List<TacticalUnit> previewAffectedUnits = new();
        private readonly Dictionary<TacticalUnit, int> previewDamageByUnit = new();
        private GridPosition? pendingMoveDestination;
        private int pendingMoveFallDamage;
        private TacticalUnit facingPreviewUnit;
        private int facingPreviewOriginalDirection;
        private DeploymentManager subscribedDeployment;
        private TacticalCameraPan cameraPan;
        private Coroutine cameraFocusRoutine;
        private Vector3 cameraPositionBeforeSelection;
        private bool hasCameraPositionBeforeSelection;

        public bool IsAttackMode { get; private set; }
        public bool CanUseAttackButton => deployment != null &&
            !deployment.InteractionLocked &&
            deployment.Phase == BattlePhase.PlayerTurn && selected != null &&
            selected.Team == UnitTeam.Player && selected.IsAlive &&
            selected.CanUseCurrentAttackMode &&
            !selected.IsMoving && !selected.HasAttacked;

        public void Configure(Camera camera, DeploymentManager manager, GridHighlighter gridHighlighter)
        {
            worldCamera = camera;
            deployment = manager;
            highlighter = gridHighlighter;
            ResolveRuntimeReferences();
            BindSceneButtonsIfMissing();
            EnsureActionButtonListeners();
            SubscribeToDeploymentEvents();
            RefreshActionButtons();
        }

        private void Awake()
        {
            ResolveRuntimeReferences();
            BindSceneButtonsIfMissing();
            EnsureActionButtonListeners();
            RefreshActionButtons();
            SubscribeToDeploymentEvents();
        }

        private void EnsureActionButtonListeners()
        {
            if (attackButton != null)
            {
                attackButton.onClick.RemoveListener(HandlePrimaryButton);
                attackButton.onClick.AddListener(HandlePrimaryButton);
                attackButtonLabel = attackButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (attackChangeButton != null)
            {
                attackChangeButton.onClick.RemoveListener(HandleAttackChangeButton);
                attackChangeButton.onClick.AddListener(HandleAttackChangeButton);
                attackChangeButtonLabel = attackChangeButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (moveUndoButton != null)
            {
                moveUndoButton.onClick.RemoveListener(HandleSecondaryButton);
                moveUndoButton.onClick.AddListener(HandleSecondaryButton);
                moveUndoButtonLabel = moveUndoButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (turnButton != null)
            {
                turnButton.onClick.RemoveListener(HandleTurnButton);
                turnButton.onClick.AddListener(HandleTurnButton);
                turnButtonLabel = turnButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void ResolveRuntimeReferences()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera != null && worldCamera.GetComponent<TacticalCameraPan>() == null)
                worldCamera.gameObject.AddComponent<TacticalCameraPan>();
            if (worldCamera != null)
                cameraPan = worldCamera.GetComponent<TacticalCameraPan>();
            if (deployment == null)
                deployment = FindAnyObjectByType<DeploymentManager>();
            if (highlighter == null)
                highlighter = FindAnyObjectByType<GridHighlighter>();
        }

        private void BindSceneButtonsIfMissing()
        {
            BindNamedSceneButtons();
            if (attackButton != null && moveUndoButton != null && turnButton != null && attackChangeButton != null)
                return;

            var buttons = FindActionButtonCandidates();
            if (buttons.Count < 3)
            {
                EnsureDefaultActionButtons();
                buttons = FindActionButtonCandidates();
            }

            if (buttons.Count < 3)
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
            attackChangeButton ??= FindSceneButtonByName("Attack change Button", "Attack Change Button", "AttackChangeButton", "Change Button (1)", "Change Button");
        }

        private void BindNamedSceneButtons()
        {
            attackButton ??= FindSceneButtonByName("Attack Button");
            attackChangeButton ??= FindSceneButtonByName("Attack change Button", "Attack Change Button", "AttackChangeButton", "Change Button (1)", "Change Button");
            moveUndoButton ??= FindSceneButtonByName("Cancel Button", "Move Undo Button", "Move Reset Button");
            turnButton ??= FindSceneButtonByName("Turn Button");
        }

        private static Button FindSceneButtonByName(params string[] names)
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
            {
                if (button == null || IsLevelUpUiButton(button))
                    continue;
                foreach (var name in names)
                    if (button.name == name)
                        return button;
            }
            return null;
        }
        private static List<Button> FindActionButtonCandidates()
        {
            var result = new List<Button>();
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include))
            {
                if (button == null || IsLevelUpUiButton(button) || IsAttackChangeButton(button))
                    continue;
                result.Add(button);
            }
            return result;
        }

        private static bool IsAttackChangeButton(Button button)
        {
            return button != null &&
                   (button.name == "Attack change Button" ||
                    button.name == "Attack Change Button" ||
                    button.name == "AttackChangeButton" ||
                    button.name == "Change Button (1)" ||
                    button.name == "Change Button");
        }
        private static bool IsLevelUpUiButton(Button button)
        {
            var current = button.transform;
            while (current != null)
            {
                if (current.name == "Level Up Upgrade Overlay" ||
                    current.name == "Level Up Upgrade Panel")
                    return true;
                current = current.parent;
            }
            return false;
        }

        public static void EnsureDefaultActionButtons()
        {
            if (GameObject.Find("Runtime Action Buttons") != null)
                return;
            var canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                var canvasObject = new GameObject("StellaStair Runtime Battle UI");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }
            else if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            var eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }
            if (eventSystem.GetComponent<BaseInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            var root = new GameObject("Runtime Action Buttons", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 24f);
            rootRect.sizeDelta = new Vector2(760f, 64f);

            CreateRuntimeActionButton(rootRect, "Turn Button", "TURN END", -285f);
            CreateRuntimeActionButton(rootRect, "Cancel Button", "CANCEL", -95f);
            CreateRuntimeActionButton(rootRect, "Attack change Button", "ATTACK", 95f);
            CreateRuntimeActionButton(rootRect, "Attack Button", "ATTACK", 285f);
        }

        private static Button CreateRuntimeActionButton(
            RectTransform parent, string name, string labelText, float x)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(170f, 54f);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            var button = buttonObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.18f, 0.24f, 0.32f, 0.95f);
            colors.pressedColor = new Color(0.04f, 0.06f, 0.09f, 1f);
            colors.disabledColor = new Color(0.08f, 0.08f, 0.08f, 0.35f);
            button.colors = colors;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = labelText;
            label.fontSize = 20f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            return button;
        }

        private static float GetButtonX(Button button)
        {
            return button.transform.position.x;
        }

        private void OnDestroy()
        {
            if (subscribedDeployment != null)
            {
                subscribedDeployment.EnemyIntentsChanged -= OnEnemyIntentsChanged;
                subscribedDeployment.PhaseChanged -= OnPhaseChanged;
            }
            if (attackButton != null)
                attackButton.onClick.RemoveListener(HandlePrimaryButton);
            if (attackChangeButton != null)
                attackChangeButton.onClick.RemoveListener(HandleAttackChangeButton);
            if (moveUndoButton != null)
                moveUndoButton.onClick.RemoveListener(HandleSecondaryButton);
            if (turnButton != null)
                turnButton.onClick.RemoveListener(HandleTurnButton);
        }

        private void SubscribeToDeploymentEvents()
        {
            if (subscribedDeployment == deployment)
                return;
            if (subscribedDeployment != null)
            {
                subscribedDeployment.EnemyIntentsChanged -= OnEnemyIntentsChanged;
                subscribedDeployment.PhaseChanged -= OnPhaseChanged;
            }
            subscribedDeployment = deployment;
            if (subscribedDeployment != null)
            {
                subscribedDeployment.EnemyIntentsChanged += OnEnemyIntentsChanged;
                subscribedDeployment.PhaseChanged += OnPhaseChanged;
            }
        }

        private void OnEnemyIntentsChanged()
        {
            if (selected != null && selected.Team == UnitTeam.Enemy)
                RefreshSelectionDisplay();
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            if (phase != BattlePhase.PlayerTurn)
                ClearAttackPreview();
            RefreshSelectionDisplay();
            RefreshActionButtons();
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
                    CommitFacingPreview();
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

        public void HandleAttackChangeButton()
        {
            if (deployment != null && deployment.InteractionLocked)
                return;
            if (selected == null || !selected.HasMultipleAttackModes || selected.HasAttacked)
                return;
            if (!selected.CycleAttackMode())
                return;
            ClearAttackPreview();
            RefreshSelectionDisplay();
            RefreshActionButtons();
        }
        public void HandlePrimaryButton()
        {
            if (deployment != null && deployment.InteractionLocked)
                return;
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
            if (deployment != null && deployment.InteractionLocked)
                return;
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
            if (deployment.InteractionLocked)
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

        public void DebugLevelUpSelectedUnit()
        {
            if (selected == null || selected.Team != UnitTeam.Player || !selected.IsAlive)
                return;
            if (!selected.DebugLevelUp())
                return;

            RefreshSelectionDisplay();
            RefreshActionButtons();
        }

        private void Update()
        {
            ResolveRuntimeReferences();
            BindSceneButtonsIfMissing();
            EnsureActionButtonListeners();
            SubscribeToDeploymentEvents();
            RefreshActionButtons();
            if (deployment == null || deployment.InteractionLocked)
                return;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.lKey.wasPressedThisFrame)
                    DebugLevelUpSelectedUnit();

                var enterPressed = Keyboard.current.enterKey.wasPressedThisFrame ||
                                   Keyboard.current.numpadEnterKey.wasPressedThisFrame;
                if (deployment.Phase == BattlePhase.Deployment &&
                    (Keyboard.current.spaceKey.wasPressedThisFrame || enterPressed) && deployment.StartBattle())
                {
                    ClearSelection();
                    return;
                }

                if (deployment.Phase == BattlePhase.PlayerTurn &&
                    (Keyboard.current.spaceKey.wasPressedThisFrame || enterPressed) && deployment.EndPlayerTurn())
                {
                    ClearSelection();
                    return;
                }
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
            if (hit == null)
                hit = Physics2D.OverlapPoint(world);
            if (IsAttackMode && hit != null && hit.TryGetComponent<TacticalUnit>(out var attackTargetUnit) &&
                selected != null && attackablePositions.Contains(attackTargetUnit.Position) &&
                attackTargetUnit != selected)
            {
                SelectAttackPreview(attackTargetUnit.Position);
                return;
            }
            if (IsAttackMode && selected != null && attackablePositions.Contains(cell))
            {
                SelectAttackPreview(cell);
                return;
            }
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
                if (deployment.Phase == BattlePhase.Deployment &&
                    selected != null && selected != unit &&
                    selected.Team == UnitTeam.Player && unit.Team == UnitTeam.Player &&
                    selected.IsPlaced && unit.IsPlaced &&
                    deployment.TrySwapDeployment(selected, unit))
                {
                    ClearSelection();
                    return;
                }
                if (!IsAttackMode)
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
                    ClearSelection();
            }
            else if (deployment.Phase == BattlePhase.PlayerTurn)
            {
                if (!IsAttackMode && reachable != null && reachable.ContainsKey(cell) &&
                         cell != selected.Position)
                {
                    SelectMovePreview(cell);
                }
            }
        }

        private void FocusCameraOnUnit(TacticalUnit unit)
        {
            if (unit == null || worldCamera == null)
                return;
            cameraPan ??= worldCamera.GetComponent<TacticalCameraPan>();
            if (cameraPan == null)
                return;
            if (!hasCameraPositionBeforeSelection)
            {
                cameraPositionBeforeSelection = worldCamera.transform.position;
                hasCameraPositionBeforeSelection = true;
            }
            if (cameraFocusRoutine != null)
                StopCoroutine(cameraFocusRoutine);
            cameraFocusRoutine = unit.IsPlaced
                ? StartCoroutine(cameraPan.FocusOn(unit))
                : StartCoroutine(cameraPan.FocusOnPosition(unit.transform.position));
        }

        private void FocusCameraOnMoveDestination(GridPosition destination)
        {
            if (selected == null || worldCamera == null)
                return;
            cameraPan ??= worldCamera.GetComponent<TacticalCameraPan>();
            if (cameraPan == null)
                return;
            if (cameraFocusRoutine != null)
                StopCoroutine(cameraFocusRoutine);
            cameraFocusRoutine = StartCoroutine(cameraPan.FocusOnPosition(selected.GetPreviewStandingWorldPosition(destination)));
        }

        private void RestoreCameraAfterSelection()
        {
            if (!hasCameraPositionBeforeSelection)
                return;
            if (cameraFocusRoutine != null)
                StopCoroutine(cameraFocusRoutine);
            cameraPan ??= worldCamera != null ? worldCamera.GetComponent<TacticalCameraPan>() : null;
            if (cameraPan != null)
                cameraFocusRoutine = StartCoroutine(cameraPan.RestorePosition(cameraPositionBeforeSelection));
            hasCameraPositionBeforeSelection = false;
        }

        private void Select(TacticalUnit unit)
        {
            if (deployment.InteractionLocked)
                return;
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
                    selected.SetSelectedHighlighted(false);
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
            selected.SetSelectedHighlighted(true);
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
                    if (deployment.Board.CanEnter(position, unit) ||
                        deployment.Board.TryGetOccupant(position, out var occupant) &&
                        occupant != null && occupant != unit &&
                        occupant.Team == UnitTeam.Player)
                        positions.Add(position);
                }
                highlighter.ShowFloor(deployment.Board, positions, unit.IsPlaced ? unit.Position : null);
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
                highlighter.ShowMovePreview(deployment.Board, reachable.Keys, selected.Position, pendingMoveDestination.Value, selected);
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
                if (deployment.TryGetEnemyIntent(selected, out var intent))
                {
                    foreach (var position in selected.GetAttackTargetPositions(intent.AttackOrigin))
                        attackablePositions.Add(position);
                    if (intent.WillAttack)
                        BuildAttackPreview(
                            selected, intent.TargetPosition, intent.AttackOrigin);
                    foreach (var pair in intent.PredictedForcedDestinations)
                    {
                        previewKnockbackDestinations.Add(pair.Value);
                        SetPreviewKnockbackGhostDestination(pair.Key, pair.Value);
                        if (intent.WillAttack && pair.Value == intent.TargetPosition &&
                            pair.Key != null && pair.Key.Team != selected.Team)
                        {
                            AddPreviewAffectedUnit(pair.Key);
                            AddPreviewDamage(pair.Key, selected.BasicAttackDamage);
                        }
                    }
                    highlighter.ShowEnemyIntentPreview(
                        deployment.Board, selected.Position,
                        intent.MoveDestination, intent.WillMove,
                        intent.TargetPosition, intent.WillAttack,
                        reachable.Keys, attackablePositions,
                        previewKnockbackDestinations,
                        previewCollisionPositions, previewVoidPositions,
                        selected, previewKnockbackGhostDestinations);
                }
                else
                {
                    foreach (var position in selected.GetAttackTargetPositions(selected.Position))
                        attackablePositions.Add(position);
                    highlighter.ShowEnemyIntentPreview(
                        deployment.Board, selected.Position,
                        selected.Position, false,
                        selected.Position, false,
                        reachable.Keys, attackablePositions,
                        previewKnockbackDestinations,
                        previewCollisionPositions, previewVoidPositions,
                        selected, previewKnockbackGhostDestinations);
                }
                return;
            }

            reachable = selected.RemainingMovement <= 0 || selected.HasAttacked
                ? new Dictionary<GridPosition, int> { [selected.Position] = 0 }
                : GridPathfinder.FindReachable(
                    deployment.Board, selected.Position, selected.RemainingMovement, selected);
            attackablePositions.Clear();
            if (IsAttackMode && !selected.CanUseCurrentAttackMode)
            {
                highlighter.Clear();
                return;
            }
            if (!selected.HasAttacked && selected.CanUseCurrentAttackMode)
            {
                foreach (var position in selected.GetAttackTargetPositions(selected.Position))
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
                        pendingAttackTargetsWood ? selected.AttackDamage : 0,
                        reachable.Keys, attackablePositions,
                        previewKnockbackGhostDestinations, previewEffectPositions);
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
                selected.SetSelectedHighlighted(false);
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

        private void ApplyFacingPreview(GridPosition targetPosition)
        {
            if (selected == null || targetPosition.X == selected.Position.X)
                return;
            if (facingPreviewUnit != selected)
            {
                ClearFacingPreview();
                facingPreviewUnit = selected;
                facingPreviewOriginalDirection = selected.FacingDirection;
            }
            selected.FaceToward(targetPosition);
        }

        private void CommitFacingPreview()
        {
            facingPreviewUnit = null;
            facingPreviewOriginalDirection = 0;
        }

        private void ClearFacingPreview()
        {
            if (facingPreviewUnit != null && facingPreviewUnit.IsAlive)
                facingPreviewUnit.RestoreFacingDirection(facingPreviewOriginalDirection);
            facingPreviewUnit = null;
            facingPreviewOriginalDirection = 0;
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
            ApplyFacingPreview(destination);
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
            ClearFacingPreview();
            pendingMoveDestination = null;
            pendingMoveFallDamage = 0;
        }

        private void RefreshActionButtons()
        {
            var locked = deployment != null && deployment.InteractionLocked;
            var playerSelection = deployment != null && !locked &&
                                  deployment.Phase == BattlePhase.PlayerTurn &&
                                  selected != null && selected.Team == UnitTeam.Player && selected.IsAlive;

            if (attackButton != null)
            {
                var showAttackButton = playerSelection && !selected.HasAttacked &&
                                       (pendingMoveDestination.HasValue ||
                                        !IsAttackMode || pendingAttackTarget.HasValue);
                attackButton.gameObject.SetActive(showAttackButton);
                attackButton.interactable = pendingMoveDestination.HasValue
                    ? !locked && selected != null && !selected.IsMoving
                    : CanUseAttackButton;
                if (attackButtonLabel != null)
                    attackButtonLabel.text = pendingMoveDestination.HasValue
                        ? "MOVE CONFIRM"
                        : IsAttackMode ? "ATK CONFIRM" : "ATK";
            }

            if (attackChangeButton != null)
            {
                var showAttackChange = playerSelection && IsAttackMode && !selected.HasAttacked &&
                                       !pendingMoveDestination.HasValue &&
                                       selected.HasMultipleAttackModes;
                attackChangeButton.gameObject.SetActive(showAttackChange);
                attackChangeButton.interactable = showAttackChange && !locked && !selected.IsMoving;
                if (attackChangeButtonLabel != null && selected != null)
                    attackChangeButtonLabel.text = selected.CurrentAttackName;
            }
            if (moveUndoButton != null)
            {
                var showMoveUndo = playerSelection && selected.HasMoved &&
                                   !selected.HasAttacked && !selected.IsMoving && !IsAttackMode;
                var showAttackCancel = playerSelection && IsAttackMode;
                var showMoveCancel = playerSelection && pendingMoveDestination.HasValue;
                moveUndoButton.gameObject.SetActive(showMoveUndo || showAttackCancel || showMoveCancel);
                moveUndoButton.interactable = !locked && (showMoveCancel || showAttackCancel ||
                                              showMoveUndo && selected.CanUndoMovement);
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
                    ? !locked && deployment.CanStartBattle()
                    : playerTurn && !locked;
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
            ApplyFacingPreview(targetPosition);

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
            ApplyFacingPreview(targetPosition);
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
            if (!attackWoodOnly && attacker.IsPiercingArrowAttackPosition(origin, targetPosition))
            {
                if (attacker.HasCustomCurrentAttackModeEffectOffsets)
                {
                    foreach (var position in attacker.GetSpecialAttackEffectPositions(targetPosition))
                    {
                        previewEffectPositions.Add(position);
                        if (deployment.Board.TryGetOccupant(position, out var unit) && unit != null && unit != attacker)
                        {
                            AddPreviewAffectedUnit(unit);
                            AddPreviewDamage(unit, attacker.PiercingArrowDamage);
                        }
                    }
                    return;
                }

                var direction = GetPreviewLineDirection(origin, targetPosition);
                for (var step = 1; step <= 5; step++)
                {
                    var position = new GridPosition(origin.X + direction.x * step, origin.Y + direction.y * step);
                    previewEffectPositions.Add(position);
                    if (deployment.Board.TryGetOccupant(position, out var unit) && unit != null && unit != attacker)
                    {
                        AddPreviewAffectedUnit(unit);
                        AddPreviewDamage(unit, attacker.PiercingArrowDamage);
                    }
                }
                return;
            }

            if (!attackWoodOnly && attacker.IsBowStrikeAttackPosition(origin, targetPosition))
            {
                if (deployment.Board.TryGetOccupant(targetPosition, out var unit) && unit != null && unit != attacker)
                {
                    AddPreviewAffectedUnit(unit);
                    AddPreviewDamage(unit, attacker.BowStrikeDamage);
                    var direction = unit.Position.X == origin.X ? attacker.FacingDirection : unit.Position.X > origin.X ? 1 : -1;
                    PreviewKnockback(unit, direction, 2);
                }
                return;
            }

            if (!attackWoodOnly && attacker.IsHarpoonAttackPosition(origin, targetPosition))
            {
                if (deployment.Board.TryGetOccupant(targetPosition, out var unit) && unit != null && unit != attacker)
                {
                    AddPreviewAffectedUnit(unit);
                    AddPreviewDamage(unit, attacker.HarpoonDamage);
                    var direction = unit.Position.X > origin.X ? -1 : 1;
                    PreviewKnockback(unit, direction, 1);
                }
                return;
            }

            if (!attackWoodOnly && attacker.IsFireballAttackPosition(origin, targetPosition))
            {
                foreach (var position in attacker.GetSpecialAttackEffectPositions(targetPosition))
                {
                    previewEffectPositions.Add(position);
                    if (deployment.Board.TryGetOccupant(position, out var unit) && unit != null && unit != attacker)
                    {
                        AddPreviewAffectedUnit(unit);
                        AddPreviewDamage(unit, attacker.FireballDamage);
                    }
                }
                return;
            }

            if (!attackWoodOnly && attacker.IsIceSpikeAttackPosition(origin, targetPosition))
            {
                if (deployment.Board.TryGetOccupant(targetPosition, out var unit) && unit != null && unit != attacker)
                {
                    AddPreviewAffectedUnit(unit);
                    AddPreviewDamage(unit, attacker.IceSpikeDamage);
                }
                else
                {
                    PreviewIceBoxFall(attacker, targetPosition);
                }
                return;
            }

            if (!attackWoodOnly && attacker.IsNatureFragranceAttackPosition(origin, targetPosition))
            {
                if (deployment.Board.TryGetOccupant(targetPosition, out var unit) && unit != null && unit != attacker)
                {
                    AddPreviewAffectedUnit(unit);
                    AddPreviewHeal(unit, attacker.NatureFragranceHealAmount);
                }
                return;
            }
            if (!attackWoodOnly && attacker.IsThrustAttackPosition(origin, targetPosition))
            {
                var direction = targetPosition.X > origin.X ? 1 : -1;
                AddThrustPreviewDamage(attacker, new GridPosition(origin.X + direction, origin.Y),
                    attacker.ThrustFrontDamage, direction);
                AddThrustPreviewDamage(attacker, new GridPosition(origin.X + direction * 2, origin.Y),
                    attacker.ThrustBackDamage, direction);
                return;
            }

            TacticalUnit affectedUnit = null;
            var directlyAffectedUnits = new HashSet<TacticalUnit>();
            if (attacker.HasCustomAttackEffectOffsets)
            {
                foreach (var effectPosition in attacker.GetAttackEffectPositions(targetPosition))
                {
                    if (attacker.IsWizardDefaultAttackMode() && effectPosition != targetPosition)
                        continue;
                    previewEffectPositions.Add(effectPosition);
                    var affected = PreviewAttackEffectCell(attacker, origin, effectPosition, attackWoodOnly);
                    if (affected != null)
                    {
                        directlyAffectedUnits.Add(affected);
                        if (affectedUnit == null && affected.Position == targetPosition)
                            affectedUnit = affected;
                    }
                }
            }
            else
            {
                if (!attackWoodOnly && deployment.Board.TryGetOccupant(targetPosition, out var foundUnit))
                    affectedUnit = foundUnit;
                if (affectedUnit != null && affectedUnit != attacker)
                {
                    directlyAffectedUnits.Add(affectedUnit);
                    AddPreviewAffectedUnit(affectedUnit);
                    AddPreviewDamage(affectedUnit, attacker.BasicAttackDamage);

                    if (attacker.KnockbackDistance > 0)
                    {
                        var primaryDirection = affectedUnit.Position.X == origin.X
                            ? attacker.FacingDirection
                            : affectedUnit.Position.X > origin.X ? 1 : -1;
                        PreviewKnockback(affectedUnit, primaryDirection, attacker.KnockbackDistance);
                    }
                }
            }

            if (attacker.IsWizardDefaultAttackMode() && attacker.AreaKnockbackRadius > 0 && attacker.AreaKnockbackDistance > 0)
            {
                if (attackWoodOnly)
                {
                    for (var xOffset = -1; xOffset <= 1; xOffset += 2)
                    {
                        var sidePosition = new GridPosition(
                            targetPosition.X + xOffset, targetPosition.Y);
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
                        if (directlyAffectedUnits.Contains(nearby) || nearby.Position.X == targetPosition.X)
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


        private TacticalUnit PreviewAttackEffectCell(
            TacticalUnit attacker, GridPosition origin, GridPosition effectPosition,
            bool attackWoodOnly)
        {
            if (attackWoodOnly)
                return null;
            if (!deployment.Board.TryGetOccupant(effectPosition, out var affectedUnit) ||
                affectedUnit == null || affectedUnit == attacker)
                return null;

            AddPreviewAffectedUnit(affectedUnit);
            AddPreviewDamage(affectedUnit, attacker.BasicAttackDamage);
            if (attacker.KnockbackDistance > 0)
            {
                var direction = affectedUnit.Position.X == origin.X
                    ? attacker.FacingDirection
                    : affectedUnit.Position.X > origin.X ? 1 : -1;
                PreviewKnockback(affectedUnit, direction, attacker.KnockbackDistance);
            }
            return affectedUnit;
        }
        private static Vector2Int GetPreviewLineDirection(GridPosition origin, GridPosition target)
        {
            var dx = Math.Sign(target.X - origin.X);
            var dy = Math.Sign(target.Y - origin.Y);
            if (Mathf.Abs(target.X - origin.X) >= Mathf.Abs(target.Y - origin.Y))
                dy = 0;
            else
                dx = 0;
            return new Vector2Int(dx, dy);
        }
        private void PreviewIceBoxFall(TacticalUnit attacker, GridPosition spawnPosition)
        {
            if (attacker == null || deployment == null || deployment.Board == null)
                return;

            var result = deployment.Board.ResolveVerticalFall(
                spawnPosition, attacker, out var landing, out var fallDistance, out var blockingUnit);
            if (result != KnockbackLandingType.Collision || blockingUnit == null || !blockingUnit.IsAlive)
                return;

            AddPreviewAffectedUnit(blockingUnit);
            var fallDamage = Mathf.Min(1, Mathf.Max(0, fallDistance - 1));
            var direction = attacker.FacingDirection;
            var pushResult = deployment.Board.ResolveKnockbackLanding(
                blockingUnit.Position, direction, blockingUnit, out var pushedLanding, out _, out _);

            if (pushResult == KnockbackLandingType.Landing)
            {
                previewKnockbackDestinations.Add(pushedLanding);
                SetPreviewKnockbackGhostDestination(blockingUnit, pushedLanding);
                if (fallDamage > 0)
                    AddPreviewDamage(blockingUnit, fallDamage);
                return;
            }

            AddPreviewDamage(blockingUnit, 1);
            previewCollisionPositions.Add(blockingUnit.Position);
        }
        private void AddThrustPreviewDamage(
            TacticalUnit attacker, GridPosition position, int damage, int direction)
        {
            if (attacker == null || damage <= 0)
                return;
            if (!deployment.Board.TryGetOccupant(position, out var unit) ||
                unit == null || unit == attacker || unit.Team == attacker.Team)
                return;

            AddPreviewAffectedUnit(unit);
            AddPreviewDamage(unit, damage);
            if (attacker.ThrustHasKnockback)
                PreviewKnockback(unit, direction, 1);
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
            ClearFacingPreview();
            pendingAttackTarget = null;
            pendingAttackTargetsWood = false;
            previewKnockbackDestinations.Clear();
            previewKnockbackGhostDestinations.Clear();
            previewCollisionPositions.Clear();
            previewVoidPositions.Clear();
            previewEffectPositions.Clear();
        }

        private void PreviewKnockback(TacticalUnit affectedUnit, int direction, int distance)
        {
            if (affectedUnit == null || affectedUnit.IsObjective || !affectedUnit.IsAlive || distance <= 0)
                return;

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
                            SetPreviewKnockbackGhostDestination(blockingUnit, pushedLanding);
                            SetPreviewKnockbackGhostDestination(affectedUnit, landing);
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
                SetPreviewKnockbackGhostDestination(affectedUnit, current);
            }
        }

        private void SetPreviewKnockbackGhostDestination(TacticalUnit unit, GridPosition destination)
        {
            if (unit == null || !unit.IsAlive)
                return;
            previewKnockbackGhostDestinations[unit] = destination;
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

        private void AddPreviewHeal(TacticalUnit unit, int amount)
        {
            if (unit == null || amount <= 0)
                return;
            var healAmount = Mathf.Min(amount, unit.MaxHealth - unit.CurrentHealth);
            if (healAmount <= 0)
                return;
            AddPreviewAffectedUnit(unit);
            unit.SetPreviewDamage(-healAmount);
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

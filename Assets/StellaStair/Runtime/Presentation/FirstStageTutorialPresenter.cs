using System.Collections.Generic;
using StellaStair.Battle;
using StellaStair.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StellaStair.Presentation
{
    public sealed class FirstStageTutorialPresenter : MonoBehaviour
    {
        private const string DeployPrompt = "\uCE90\uB9AD\uD130\uB97C \uD074\uB9AD\uD574 \uBC30\uCE58\uD558\uC138\uC694";
        private const string DeploymentConfirmPrompt = "\uBC30\uCE58 \uC644\uB8CC\uD558\uBA74 \uBC30\uCE58 \uD655\uC815 \uBC84\uD2BC\uC744 \uB204\uB974\uC138\uC694";
        private const string ActionPrompt = "\uCE90\uB9AD\uD130\uB97C \uD074\uB9AD\uD574 \uD589\uB3D9\uD558\uC138\uC694";
        private const string EndTurnPrompt = "\uD589\uB3D9\uC744 \uC644\uB8CC \uD588\uB2E4\uBA74 \uD134\uC885\uB8CC \uBC84\uD2BC\uC744 \uB20C\uB7EC \uD134\uC744 \uC885\uB8CC\uD558\uC138\uC694";
        private const string HerbPrompt = "\uC57D\uCD08\uB97C \uACF5\uACA9\uD574 \uCC44\uC9D1\uD558\uC138\uC694";

        [SerializeField] private Vector2 size = new(780f, 54f);
        [SerializeField] private float topMargin = 18f;
        [SerializeField] private TMP_Text promptLabel;

        private readonly HashSet<TacticalUnit> observedPlayers = new();
        private DeploymentManager battle;
        private GameObject promptRoot;
        private string currentPrompt = DeployPrompt;
        private bool playerActionObserved;
        private bool playerEndedTurn;
        private bool visible;

        public void Configure(DeploymentManager manager)
        {
            if (battle == manager)
            {
                RefreshObservedPlayers();
                RefreshPrompt();
                EnsureSceneUi();
                ApplyPromptToSceneUi();
                return;
            }

            Unsubscribe();
            battle = manager;
            if (battle != null)
            {
                battle.PhaseChanged += OnPhaseChanged;
                battle.PlayerTurnStarted += OnPlayerTurnStarted;
            }
            RefreshObservedPlayers();
            RefreshPrompt();
            EnsureSceneUi();
            ApplyPromptToSceneUi();
        }

        public void ShowTutorial()
        {
            visible = true;
            EnsureSceneUi();
            RefreshPrompt();
            ApplyPromptToSceneUi();
        }

        private void Start()
        {
            if (battle == null)
                Configure(FindAnyObjectByType<DeploymentManager>());
            EnsureSceneUi();
            ApplyPromptToSceneUi();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            RefreshObservedPlayers();
            RefreshPrompt();
            EnsureSceneUi();
            ApplyPromptToSceneUi();
        }

        private void EnsureSceneUi()
        {
            if (promptLabel != null)
                return;

            foreach (var label in FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label != null && label.name == "First Stage Tutorial Text")
                {
                    promptLabel = label;
                    promptRoot = label.transform.parent != null
                        ? label.transform.parent.gameObject
                        : label.gameObject;
                    ConfigurePromptRect();
                    return;
                }
            }

            var canvasObject = new GameObject(
                "First Stage Tutorial Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 320;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            promptRoot = new GameObject(
                "First Stage Tutorial Banner",
                typeof(RectTransform), typeof(Image));
            promptRoot.transform.SetParent(canvasObject.transform, false);
            var image = promptRoot.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.62f);
            image.raycastTarget = false;

            var textObject = new GameObject("First Stage Tutorial Text", typeof(RectTransform));
            textObject.transform.SetParent(promptRoot.transform, false);
            promptLabel = textObject.AddComponent<TextMeshProUGUI>();
            try
            {
                if (TMP_Settings.defaultFontAsset != null)
                    promptLabel.font = TMP_Settings.defaultFontAsset;
            }
            catch (System.NullReferenceException)
            {
            }
            promptLabel.fontSize = 30f;
            promptLabel.fontStyle = FontStyles.Normal;
            promptLabel.alignment = TextAlignmentOptions.Center;
            promptLabel.color = Color.white;
            promptLabel.raycastTarget = false;
            promptLabel.enableWordWrapping = false;

            ConfigurePromptRect();
        }

        private void ConfigurePromptRect()
        {
            if (promptRoot != null && promptRoot.TryGetComponent<RectTransform>(out var rootRect))
            {
                rootRect.anchorMin = new Vector2(0.5f, 1f);
                rootRect.anchorMax = new Vector2(0.5f, 1f);
                rootRect.pivot = new Vector2(0.5f, 1f);
                rootRect.anchoredPosition = new Vector2(0f, -topMargin);
                rootRect.sizeDelta = size;
            }

            if (promptLabel != null)
            {
                var labelRect = promptLabel.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(18f, 0f);
                labelRect.offsetMax = new Vector2(-18f, 0f);
            }
        }

        private void ApplyPromptToSceneUi()
        {
            if (promptLabel == null)
                return;

            promptLabel.text = currentPrompt;
            var shouldShow = visible && battle != null &&
                             battle.Phase != BattlePhase.Victory &&
                             battle.Phase != BattlePhase.Defeat;
            if (promptRoot != null)
                promptRoot.SetActive(shouldShow);
            else
                promptLabel.gameObject.SetActive(shouldShow);
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            if (phase == BattlePhase.EnemyTurn && playerActionObserved)
                playerEndedTurn = true;
            RefreshPrompt();
            ApplyPromptToSceneUi();
        }

        private void OnPlayerTurnStarted(int turnNumber)
        {
            if (playerEndedTurn || turnNumber > 1)
            {
                playerEndedTurn = true;
                currentPrompt = HerbPrompt;
            }
            RefreshPrompt();
            ApplyPromptToSceneUi();
        }

        private void RefreshObservedPlayers()
        {
            if (battle == null)
                return;

            foreach (var unit in battle.PlayerUnits)
            {
                if (unit == null || observedPlayers.Contains(unit))
                    continue;
                observedPlayers.Add(unit);
                unit.MoveCompleted += OnPlayerMoveCompleted;
                unit.AttackUsed += OnPlayerAttackUsed;
            }
        }

        private void OnPlayerMoveCompleted(TacticalUnit unit)
        {
            if (unit != null && unit.Team == UnitTeam.Player)
                playerActionObserved = true;
            RefreshPrompt();
            ApplyPromptToSceneUi();
        }

        private void OnPlayerAttackUsed(TacticalUnit unit, string skillKey)
        {
            if (unit != null && unit.Team == UnitTeam.Player)
                playerActionObserved = true;
            RefreshPrompt();
            ApplyPromptToSceneUi();
        }

        private void RefreshPrompt()
        {
            if (battle == null)
                return;

            if (battle.Phase == BattlePhase.Deployment)
            {
                currentPrompt = AreAllPlayersPlaced() ? DeploymentConfirmPrompt : DeployPrompt;
                return;
            }

            if (playerEndedTurn)
            {
                currentPrompt = HerbPrompt;
                return;
            }

            if (battle.Phase == BattlePhase.PlayerTurn)
            {
                currentPrompt = playerActionObserved || HasAnyPlayerActed()
                    ? EndTurnPrompt
                    : ActionPrompt;
                return;
            }

            if (battle.Phase == BattlePhase.EnemyTurn)
                currentPrompt = playerActionObserved ? HerbPrompt : ActionPrompt;
        }

        private bool AreAllPlayersPlaced()
        {
            if (battle == null || battle.PlayerUnits.Count == 0)
                return false;
            foreach (var unit in battle.PlayerUnits)
                if (unit == null || !unit.IsPlaced)
                    return false;
            return true;
        }

        private bool HasAnyPlayerActed()
        {
            if (battle == null)
                return false;
            foreach (var unit in battle.PlayerUnits)
                if (unit != null && unit.Team == UnitTeam.Player &&
                    (unit.HasMoved || unit.HasAttacked))
                    return true;
            return false;
        }

        private void Unsubscribe()
        {
            if (battle != null)
            {
                battle.PhaseChanged -= OnPhaseChanged;
                battle.PlayerTurnStarted -= OnPlayerTurnStarted;
            }

            foreach (var unit in observedPlayers)
            {
                if (unit == null)
                    continue;
                unit.MoveCompleted -= OnPlayerMoveCompleted;
                unit.AttackUsed -= OnPlayerAttackUsed;
            }
            observedPlayers.Clear();
            battle = null;
        }
    }
}
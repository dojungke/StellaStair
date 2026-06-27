using System.Collections;
using System.Collections.Generic;
using StellaStair.Grid;
using StellaStair.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellaStair.Battle
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class StageProgression : MonoBehaviour
    {
        [SerializeField] private List<TacticalMapData> registeredStages = new();
        [SerializeField] private BattleUiData commonBattleUi;
        [SerializeField] private bool avoidImmediateRepeat = true;
        [SerializeField] private bool autoAdvanceOnVictory = true;
        [SerializeField, Min(0f)] private float advanceDelay = 1.5f;

        private static string lastStageName;
        private DeploymentManager battle;
        private bool advancing;

        public TacticalMapData CurrentStage { get; private set; }
        public IReadOnlyList<TacticalMapData> RegisteredStages => registeredStages;

        private void Awake()
        {
            var board = FindAnyObjectByType<TacticalBoard>();
            CurrentStage = SelectRandomStage();
            if (CurrentStage != null && board != null)
            {
                CurrentStage.ApplyTo(board);
                lastStageName = CurrentStage.name;
                Debug.Log($"Stage entered: {CurrentStage.name}");
            }
            if (commonBattleUi != null)
                commonBattleUi.ApplyToCurrentScene();
        }

        private void Start()
        {
            battle = FindAnyObjectByType<DeploymentManager>();
            if (battle != null)
                battle.ConfigureStage(
                    CurrentStage != null ? CurrentStage.stageType : TacticalStageType.Elimination,
                    CurrentStage != null ? CurrentStage.defenseTurnsToSurvive : 5);
            if (battle != null)
                battle.PhaseChanged += OnPhaseChanged;
        }

        private TacticalMapData SelectRandomStage()
        {
            var available = new List<TacticalMapData>();
            foreach (var stage in registeredStages)
            {
                if (stage == null)
                    continue;
                if (avoidImmediateRepeat && registeredStages.Count > 1 &&
                    stage.name == lastStageName)
                    continue;
                available.Add(stage);
            }
            if (available.Count == 0)
                foreach (var stage in registeredStages)
                    if (stage != null)
                        available.Add(stage);
            return available.Count > 0 ? available[Random.Range(0, available.Count)] : null;
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            if (phase == BattlePhase.Victory && autoAdvanceOnVictory && !advancing)
                StartCoroutine(AdvanceRoutine());
        }

        private IEnumerator AdvanceRoutine()
        {
            advancing = true;
            if (advanceDelay > 0f)
                yield return new WaitForSecondsRealtime(advanceDelay);
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }

        private void OnDestroy()
        {
            if (battle != null)
                battle.PhaseChanged -= OnPhaseChanged;
        }
    }
}

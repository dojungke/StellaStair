using System.Collections;
using StellaStair.Grid;
using StellaStair.Battle;
using StellaStair.Units;
using UnityEngine;

namespace StellaStair.Presentation
{
    [DisallowMultipleComponent]
    public sealed class BattleReactiveDialogueController : MonoBehaviour
    {
        [SerializeField] private TacticalDialogueDatabase dialogueDatabase;
        [SerializeField] private BattleSpeechBubblePresenter speechBubblePresenter;
        [SerializeField, Min(0.5f)] private float bubbleDuration = 2.2f;
        [SerializeField, Min(0f)] private float sameSpeakerCooldown = 0.35f;
        [SerializeField, Min(0.01f)] private float focusDuration = 0.35f;

        private bool dialogueSequenceBusy;

        private DeploymentManager deployment;
        private StageProgression stageProgression;
        private TacticalUnit lastSpeaker;
        private float lastSpeakTime = -999f;
        private float lastDialogueDuration = 2.2f;

        public void Configure(DeploymentManager manager)
        {
            if (deployment == manager)
                return;

            Unsubscribe();
            deployment = manager;
            if (deployment != null)
            {
                deployment.EnemyKilled += OnEnemyKilled;
                deployment.BeforeLevelUp += OnBeforeLevelUp;
                deployment.UnitHealed += OnUnitHealed;
                deployment.BeforeAttack += OnBeforeAttack;
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (deployment == null)
                return;

            deployment.EnemyKilled -= OnEnemyKilled;
            deployment.BeforeLevelUp -= OnBeforeLevelUp;
            deployment.UnitHealed -= OnUnitHealed;
            deployment.BeforeAttack -= OnBeforeAttack;
            deployment = null;
        }

        private void OnEnemyKilled(TacticalUnit killer, TacticalUnit deadUnit, string skillKey)
        {
            if (killer == null || killer.Team != UnitTeam.Player)
                return;
            StartCoroutine(SpeakAfterAttackRoutine(killer, skillKey));
        }

        private IEnumerator SpeakAfterAttackRoutine(TacticalUnit killer, string skillKey)
        {
            while (killer != null && killer.IsAttacking)
                yield return null;
            yield return PlayDialogueSequence(killer, TacticalDialogueTiming.EnemyKilled, skillKey, false);
        }

        private IEnumerator OnBeforeAttack(TacticalUnit attacker, string skillKey, GridPosition targetPosition)
        {
            if (attacker == null || attacker.Team != UnitTeam.Player ||
                string.IsNullOrWhiteSpace(skillKey))
                yield break;
            yield return PlayDialogueSequence(attacker, TacticalDialogueTiming.SkillUsed, skillKey, false);
        }

        private IEnumerator OnBeforeLevelUp(TacticalUnit unit)
        {
            if (unit == null || unit.Team != UnitTeam.Player)
                yield break;
            while (unit.IsAttacking)
                yield return null;
            yield return PlayDialogueSequence(unit, TacticalDialogueTiming.LevelUp, null, true);
        }

        private void OnUnitHealed(TacticalUnit target, TacticalUnit source, int amount)
        {
            if (amount <= 0 || target == null || target.Team != UnitTeam.Player)
                return;
            if (source == null || source.Team != UnitTeam.Player || source == target)
                return;
            StartCoroutine(PlayDialogueSequence(source, TacticalDialogueTiming.AllyHealed, null, false));
        }

        private IEnumerator PlayDialogueSequence(TacticalUnit speaker, TacticalDialogueTiming timing, string skillKey, bool focus)
        {
            while (dialogueSequenceBusy)
                yield return null;

            dialogueSequenceBusy = true;
            try
            {
                if (focus)
                    yield return FocusOnUnitRoutine(speaker);
                if (Speak(speaker, timing, skillKey))
                    yield return new WaitForSecondsRealtime(lastDialogueDuration);
            }
            finally
            {
                dialogueSequenceBusy = false;
            }
        }

        private IEnumerator FocusOnUnitRoutine(TacticalUnit unit)
        {
            if (unit == null)
                yield break;
            var camera = Camera.main ?? FindAnyObjectByType<Camera>();
            if (camera == null)
                yield break;
            var pan = camera.GetComponent<TacticalCameraPan>();
            if (pan == null)
                pan = camera.gameObject.AddComponent<TacticalCameraPan>();
            yield return pan.FocusOn(unit, focusDuration);
        }

        private bool Speak(TacticalUnit speaker, TacticalDialogueTiming timing, string skillKey = null)
        {
            if (speaker == null || !speaker.IsAlive)
                return false;
            if (lastSpeaker == speaker && Time.time - lastSpeakTime < sameSpeakerCooldown)
                return false;

            EnsureDependencies();
            var stage = stageProgression != null ? stageProgression.CurrentStage : null;
            var line = dialogueDatabase != null
                ? dialogueDatabase.GetNextLine(stage, timing, GetSpeakerKey(speaker), skillKey, timing == TacticalDialogueTiming.SkillUsed, true)
                : null;
            lastDialogueDuration = line != null && line.duration > 0f ? line.duration : bubbleDuration;
            var text = line != null && !string.IsNullOrWhiteSpace(line.text)
                ? line.text
                : GetFallbackText(timing);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            speechBubblePresenter?.Show(speaker, text, lastDialogueDuration);
            lastSpeaker = speaker;
            lastSpeakTime = Time.time;
            return true;
        }

        private void EnsureDependencies()
        {
            if (stageProgression == null)
                stageProgression = FindAnyObjectByType<StageProgression>();
            if (dialogueDatabase == null)
                dialogueDatabase = Resources.Load<TacticalDialogueDatabase>("TacticalDialogueDatabase");
            if (speechBubblePresenter == null)
                speechBubblePresenter = FindAnyObjectByType<BattleSpeechBubblePresenter>();
            if (speechBubblePresenter == null)
                speechBubblePresenter = gameObject.AddComponent<BattleSpeechBubblePresenter>();
        }

        private static string GetSpeakerKey(TacticalUnit unit)
        {
            if (unit == null)
                return string.Empty;

            var primary = unit.Definition != null && !string.IsNullOrWhiteSpace(unit.Definition.DisplayName)
                ? unit.Definition.DisplayName
                : !string.IsNullOrWhiteSpace(unit.ProgressKey)
                    ? unit.ProgressKey
                    : unit.name;
            var alias = GetSpeakerAlias(primary);
            return string.IsNullOrWhiteSpace(alias) ? primary : primary + "|" + alias;
        }

        private static string GetSpeakerAlias(string speakerKey)
        {
            return speakerKey switch
            {
                "Wizard" => "마법사",
                "Knight" => "기사",
                "Archer" => "궁수",
                _ => string.Empty
            };
        }

        private static string GetFallbackText(TacticalDialogueTiming timing)
        {
            return timing switch
            {
                TacticalDialogueTiming.EnemyKilled => "\uD574\uB0C8\uC5B4.",
                TacticalDialogueTiming.LevelUp => "\uB354 \uAC15\uD574\uC84C\uC5B4.",
                TacticalDialogueTiming.AllyHealed => "\uC774\uC81C \uAD1C\uCC2E\uC744 \uAC70\uC57C.",
                TacticalDialogueTiming.SkillUsed => string.Empty,
                _ => string.Empty
            };
        }
    }
}
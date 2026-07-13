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

        private DeploymentManager deployment;
        private StageProgression stageProgression;
        private TacticalUnit lastSpeaker;
        private float lastSpeakTime = -999f;

        public void Configure(DeploymentManager manager)
        {
            if (deployment == manager)
                return;

            Unsubscribe();
            deployment = manager;
            if (deployment != null)
            {
                deployment.EnemyKilled += OnEnemyKilled;
                deployment.LevelUpUpgradeSelected += OnLevelUpUpgradeSelected;
                deployment.UnitHealed += OnUnitHealed;
                deployment.SkillUsed += OnSkillUsed;
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
            deployment.LevelUpUpgradeSelected -= OnLevelUpUpgradeSelected;
            deployment.UnitHealed -= OnUnitHealed;
            deployment.SkillUsed -= OnSkillUsed;
            deployment = null;
        }

        private void OnEnemyKilled(TacticalUnit killer, TacticalUnit deadUnit, string skillKey)
        {
            if (killer == null || killer.Team != UnitTeam.Player)
                return;
            Speak(killer, TacticalDialogueTiming.EnemyKilled, skillKey);
        }

        private void OnLevelUpUpgradeSelected(TacticalUnit unit)
        {
            Speak(unit, TacticalDialogueTiming.LevelUp);
        }

        private void OnSkillUsed(TacticalUnit unit, string skillKey)
        {
            if (unit == null || unit.Team != UnitTeam.Player || string.IsNullOrWhiteSpace(skillKey))
                return;

            Speak(unit, TacticalDialogueTiming.SkillUsed, skillKey);
        }

        private void OnUnitHealed(TacticalUnit target, TacticalUnit source, int amount)
        {
            if (amount <= 0 || target == null || target.Team != UnitTeam.Player)
                return;
            if (source == null || source.Team != UnitTeam.Player || source == target)
                return;

            Speak(source, TacticalDialogueTiming.AllyHealed);
        }

        private void Speak(TacticalUnit speaker, TacticalDialogueTiming timing, string skillKey = null)
        {
            if (speaker == null || !speaker.IsAlive)
                return;
            if (lastSpeaker == speaker && Time.time - lastSpeakTime < sameSpeakerCooldown)
                return;

            EnsureDependencies();
            var stage = stageProgression != null ? stageProgression.CurrentStage : null;
            var line = dialogueDatabase != null
                ? dialogueDatabase.GetRandomLine(stage, timing, GetSpeakerKey(speaker), skillKey, timing == TacticalDialogueTiming.SkillUsed, true)
                : null;
            var text = line != null && !string.IsNullOrWhiteSpace(line.text)
                ? line.text
                : GetFallbackText(timing);
            if (string.IsNullOrWhiteSpace(text))
                return;

            speechBubblePresenter?.Show(speaker, text, bubbleDuration);
            lastSpeaker = speaker;
            lastSpeakTime = Time.time;
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
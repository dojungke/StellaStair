using System;
using System.Collections.Generic;
using StellaStair.Grid;
using UnityEngine;

namespace StellaStair.Presentation
{
    public enum TacticalDialogueTiming
    {
        BeforeBattle,
        AfterVictory,
        AfterDefeat,
        EnemyKilled,
        LevelUp,
        AllyHealed,
        SkillUsed
    }

    public enum TacticalDialoguePortraitMode
    {
        Empty,
        Normal,
        Dark
    }

    [CreateAssetMenu(menuName = "Stella Stair/Tactical Dialogue Database", fileName = "TacticalDialogueDatabase")]
    public sealed class TacticalDialogueDatabase : ScriptableObject
    {
        [Serializable]
        public sealed class CharacterPortrait
        {
            public string characterId = string.Empty;
            public Sprite normalSprite;
        }

        [Serializable]
        public sealed class Line
        {
            public string stageKey = string.Empty;
            public TacticalDialogueTiming timing = TacticalDialogueTiming.BeforeBattle;
            [Min(0.1f)] public float duration = 2.2f;
            [Min(0)] public int order;
            public string speakerName = string.Empty;
            public string skillKey = string.Empty;
            [TextArea(2, 5)] public string text = string.Empty;
            public string leftCharacterId = string.Empty;
            public TacticalDialoguePortraitMode leftPortrait = TacticalDialoguePortraitMode.Empty;
            public string rightCharacterId = string.Empty;
            public TacticalDialoguePortraitMode rightPortrait = TacticalDialoguePortraitMode.Empty;
        }

        [SerializeField] private List<CharacterPortrait> characters = new();
        [SerializeField] private List<Line> lines = new();

        public IReadOnlyList<CharacterPortrait> Characters => characters;
        public IReadOnlyList<Line> Lines => lines;

        public void ReplaceLines(IEnumerable<Line> source)
        {
            lines.Clear();
            if (source == null)
                return;
            lines.AddRange(source);
            lines.Sort((a, b) =>
            {
                var stage = string.Compare(a?.stageKey, b?.stageKey, StringComparison.OrdinalIgnoreCase);
                if (stage != 0)
                    return stage;
                var timing = (a?.timing ?? 0).CompareTo(b?.timing ?? 0);
                if (timing != 0)
                    return timing;
                return (a?.order ?? 0).CompareTo(b?.order ?? 0);
            });
        }

        public List<Line> GetLines(TacticalMapData stage, TacticalDialogueTiming timing)
        {
            var result = new List<Line>();
            foreach (var line in lines)
            {
                if (line == null || line.timing != timing || !MatchesStage(line.stageKey, stage))
                    continue;
                result.Add(line);
            }
            result.Sort((a, b) => a.order.CompareTo(b.order));
            return result;
        }

        public Line GetRandomLine(TacticalMapData stage, TacticalDialogueTiming timing, string speakerKey = null, string skillKey = null, bool requireSkillMatch = false, bool requireSpeakerMatch = false)
        {
            var candidates = GetLines(stage, timing);
            if (candidates.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(skillKey))
            {
                var skillMatches = new List<Line>();
                foreach (var line in candidates)
                {
                    if (MatchesSkill(line, skillKey))
                        skillMatches.Add(line);
                }

                if (skillMatches.Count > 0)
                    candidates = skillMatches;
                else if (requireSkillMatch)
                    return null;
            }

            if (!string.IsNullOrWhiteSpace(speakerKey))
            {
                var speakerMatches = new List<Line>();
                foreach (var line in candidates)
                {
                    if (MatchesSpeaker(line, speakerKey))
                        speakerMatches.Add(line);
                }

                if (speakerMatches.Count > 0)
                    candidates = speakerMatches;
                else if (requireSpeakerMatch)
                    return null;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        public bool TryGetPortrait(string characterId, TacticalDialoguePortraitMode mode, out Sprite sprite)
        {
            sprite = null;
            if (mode == TacticalDialoguePortraitMode.Empty || string.IsNullOrWhiteSpace(characterId))
                return false;

            foreach (var character in characters)
            {
                if (character == null || !string.Equals(character.characterId, characterId.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;
                sprite = character.normalSprite;
                return sprite != null;
            }
            return false;
        }

        private static bool MatchesStage(string key, TacticalMapData stage)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Trim() == "*")
                return true;
            if (stage == null)
                return false;

            var normalized = key.Trim();
            return string.Equals(normalized, stage.name, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, stage.mapName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, stage.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesSkill(Line line, string skillKey)
        {
            if (line == null || string.IsNullOrWhiteSpace(skillKey))
                return false;
            if (string.IsNullOrWhiteSpace(line.skillKey))
                return false;

            return string.Equals(skillKey.Trim(), line.skillKey.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        private static bool MatchesSpeaker(Line line, string speakerKey)
        {
            if (line == null || string.IsNullOrWhiteSpace(speakerKey))
                return false;

            var speakerName = line.speakerName?.Trim();
            if (string.IsNullOrWhiteSpace(speakerName))
                return false;

            foreach (var key in speakerKey.Split('|'))
            {
                var normalized = key.Trim();
                if (normalized.Length > 0 && string.Equals(normalized, speakerName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}

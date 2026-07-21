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
        SkillUsed,
        GuildFirstVisit
    }

    public enum TacticalDialoguePortraitMode
    {
        Empty,
        Normal,
        Dark
    }

    public enum TacticalDialoguePortraitDirection
    {
        Default,
        Flipped
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
            public TacticalDialoguePortraitDirection leftDirection = TacticalDialoguePortraitDirection.Default;
            public string rightCharacterId = string.Empty;
            public TacticalDialoguePortraitMode rightPortrait = TacticalDialoguePortraitMode.Empty;
            public TacticalDialoguePortraitDirection rightDirection = TacticalDialoguePortraitDirection.Default;
        }

        [SerializeField] private List<CharacterPortrait> characters = new();
        [SerializeField] private List<Line> lines = new();
        [NonSerialized] private readonly Dictionary<string, int> orderedLineIndices = new();

        public IReadOnlyList<CharacterPortrait> Characters => characters;
        public IReadOnlyList<Line> Lines => lines;

        public void ReplaceLines(IEnumerable<Line> source)
        {
            lines.Clear();
            orderedLineIndices.Clear();
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
            var specific = new List<Line>();
            var defaults = new List<Line>();
            foreach (var line in lines)
            {
                if (line == null || line.timing != timing || string.IsNullOrWhiteSpace(line.text))
                    continue;

                if (IsDefaultStageKey(line.stageKey))
                {
                    defaults.Add(line);
                    continue;
                }

                if (MatchesStage(line.stageKey, stage))
                    specific.Add(line);
            }

            var result = specific.Count > 0 ? specific : defaults;
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
                {
                    var defaultMatches = new List<Line>();
                    foreach (var line in candidates)
                        if (line != null && string.IsNullOrWhiteSpace(line.skillKey))
                            defaultMatches.Add(line);

                    if (defaultMatches.Count == 0)
                        return null;
                    candidates = defaultMatches;
                }
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

        public Line GetNextLine(TacticalMapData stage, TacticalDialogueTiming timing,
            string speakerKey = null, string skillKey = null,
            bool requireSkillMatch = false, bool requireSpeakerMatch = false)
        {
            var candidates = GetLines(stage, timing);
            if (candidates.Count == 0)
                return null;

            var sequenceKey = $"{stage?.name}|{timing}|{speakerKey}|{skillKey}";
            if (!string.IsNullOrWhiteSpace(skillKey))
            {
                var skillMatches = new List<Line>();
                foreach (var line in candidates)
                    if (MatchesSkill(line, skillKey))
                        skillMatches.Add(line);

                if (skillMatches.Count > 0)
                    candidates = skillMatches;
                else if (requireSkillMatch)
                {
                    var defaultMatches = new List<Line>();
                    foreach (var line in candidates)
                        if (line != null && string.IsNullOrWhiteSpace(line.skillKey))
                            defaultMatches.Add(line);
                    if (defaultMatches.Count == 0)
                        return null;
                    candidates = defaultMatches;
                    sequenceKey += "|default";
                }
            }

            if (!string.IsNullOrWhiteSpace(speakerKey))
            {
                var speakerMatches = new List<Line>();
                foreach (var line in candidates)
                    if (MatchesSpeaker(line, speakerKey))
                        speakerMatches.Add(line);
                if (speakerMatches.Count > 0)
                    candidates = speakerMatches;
                else if (requireSpeakerMatch)
                    return null;
            }

            var orderGroups = new List<List<Line>>();
            foreach (var candidate in candidates)
            {
                if (orderGroups.Count == 0 || orderGroups[orderGroups.Count - 1][0].order != candidate.order)
                    orderGroups.Add(new List<Line>());
                orderGroups[orderGroups.Count - 1].Add(candidate);
            }

            var groupIndex = orderedLineIndices.TryGetValue(sequenceKey, out var nextIndex)
                ? nextIndex
                : 0;
            var selectedGroup = orderGroups[groupIndex % orderGroups.Count];
            orderedLineIndices[sequenceKey] = groupIndex + 1;
            return selectedGroup[UnityEngine.Random.Range(0, selectedGroup.Count)];
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

        private static bool IsDefaultStageKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) || key.Trim() == "*";
        }

        private static bool MatchesStage(string key, TacticalMapData stage)
        {
            if (IsDefaultStageKey(key) || stage == null)
                return false;

            var normalized = NormalizeStageKey(key);
            return string.Equals(normalized, NormalizeStageKey(stage.name), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, NormalizeStageKey(stage.mapName), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, NormalizeStageKey(stage.DisplayName), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStageKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
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

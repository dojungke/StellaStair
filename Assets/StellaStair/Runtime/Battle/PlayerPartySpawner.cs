using System;
using System.Collections.Generic;
using StellaStair.Units;
using UnityEngine;

namespace StellaStair.Battle
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class PlayerPartySpawner : MonoBehaviour
    {
        [Serializable]
        private struct PartyMember
        {
            public string objectName;
            public string definitionName;
            public Vector3 stagingPosition;
            public Color fallbackColor;

            public PartyMember(
                string objectName, string definitionName, Vector3 stagingPosition, Color fallbackColor)
            {
                this.objectName = objectName;
                this.definitionName = definitionName;
                this.stagingPosition = stagingPosition;
                this.fallbackColor = fallbackColor;
            }
        }

        [SerializeField] private List<PartyMember> defaultParty = new()
        {
            new PartyMember("Player Wizard", "Wizard", new Vector3(-4f, 3.5f), new Color(0.7f, 0.35f, 1f)),
            new PartyMember("Player Archer", "Archer", new Vector3(-3f, 3.5f), new Color(0.25f, 0.85f, 0.35f)),
            new PartyMember("Player Knight", "Knight", new Vector3(-2f, 3.5f), new Color(0.25f, 0.55f, 1f))
        };

        [SerializeField] private bool replaceExistingPlayersOnStageEntry = true;
        private static readonly Dictionary<string, TacticalUnit.UnitProgressSnapshot> savedPartyProgress = new();
        private bool hasSpawned;

        private void Start()
        {
            SpawnDefaultPartyIfNeeded();
        }

        public void SpawnDefaultPartyIfNeeded()
        {
            if (hasSpawned)
                return;

            var battle = FindAnyObjectByType<DeploymentManager>();
            if (battle == null)
                return;

            if (battle.PlayerUnits.Count > 0 && !replaceExistingPlayersOnStageEntry)
                return;
            if (battle.PlayerUnits.Count > 0)
                battle.ClearPlayers(true);

            foreach (var member in defaultParty)
                SpawnPlayer(battle, member);

            hasSpawned = true;
        }


        public static void SavePartyProgress(IEnumerable<TacticalUnit> units)
        {
            if (units == null)
                return;

            foreach (var unit in units)
            {
                if (unit == null || unit.Definition == null)
                    continue;
                savedPartyProgress[unit.ProgressKey] = unit.CaptureProgress();
            }
        }
        private static void SpawnPlayer(DeploymentManager battle, PartyMember member)
        {
            var definition = LoadDefinition(member.definitionName);
            var playerObject = new GameObject(
                string.IsNullOrWhiteSpace(member.objectName) ? member.definitionName : member.objectName,
                typeof(SpriteRenderer), typeof(BoxCollider2D));
            playerObject.transform.position = member.stagingPosition;
            playerObject.transform.localScale = Vector3.one;

            var renderer = playerObject.GetComponent<SpriteRenderer>();
            renderer.color = member.fallbackColor;
            renderer.sortingOrder = 20;
            playerObject.GetComponent<BoxCollider2D>().size = Vector2.one;

            var unit = playerObject.AddComponent<TacticalUnit>();
            unit.Configure(definition, UnitTeam.Player);
            if (definition != null && savedPartyProgress.TryGetValue(definition.name, out var progress))
                unit.ApplyProgress(progress);
            unit.EnsureClickableBody();
            battle.RegisterPlayer(unit);
        }

        private static UnitDefinition LoadDefinition(string assetName)
        {
            var definition = Resources.Load<UnitDefinition>($"UnitDefinitions/{assetName}");
            if (definition == null)
                Debug.LogError($"UnitDefinition asset is missing: UnitDefinitions/{assetName}");
            return definition;
        }
    }
}

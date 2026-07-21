using System;
using System.Collections.Generic;
using StellaStair.Grid;
using StellaStair.Units;
using UnityEngine;

namespace StellaStair.Battle
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class PlayerPartySpawner : MonoBehaviour
    {
        [Serializable]
        public sealed class PartyMember
        {
            [Tooltip("Name of the spawned unit object.") ]
            public string objectName;
            [Tooltip("Fallback Resources asset name when Definition is empty.")]
            public string definitionName;
            [Tooltip("UnitDefinition asset used to create this party member.")]
            public UnitDefinition definition;
            [Tooltip("Initial staging position before deployment.")]
            public Vector3 stagingPosition;
            [Tooltip("Fallback color when the definition has no visual.")]
            public Color fallbackColor;

            public PartyMember(
                string objectName, string definitionName, Vector3 stagingPosition, Color fallbackColor)
            {
                this.objectName = objectName;
                this.definitionName = definitionName;
                this.definition = null;
                this.stagingPosition = stagingPosition;
                this.fallbackColor = fallbackColor;
            }
        }

        [Header("Player Party")]
        [Tooltip("Player units spawned when entering a stage. Add entries and assign a UnitDefinition.")]
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

            var deploymentCells = new List<GridPosition>(battle.Board != null
                ? battle.Board.GetPlayerDeploymentCells()
                : Array.Empty<GridPosition>());
            deploymentCells.Sort((left, right) =>
            {
                var y = left.Y.CompareTo(right.Y);
                return y != 0 ? y : left.X.CompareTo(right.X);
            });

            for (var i = 0; i < defaultParty.Count; i++)
            {
                var member = defaultParty[i];
                var stagingPosition = member.stagingPosition;
                if (battle.Board != null && i < deploymentCells.Count)
                    stagingPosition = battle.Board.PositionToWorld(deploymentCells[i]) +
                        Vector3.up * (battle.Board.Grid.cellSize.y * 2f);
                SpawnPlayer(battle, member, stagingPosition);
            }

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
        private static void SpawnPlayer(DeploymentManager battle, PartyMember member, Vector3 stagingPosition)
        {
            var definition = member.definition != null
                ? member.definition
                : LoadDefinition(member.definitionName);
            var objectName = !string.IsNullOrWhiteSpace(member.objectName)
                ? member.objectName
                : definition != null ? definition.name : member.definitionName;
            var playerObject = new GameObject(
                objectName,
                typeof(SpriteRenderer), typeof(BoxCollider2D));
            playerObject.transform.position = stagingPosition;
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

using System.Collections;
using System.Collections.Generic;
using StellaStair.Grid;
using StellaStair.Presentation;
using StellaStair.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

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
        private const int RoundSpawnSearchPadding = 8;
        private DeploymentManager battle;
        private TacticalBoard board;
        private TacticalMapData.Round previousRoundSnapshot;
        private int nextRoundIndex;
        private Coroutine roundRoutine;
        private bool advancing;

        public TacticalMapData CurrentStage { get; private set; }
        public IReadOnlyList<TacticalMapData> RegisteredStages => registeredStages;

        private void Awake()
        {
            board = FindAnyObjectByType<TacticalBoard>();
            CurrentStage = SelectRandomStage();
            if (CurrentStage != null && board != null)
            {
                CurrentStage.ApplyTo(board);
                previousRoundSnapshot = CreateSnapshotFromStage(CurrentStage);
                nextRoundIndex = 0;
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
            {
                battle.PhaseChanged += OnPhaseChanged;
                battle.PlayerTurnStarted += OnPlayerTurnStarted;
                battle.EnemyForcesDefeated += OnEnemyForcesDefeated;
                UpdatePendingRoundFlag();
            }
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

        private void OnPlayerTurnStarted(int turnNumber)
        {
            TryStartRounds(RoundStartCondition.TurnStart, turnNumber);
        }

        private void OnEnemyForcesDefeated()
        {
            TryStartRounds(RoundStartCondition.EnemiesDefeated, battle != null ? battle.CurrentPlayerTurn : 0);
        }

        private void TryStartRounds(RoundStartCondition condition, int turnNumber)
        {
            if (roundRoutine != null)
                return;
            roundRoutine = StartCoroutine(TryStartRoundsRoutine(condition, turnNumber));
        }

        private IEnumerator TryStartRoundsRoutine(RoundStartCondition condition, int turnNumber)
        {
            if (CurrentStage == null || board == null || battle == null)
            {
                roundRoutine = null;
                yield break;
            }

            var startedAnyRound = false;
            battle.PushInteractionLock();
            try
            {
                while (nextRoundIndex < CurrentStage.rounds.Count)
                {
                    var round = CurrentStage.rounds[nextRoundIndex];
                    if (round == null)
                    {
                        nextRoundIndex++;
                        continue;
                    }

                    var canStart = round.startCondition == condition &&
                                   (condition != RoundStartCondition.TurnStart ||
                                    turnNumber >= Mathf.Max(1, round.startTurn));
                    if (!canStart)
                        break;

                    ApplyRound(round);
                    nextRoundIndex++;
                    startedAnyRound = true;
                }

                if (startedAnyRound)
                {
                    yield return null;
                    while (board != null && board.HasUnitsResolvingForcedMovement())
                        yield return null;
                }
            }
            finally
            {
                battle.PopInteractionLock();
                roundRoutine = null;
            }

            UpdatePendingRoundFlag();
        }

        private void ApplyRound(TacticalMapData.Round round)
        {
            var previous = previousRoundSnapshot ?? CreateSnapshotFromStage(CurrentStage);
            ApplyLayerDiff(board.WalkableTilemap, previous.walkable, round.walkable);
            ApplyLayerDiff(board.PlayerDeploymentTilemap, previous.playerDeployment, round.playerDeployment);
            ApplyLayerDiff(board.LadderTilemap, previous.ladders, round.ladders);
            ApplyWoodLayerDiff(board.WoodTilemap, previous.wood, round.wood);
            ApplyMarkerAdditions(
                EnsureLayer("Crates", 18, board.ConfigureCrates),
                previous.crates, round.crates);
            ApplyMarkerAdditions(
                EnsureLayer("Bomb Crates", 19, board.ConfigureBombCrates),
                previous.bombCrates, round.bombCrates);
            board.SpawnCrateMarkers();
            SpawnChangedEnemies(
                previous.enemyGuardSpawns, round.enemyGuardSpawns,
                CurrentStage.enemyGuardSpawns, "EnemyGuard");
            SpawnChangedEnemies(
                previous.enemySoldierSpawns, round.enemySoldierSpawns,
                CurrentStage.enemySoldierSpawns, "EnemySoldier");
            board.ResolveUnsupportedOccupants();
            previousRoundSnapshot = CloneRound(round);
            battle.RefreshEnemyIntentsForCurrentBoard();
            Debug.Log($"Round started: {round.roundName}");
        }

        private void UpdatePendingRoundFlag()
        {
            if (battle != null && CurrentStage != null)
                battle.WaitForPendingRounds = nextRoundIndex < CurrentStage.rounds.Count;
        }

        private Tilemap EnsureLayer(string layerName, int sortingOrder, System.Action<Tilemap> configure)
        {
            var child = board.Grid != null ? board.Grid.transform.Find(layerName) : null;
            if (child != null && child.TryGetComponent<Tilemap>(out var existing))
            {
                configure?.Invoke(existing);
                return existing;
            }

            var gameObject = new GameObject(layerName, typeof(Tilemap), typeof(TilemapRenderer));
            gameObject.transform.SetParent(board.Grid.transform, false);
            gameObject.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            var tilemap = gameObject.GetComponent<Tilemap>();
            configure?.Invoke(tilemap);
            return tilemap;
        }

        private void ApplyLayerDiff(
            Tilemap tilemap, List<TacticalMapData.Cell> previous,
            List<TacticalMapData.Cell> next)
        {
            if (tilemap == null)
                return;
            var previousByPosition = ToDictionary(previous);
            var nextByPosition = ToDictionary(next);

            foreach (var pair in previousByPosition)
                if (!nextByPosition.ContainsKey(pair.Key))
                    tilemap.SetTile(pair.Key, null);

            foreach (var pair in nextByPosition)
            {
                if (previousByPosition.TryGetValue(pair.Key, out var oldCell) &&
                    oldCell.tile == pair.Value.tile &&
                    oldCell.color == pair.Value.color)
                    continue;
                tilemap.SetTile(pair.Key, pair.Value.tile);
                tilemap.SetTileFlags(pair.Key, TileFlags.None);
                tilemap.SetColor(pair.Key, pair.Value.color);
            }
        }

        private void ApplyWoodLayerDiff(
            Tilemap tilemap, List<TacticalMapData.Cell> previous,
            List<TacticalMapData.Cell> next)
        {
            if (tilemap == null)
                return;
            var previousByPosition = ToDictionary(previous);
            var nextByPosition = ToDictionary(next);

            foreach (var pair in previousByPosition)
                if (!nextByPosition.ContainsKey(pair.Key))
                    tilemap.SetTile(pair.Key, null);

            foreach (var pair in nextByPosition)
            {
                if (previousByPosition.ContainsKey(pair.Key))
                    continue;
                tilemap.SetTile(pair.Key, pair.Value.tile);
                tilemap.SetTileFlags(pair.Key, TileFlags.None);
                tilemap.SetColor(pair.Key, pair.Value.color);
            }
        }

        private void ApplyMarkerAdditions(
            Tilemap tilemap, List<TacticalMapData.Cell> previous,
            List<TacticalMapData.Cell> next)
        {
            if (tilemap == null)
                return;
            var previousByPosition = ToDictionary(previous);

            foreach (var pair in ToDictionary(next))
            {
                if (previousByPosition.TryGetValue(pair.Key, out var oldCell) &&
                    oldCell.tile == pair.Value.tile &&
                    oldCell.color == pair.Value.color)
                    continue;
                tilemap.SetTile(pair.Key, pair.Value.tile);
                tilemap.SetTileFlags(pair.Key, TileFlags.None);
                tilemap.SetColor(pair.Key, pair.Value.color);
            }
        }

        private void SpawnChangedEnemies(
            List<TacticalMapData.Cell> previous,
            List<TacticalMapData.Cell> next,
            List<TacticalMapData.Cell> baseMapSpawns,
            string definitionName)
        {
            var previousByPosition = ToDictionary(previous);
            var baseByPosition = ToDictionary(baseMapSpawns);
            foreach (var pair in ToDictionary(next))
            {
                if (baseByPosition.ContainsKey(pair.Key))
                    continue;
                if (previousByPosition.TryGetValue(pair.Key, out var oldCell) &&
                    oldCell.tile == pair.Value.tile && oldCell.color == pair.Value.color)
                    continue;
                SpawnEnemy(pair.Value, definitionName);
            }
        }

        private void SpawnEnemy(TacticalMapData.Cell cell, string definitionName)
        {
            if (battle == null || board == null || cell == null || cell.tile == null)
                return;
            var requestedPosition = new GridPosition(cell.position.x, cell.position.y - 1);
            if (!TryFindSpawnPosition(requestedPosition, out var position))
            {
                Debug.LogWarning(
                    $"Round spawn failed: no free tile near {requestedPosition} for {definitionName}.",
                    this);
                return;
            }

            var tile = cell.tile as Tile;
            var enemyObject = new GameObject(
                $"{definitionName} Round {cell.position.x},{cell.position.y}",
                typeof(SpriteRenderer), typeof(BoxCollider2D));
            enemyObject.transform.localScale = Vector3.one;
            var renderer = enemyObject.GetComponent<SpriteRenderer>();
            renderer.sprite = tile != null ? tile.sprite : null;
            renderer.color = cell.color;
            renderer.sortingOrder = 20;
            enemyObject.GetComponent<BoxCollider2D>().size = Vector2.one;
            var unit = enemyObject.AddComponent<TacticalUnit>();
            unit.Configure(
                Resources.Load<UnitDefinition>($"UnitDefinitions/{definitionName}"),
                UnitTeam.Enemy);
            if (!battle.RegisterEnemy(unit, position))
                Destroy(enemyObject);
            else if (position != requestedPosition)
                Debug.Log(
                    $"Round spawn moved: {definitionName} {requestedPosition} -> {position}.",
                    this);
        }

        private bool TryFindSpawnPosition(GridPosition requestedPosition, out GridPosition position)
        {
            if (board.CanEnter(requestedPosition))
            {
                position = requestedPosition;
                return true;
            }

            var maxRadius = GetSpawnSearchRadius(requestedPosition);
            for (var radius = 1; radius <= maxRadius; radius++)
            {
                foreach (var candidate in EnumeratePositionsAtDistance(requestedPosition, radius))
                {
                    if (!board.CanEnter(candidate))
                        continue;
                    position = candidate;
                    return true;
                }
            }

            position = default;
            return false;
        }

        private int GetSpawnSearchRadius(GridPosition requestedPosition)
        {
            if (board.WalkableTilemap == null)
                return RoundSpawnSearchPadding;

            var bounds = board.WalkableTilemap.cellBounds;
            var min = new GridPosition(bounds.xMin, bounds.yMin);
            var max = new GridPosition(bounds.xMax - 1, bounds.yMax - 1);
            return Mathf.Max(
                requestedPosition.ManhattanDistance(min),
                requestedPosition.ManhattanDistance(max)) + RoundSpawnSearchPadding;
        }

        private static IEnumerable<GridPosition> EnumeratePositionsAtDistance(
            GridPosition center, int distance)
        {
            for (var dx = -distance; dx <= distance; dx++)
            {
                var remainingY = distance - Mathf.Abs(dx);
                yield return new GridPosition(center.X + dx, center.Y + remainingY);
                if (remainingY != 0)
                    yield return new GridPosition(center.X + dx, center.Y - remainingY);
            }
        }

        private static Dictionary<Vector3Int, TacticalMapData.Cell> ToDictionary(
            List<TacticalMapData.Cell> cells)
        {
            var result = new Dictionary<Vector3Int, TacticalMapData.Cell>();
            if (cells == null)
                return result;
            foreach (var cell in cells)
                if (cell != null && cell.tile != null)
                    result[cell.position] = cell;
            return result;
        }

        private static TacticalMapData.Round CreateSnapshotFromStage(TacticalMapData stage)
        {
            var round = new TacticalMapData.Round { roundName = "Initial" };
            if (stage == null)
                return round;
            Copy(stage.walkable, round.walkable);
            Copy(stage.playerDeployment, round.playerDeployment);
            Copy(stage.ladders, round.ladders);
            Copy(stage.wood, round.wood);
            Copy(stage.crates, round.crates);
            Copy(stage.bombCrates, round.bombCrates);
            Copy(stage.objectiveTargets, round.objectiveTargets);
            Copy(stage.defenseObjectives, round.defenseObjectives);
            return round;
        }

        private static TacticalMapData.Round CloneRound(TacticalMapData.Round source)
        {
            var clone = new TacticalMapData.Round
            {
                roundName = source.roundName,
                startCondition = source.startCondition,
                startTurn = source.startTurn
            };
            Copy(source.walkable, clone.walkable);
            Copy(source.playerDeployment, clone.playerDeployment);
            Copy(source.ladders, clone.ladders);
            Copy(source.wood, clone.wood);
            Copy(source.crates, clone.crates);
            Copy(source.bombCrates, clone.bombCrates);
            Copy(source.objectiveTargets, clone.objectiveTargets);
            Copy(source.defenseObjectives, clone.defenseObjectives);
            Copy(source.enemyGuardSpawns, clone.enemyGuardSpawns);
            Copy(source.enemySoldierSpawns, clone.enemySoldierSpawns);
            return clone;
        }

        private static void Copy(List<TacticalMapData.Cell> source, List<TacticalMapData.Cell> destination)
        {
            destination.Clear();
            if (source == null)
                return;
            foreach (var cell in source)
            {
                if (cell == null)
                    continue;
                destination.Add(new TacticalMapData.Cell
                {
                    position = cell.position,
                    tile = cell.tile,
                    color = cell.color
                });
            }
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
            if (battle != null)
            {
                battle.PlayerTurnStarted -= OnPlayerTurnStarted;
                battle.EnemyForcesDefeated -= OnEnemyForcesDefeated;
            }
        }
    }
}

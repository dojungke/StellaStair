using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StellaStair.Grid;
using StellaStair.Presentation;
using StellaStair.Units;
using UnityEngine;

namespace StellaStair.Battle
{
    public enum BattlePhase { Deployment, PlayerTurn, EnemyTurn, Victory, Defeat }

    public sealed class DeploymentManager : MonoBehaviour
    {
        [SerializeField] private TacticalBoard board;
        [SerializeField] private List<TacticalUnit> playerUnits = new();
        [SerializeField] private List<TacticalUnit> enemyUnits = new();
        [SerializeField] private TacticalStageType stageType = TacticalStageType.Elimination;
        [SerializeField, Min(1)] private int defensiveObjectiveRadius = 4;
        [SerializeField, Min(0)] private int defensiveMaxDistanceFromStart = 2;
        [SerializeField, Min(1)] private int defenseTurnsToSurvive = 5;
        [SerializeField, Min(0)] private int stageClearExperience = 2;
        [SerializeField, Min(0)] private int enemyKillExperience = 1;
        private readonly List<EnemyIntent> enemyIntents = new();
        private readonly Dictionary<TacticalUnit, GridPosition> enemyStartPositions = new();
        private Coroutine refreshEnemyIntentsRoutine;
        private TacticalBoard subscribedBoard;
        private int interactionLockCount;
        private bool stageClearExperienceGranted;

        public BattlePhase Phase { get; private set; } = BattlePhase.Deployment;
        public TacticalBoard Board => board;
        public TacticalStageType StageType => stageType;
        public int DefenseTurnsSurvived { get; private set; }
        public int DefenseTurnsToSurvive => defenseTurnsToSurvive;
        public IReadOnlyList<TacticalUnit> PlayerUnits => playerUnits;
        public IReadOnlyList<TacticalUnit> EnemyUnits => enemyUnits;
        public IReadOnlyList<EnemyIntent> EnemyIntents => enemyIntents;
        public event Action<BattlePhase> PhaseChanged;
        public event Action EnemyIntentsChanged;
        public event Action<int> PlayerTurnStarted;
        public event Action EnemyForcesDefeated;
        public bool WaitForPendingRounds { get; set; }
        public int CurrentPlayerTurn { get; private set; }
        public bool InteractionLocked => interactionLockCount > 0 ||
            board != null && board.HasUnitsResolvingForcedMovement();

        private void Awake()
        {
            if (GetComponent<EnemyIntentPresenter>() == null)
                gameObject.AddComponent<EnemyIntentPresenter>();
            SubscribeToBoardOccupancy();
        }

        public void Configure(TacticalBoard targetBoard, IEnumerable<TacticalUnit> players,
            IEnumerable<TacticalUnit> enemies = null)
        {
            board = targetBoard;
            SubscribeToBoardOccupancy();
            playerUnits.Clear();
            playerUnits.AddRange(players);
            enemyUnits.Clear();
            enemyStartPositions.Clear();
            if (enemies != null)
                foreach (var enemy in enemies)
                    RegisterEnemy(enemy);
            foreach (var player in playerUnits)
                Observe(player);
            ObserveObjectives();
        }

        public void ConfigureStage(TacticalStageType type, int turnsToSurvive = 5)
        {
            stageType = type;
            defenseTurnsToSurvive = Mathf.Max(1, turnsToSurvive);
            DefenseTurnsSurvived = 0;
            if (stageType == TacticalStageType.Defense)
                board?.SpawnDefenseObjectiveMarkers();
            ObserveObjectives();
            CheckBattleEnd();
            if (Phase == BattlePhase.PlayerTurn)
                GenerateEnemyIntents();
        }

        public bool RegisterEnemy(TacticalUnit enemy, GridPosition? startingPosition = null)
        {
            if (enemy == null || enemy.Team != UnitTeam.Enemy)
                return false;
            if (!enemyUnits.Contains(enemy))
                enemyUnits.Add(enemy);
            Observe(enemy);
            var placed = !startingPosition.HasValue || enemy.TryPlace(board, startingPosition.Value, false);
            if (placed && enemy.IsPlaced)
                enemyStartPositions[enemy] = enemy.Position;
            return placed;
        }

        public bool RegisterPlayer(TacticalUnit player)
        {
            if (player == null || player.Team != UnitTeam.Player)
                return false;
            if (!playerUnits.Contains(player))
                playerUnits.Add(player);
            Observe(player);
            return true;
        }

        public bool TryDeploy(TacticalUnit unit, GridPosition position)
        {
            return !InteractionLocked && Phase == BattlePhase.Deployment &&
                   unit != null && unit.Team == UnitTeam.Player &&
                   playerUnits.Contains(unit) && unit.TryPlace(board, position, true);
        }

        public bool TrySwapDeployment(TacticalUnit first, TacticalUnit second)
        {
            return !InteractionLocked && Phase == BattlePhase.Deployment &&
                   first != null && second != null && first != second &&
                   first.Team == UnitTeam.Player && second.Team == UnitTeam.Player &&
                   playerUnits.Contains(first) && playerUnits.Contains(second) &&
                   first.TrySwapPlacementWith(second, true);
        }

        public bool CanStartBattle()
        {
            if (playerUnits.Count == 0 || enemyUnits.Count == 0)
                return false;
            foreach (var unit in playerUnits)
                if (unit == null || !unit.IsPlaced)
                    return false;
            foreach (var unit in enemyUnits)
                if (unit == null || !unit.IsPlaced)
                    return false;
            return true;
        }

        public bool StartBattle()
        {
            if (InteractionLocked || !CanStartBattle())
                return false;
            StartCoroutine(OpeningEnemyMoveRoutine());
            return true;
        }

        private IEnumerator OpeningEnemyMoveRoutine()
        {
            SetPhase(BattlePhase.EnemyTurn);
            BeginUnitsTurn(enemyUnits);
            var reservedDestinations = new HashSet<GridPosition>();
            yield return new WaitForSeconds(0.25f);

            foreach (var enemy in enemyUnits)
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;
                CaptureEnemyStartPosition(enemy);

                if (stageType == TacticalStageType.Attack)
                    continue;

                var target = FindEnemyTarget(enemy);
                if (target == null)
                    continue;

                var destination = FindBestMove(enemy, target, reservedDestinations);
                if (!destination.HasValue)
                    continue;

                reservedDestinations.Add(destination.Value);
                if (enemy.TryMoveTo(destination.Value))
                    while (enemy.IsMoving) yield return null;
            }

            BeginUnitsTurn(playerUnits);
            SetPhase(BattlePhase.PlayerTurn);
            GenerateEnemyIntents();
        }

        public bool EndPlayerTurn()
        {
            if (InteractionLocked || Phase != BattlePhase.PlayerTurn)
                return false;
            StartCoroutine(EnemyTurnRoutine());
            return true;
        }

        public void PushInteractionLock()
        {
            interactionLockCount++;
        }

        public void PopInteractionLock()
        {
            interactionLockCount = Mathf.Max(0, interactionLockCount - 1);
        }

        public IEnumerable<TacticalUnit> GetAttackableEnemies(TacticalUnit attacker)
        {
            foreach (var enemy in enemyUnits)
                if (enemy != null && attacker.CanAttack(enemy))
                    yield return enemy;
        }

        public bool TryGetEnemyIntent(TacticalUnit enemy, out EnemyIntent intent)
        {
            foreach (var candidate in enemyIntents)
            {
                if (candidate.Enemy != enemy)
                    continue;
                intent = candidate;
                return true;
            }

            intent = default;
            return false;
        }

        private IEnumerator EnemyTurnRoutine()
        {
            SetPhase(BattlePhase.EnemyTurn);
            BeginUnitsTurn(enemyUnits);
            var executingIntents = enemyIntents.ToArray();
            enemyIntents.Clear();
            EnemyIntentsChanged?.Invoke();
            yield return new WaitForSeconds(0.25f);

            foreach (var intent in executingIntents)
            {
                var enemy = intent.Enemy;
                if (enemy == null || !enemy.IsAlive || CheckBattleEnd())
                    continue;

                if (intent.WillMove && enemy.TryMoveTo(intent.MoveDestination))
                {
                    while (enemy.IsMoving) yield return null;
                }

                if (intent.WillAttack && enemy.TryAttackPosition(intent.TargetPosition, true))
                {
                    while (enemy.IsAttacking) yield return null;
                }

                yield return new WaitForSeconds(0.2f);
            }

            if (CheckBattleEnd())
                yield break;

            if (stageType == TacticalStageType.Defense)
            {
                DefenseTurnsSurvived++;
                if (CheckBattleEnd())
                    yield break;
            }

            BeginUnitsTurn(playerUnits);
            SetPhase(BattlePhase.PlayerTurn);
            GenerateEnemyIntents();
        }

        private void GenerateEnemyIntents()
        {
            enemyIntents.Clear();
            var reservedDestinations = new HashSet<GridPosition>();
            foreach (var enemy in enemyUnits)
            {
                if (enemy == null || !enemy.IsAlive || !enemy.IsPlaced)
                    continue;
                CaptureEnemyStartPosition(enemy);

                if (stageType == TacticalStageType.Attack)
                {
                    if (!TryFindBestDefensiveAttack(
                            enemy, reservedDestinations, out var defensiveTarget,
                            out var defensiveDestination))
                        continue;

                    reservedDestinations.Add(defensiveDestination);
                    enemyIntents.Add(new EnemyIntent(
                        enemy, defensiveDestination, defensiveDestination,
                        defensiveTarget.Position,
                        defensiveDestination != enemy.Position, true));
                    continue;
                }

                var target = FindEnemyTarget(enemy);
                if (target == null)
                    continue;

                var destination = enemy.Position;
                var plannedMove = FindBestMove(enemy, target, reservedDestinations);
                if (plannedMove.HasValue)
                    destination = plannedMove.Value;
                reservedDestinations.Add(destination);

                var willAttack = enemy.IsPositionAttackableFrom(destination, target.Position);

                enemyIntents.Add(new EnemyIntent(
                    enemy, destination, destination, target.Position,
                    destination != enemy.Position, willAttack));
            }
            EnemyIntentsChanged?.Invoke();
        }

        private GridPosition? FindBestMove(TacticalUnit enemy, TacticalUnit target,
            HashSet<GridPosition> reservedDestinations = null, bool allowEqualDistance = false)
        {
            // Intent planning predicts the next enemy turn, so use the full movement
            // budget even if this enemy spent movement during its previous turn.
            var reachable = GridPathfinder.FindReachable(board, enemy.Position, enemy.MovementPoints, enemy);
            var bestDistance = enemy.Position.ManhattanDistance(target.Position);
            GridPosition? best = null;
            foreach (var pair in reachable)
            {
                if (pair.Key == enemy.Position)
                    continue;
                if (reservedDestinations != null && reservedDestinations.Contains(pair.Key))
                    continue;
                var distance = pair.Key.ManhattanDistance(target.Position);
                if (distance > bestDistance || !allowEqualDistance && distance == bestDistance)
                    continue;
                bestDistance = distance;
                best = pair.Key;
            }
            return best;
        }

        private bool TryFindBestDefensiveAttack(
            TacticalUnit enemy,
            HashSet<GridPosition> reservedDestinations,
            out TacticalUnit target,
            out GridPosition destination)
        {
            target = null;
            destination = default;
            if (enemy == null)
                return false;

            var start = GetEnemyStartPosition(enemy);
            var reachable = GridPathfinder.FindReachable(
                board, enemy.Position, enemy.MovementPoints, enemy);

            var bestTravelCost = int.MaxValue;
            var bestAnchorDistance = int.MaxValue;
            var bestTargetDistance = int.MaxValue;
            foreach (var pair in reachable)
            {
                var position = pair.Key;
                if (reservedDestinations != null && reservedDestinations.Contains(position))
                    continue;
                if (position.ManhattanDistance(start) > defensiveMaxDistanceFromStart)
                    continue;
                foreach (var player in playerUnits)
                {
                    if (player == null || !player.IsAlive || !player.IsPlaced)
                        continue;
                    if (!enemy.IsPositionAttackableFrom(position, player.Position))
                        continue;

                    var anchorDistance = position.ManhattanDistance(start);
                    var targetDistance = position.ManhattanDistance(player.Position);
                    if (pair.Value > bestTravelCost ||
                        pair.Value == bestTravelCost && anchorDistance > bestAnchorDistance ||
                        pair.Value == bestTravelCost && anchorDistance == bestAnchorDistance &&
                        targetDistance >= bestTargetDistance)
                        continue;

                    bestTravelCost = pair.Value;
                    bestAnchorDistance = anchorDistance;
                    bestTargetDistance = targetDistance;
                    target = player;
                    destination = position;
                }
            }

            return target != null;
        }

        private GridPosition GetEnemyStartPosition(TacticalUnit enemy)
        {
            CaptureEnemyStartPosition(enemy);
            return enemyStartPositions.TryGetValue(enemy, out var start)
                ? start
                : enemy.Position;
        }

        private void CaptureEnemyStartPosition(TacticalUnit enemy)
        {
            if (enemy == null || !enemy.IsPlaced || enemyStartPositions.ContainsKey(enemy))
                return;
            enemyStartPositions[enemy] = enemy.Position;
        }

        private TacticalUnit FindEnemyTarget(TacticalUnit enemy)
        {
            if (stageType == TacticalStageType.Defense)
                return FindNearestLivingDefenseObjective(enemy.Position) ??
                       FindNearestPlayer(enemy);

            if (stageType != TacticalStageType.Attack)
                return FindNearestPlayer(enemy);

            var objective = FindNearestLivingObjective(enemy.Position);
            if (objective == null)
                return FindNearestPlayer(enemy);

            TacticalUnit nearestThreat = null;
            var bestThreatDistance = int.MaxValue;
            foreach (var player in playerUnits)
            {
                if (player == null || !player.IsAlive || !player.IsPlaced)
                    continue;

                var playerToObjective = player.Position.ManhattanDistance(objective.Position);
                var canAttackObjective = player.IsPositionAttackableFrom(player.Position, objective.Position);
                if (playerToObjective > defensiveObjectiveRadius && !canAttackObjective)
                    continue;

                var enemyToPlayer = enemy.Position.ManhattanDistance(player.Position);
                if (enemyToPlayer >= bestThreatDistance)
                    continue;

                bestThreatDistance = enemyToPlayer;
                nearestThreat = player;
            }

            return nearestThreat;
        }

        private TacticalUnit FindNearestLivingObjective(GridPosition from)
        {
            if (board == null)
                return null;

            TacticalUnit nearest = null;
            var bestDistance = int.MaxValue;
            foreach (var objective in board.ObjectiveUnits)
            {
                if (objective == null || !objective.IsAlive || !objective.IsPlaced)
                    continue;
                var distance = from.ManhattanDistance(objective.Position);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                nearest = objective;
            }

            return nearest;
        }

        private TacticalUnit FindNearestLivingDefenseObjective(GridPosition from)
        {
            if (board == null)
                return null;

            TacticalUnit nearest = null;
            var bestDistance = int.MaxValue;
            foreach (var objective in board.DefenseObjectiveUnits)
            {
                if (objective == null || !objective.IsAlive || !objective.IsPlaced)
                    continue;
                var distance = from.ManhattanDistance(objective.Position);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                nearest = objective;
            }

            return nearest;
        }

        private TacticalUnit FindNearestPlayer(TacticalUnit enemy)
        {
            TacticalUnit nearest = null;
            var bestDistance = int.MaxValue;
            foreach (var player in playerUnits)
            {
                if (player == null || !player.IsAlive)
                    continue;
                var distance = enemy.Position.ManhattanDistance(player.Position);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                nearest = player;
            }
            return nearest;
        }

        private void BeginUnitsTurn(IEnumerable<TacticalUnit> units)
        {
            foreach (var unit in units)
                if (unit != null && unit.IsAlive)
                    unit.BeginTurn();
        }

        private void Observe(TacticalUnit unit)
        {
            if (unit == null) return;
            unit.Died -= OnUnitDied;
            unit.Died += OnUnitDied;
            unit.MoveCompleted -= OnUnitMoveCompleted;
            unit.MoveCompleted += OnUnitMoveCompleted;
        }

        private void ObserveObjectives()
        {
            if (board == null)
                return;
            foreach (var objective in board.ObjectiveUnits)
                Observe(objective);
            foreach (var objective in board.DefenseObjectiveUnits)
                Observe(objective);
        }

        private void OnUnitMoveCompleted(TacticalUnit unit)
        {
            if (Phase != BattlePhase.PlayerTurn || unit == null)
                return;

            if (refreshEnemyIntentsRoutine == null)
                refreshEnemyIntentsRoutine = StartCoroutine(RefreshEnemyIntentsAfterMovementRoutine());
        }

        private void SubscribeToBoardOccupancy()
        {
            if (subscribedBoard == board)
                return;
            if (subscribedBoard != null)
                subscribedBoard.OccupancyChanged -= OnBoardOccupancyChanged;
            subscribedBoard = board;
            if (subscribedBoard != null)
                subscribedBoard.OccupancyChanged += OnBoardOccupancyChanged;
        }

        private void OnBoardOccupancyChanged()
        {
            if (Phase != BattlePhase.PlayerTurn || refreshEnemyIntentsRoutine != null)
                return;
            refreshEnemyIntentsRoutine = StartCoroutine(RefreshEnemyIntentsAfterMovementRoutine());
        }

        private IEnumerator RefreshEnemyIntentsAfterMovementRoutine()
        {
            // Let every simultaneous knockback/collision reserve its destination,
            // then rebuild all intents from the final board state once.
            yield return null;
            while (board != null && board.HasUnitsResolvingForcedMovement())
                yield return null;

            refreshEnemyIntentsRoutine = null;
            if (Phase == BattlePhase.PlayerTurn)
                GenerateEnemyIntents();
        }

        private void OnUnitDied(TacticalUnit unit)
        {
            if (unit != null)
            {
                AwardEnemyKillExperience(unit);
                enemyStartPositions.Remove(unit);
            }
            enemyIntents.RemoveAll(intent => intent.Enemy == null || !intent.Enemy.IsAlive);
            EnemyIntentsChanged?.Invoke();
            if (!HasLivingUnit(enemyUnits))
                EnemyForcesDefeated?.Invoke();
            CheckBattleEnd();
        }

        private void AwardEnemyKillExperience(TacticalUnit deadUnit)
        {
            if (deadUnit == null || deadUnit.Team != UnitTeam.Enemy)
                return;
            var killer = deadUnit.LastDamageSource;
            if (killer == null || killer.Team != UnitTeam.Player || !killer.IsAlive)
                return;
            killer.GainExperience(enemyKillExperience);
        }

        private void AwardStageClearExperience()
        {
            if (stageClearExperienceGranted)
                return;
            stageClearExperienceGranted = true;
            foreach (var player in playerUnits)
                if (player != null && player.IsAlive)
                    player.GainExperience(stageClearExperience);
        }
        private bool CheckBattleEnd()
        {
            if (stageType == TacticalStageType.Attack &&
                (board == null || !HasLivingUnit(board.ObjectiveUnits)) &&
                !WaitForPendingRounds)
            {
                SetPhase(BattlePhase.Victory);
                return true;
            }

            if (stageType == TacticalStageType.Defense)
            {
                if (board == null || !HasLivingUnit(board.DefenseObjectiveUnits))
                {
                    SetPhase(BattlePhase.Defeat);
                    return true;
                }

                if (DefenseTurnsSurvived >= defenseTurnsToSurvive && !WaitForPendingRounds)
                {
                    SetPhase(BattlePhase.Victory);
                    return true;
                }
            }

            if (stageType == TacticalStageType.Elimination && !HasLivingUnit(enemyUnits) &&
                !WaitForPendingRounds)
            {
                SetPhase(BattlePhase.Victory);
                return true;
            }
            if (!HasLivingUnit(playerUnits))
            {
                SetPhase(BattlePhase.Defeat);
                return true;
            }
            return false;
        }

        private static bool HasLivingUnit(IEnumerable<TacticalUnit> units)
        {
            foreach (var unit in units)
                if (unit != null && unit.IsAlive)
                    return true;
            return false;
        }

        private void SetPhase(BattlePhase phase)
        {
            if (Phase == phase)
                return;

            Phase = phase;
            if (phase == BattlePhase.Victory)
                AwardStageClearExperience();
            PhaseChanged?.Invoke(phase);
            if (phase == BattlePhase.PlayerTurn)
            {
                CurrentPlayerTurn++;
                PlayerTurnStarted?.Invoke(CurrentPlayerTurn);
            }
        }

        public void RefreshEnemyIntentsForCurrentBoard()
        {
            ObserveObjectives();
            if (Phase == BattlePhase.PlayerTurn)
                GenerateEnemyIntents();
        }

        private void OnDestroy()
        {
            if (subscribedBoard != null)
                subscribedBoard.OccupancyChanged -= OnBoardOccupancyChanged;
        }
    }
}

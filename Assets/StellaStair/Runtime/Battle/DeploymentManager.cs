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
        [SerializeField, Min(1)] private int defenseTurnsToSurvive = 5;
        [SerializeField, Min(1)] private int escortTurnsToSurvive = 5;
        [SerializeField] private string escortUnitProgressKey = string.Empty;
        [SerializeField, Min(0)] private int stageClearExperience = 2;
        [SerializeField, Min(0)] private int enemyKillExperience = 1;
        private readonly List<EnemyIntent> enemyIntents = new();
        private readonly Dictionary<TacticalUnit, GridPosition> enemyStartPositions = new();
        private Coroutine refreshEnemyIntentsRoutine;
        private TacticalBoard subscribedBoard;
        private int interactionLockCount;
        private bool stageClearExperienceGranted;
        private int levelUpPreparationCount;
        private readonly Queue<TacticalUnit> pendingLevelUpUnits = new();
        private bool processingLevelUpQueue;
        private LevelUpUpgradePresenter levelUpUpgradePresenter;
        private TacticalUnit escortUnit;

        public BattlePhase Phase { get; private set; } = BattlePhase.Deployment;
        public TacticalBoard Board => board;
        public TacticalStageType StageType => stageType;
        public int DefenseTurnsSurvived { get; private set; }
        public int DefenseTurnsToSurvive => defenseTurnsToSurvive;
        public int EscortTurnsToSurvive => escortTurnsToSurvive;
        public string EscortUnitProgressKey => escortUnitProgressKey;
        public IReadOnlyList<TacticalUnit> PlayerUnits => playerUnits;
        public IReadOnlyList<TacticalUnit> EnemyUnits => enemyUnits;
        public IReadOnlyList<EnemyIntent> EnemyIntents => enemyIntents;
        public event Action<BattlePhase> PhaseChanged;
        public event Action EnemyIntentsChanged;
        public event Action<int> PlayerTurnStarted;
        public event Action EnemyForcesDefeated;
        public event Action<TacticalUnit, TacticalUnit, string> EnemyKilled;
        public event Action<TacticalUnit, TacticalUnit, int> UnitHealed;
        public event Action<TacticalUnit> LevelUpUpgradeSelected;
        public event Func<TacticalUnit, IEnumerator> BeforeLevelUp;
        public event Action<TacticalUnit, string> SkillUsed;
        public event Func<TacticalUnit, string, GridPosition, IEnumerator> BeforeAttack;
        public bool WaitForPendingRounds { get; set; }
        public int CurrentPlayerTurn { get; private set; }
        public bool InteractionLocked => interactionLockCount > 0 ||
            board != null && board.HasUnitsResolvingForcedMovement();
        public bool HasPendingLevelUpSelection =>
            levelUpPreparationCount > 0 ||
            levelUpUpgradePresenter != null && levelUpUpgradePresenter.HasPendingSelection;

        private void Awake()
        {
            if (GetComponent<EnemyIntentPresenter>() == null)
                gameObject.AddComponent<EnemyIntentPresenter>();
            EnsureLevelUpUpgradePresenter();
            EnsureBattleReactiveDialogueController();
            SubscribeToBoardOccupancy();
        }

        private void EnsureLevelUpUpgradePresenter()
        {
            levelUpUpgradePresenter = GetComponent<LevelUpUpgradePresenter>();
            if (levelUpUpgradePresenter == null)
                levelUpUpgradePresenter = gameObject.AddComponent<LevelUpUpgradePresenter>();
            levelUpUpgradePresenter.Configure(this);
            levelUpUpgradePresenter.UpgradeSelected -= OnLevelUpUpgradeSelected;
            levelUpUpgradePresenter.UpgradeSelected += OnLevelUpUpgradeSelected;
        }

        private void EnsureBattleReactiveDialogueController()
        {
            var controller = GetComponent<BattleReactiveDialogueController>();
            if (controller == null)
                controller = gameObject.AddComponent<BattleReactiveDialogueController>();
            controller.Configure(this);
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
            NotifyCurrentPhaseChanged();
        }

        public void ConfigureStage(
            TacticalStageType type, int turnsToSurvive = 5,
            int escortTurns = 5, string escortProgressKey = null)
        {
            stageType = type;
            defenseTurnsToSurvive = Mathf.Max(1, turnsToSurvive);
            escortTurnsToSurvive = Mathf.Max(1, escortTurns);
            escortUnitProgressKey = escortProgressKey ?? string.Empty;
            escortUnit = null;
            DefenseTurnsSurvived = 0;
            if (stageType == TacticalStageType.Defense)
                board?.SpawnDefenseObjectiveMarkers();
            ObserveObjectives();
            CheckBattleEnd();
            if (Phase == BattlePhase.PlayerTurn)
                GenerateEnemyIntents();
            NotifyCurrentPhaseChanged();
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
            {
                enemyStartPositions[enemy] = enemy.Position;
                FaceEnemyTowardNearestPlayerDeploymentCell(enemy);
            }
            NotifyCurrentPhaseChanged();
            return placed;
        }

        private void FaceEnemyTowardNearestPlayerDeploymentCell(TacticalUnit enemy)
        {
            if (enemy == null || board == null || !enemy.IsPlaced)
                return;

            var bestDistance = int.MaxValue;
            GridPosition? best = null;
            foreach (var cell in board.GetPlayerDeploymentCells())
            {
                var distance = Mathf.Abs(cell.X - enemy.Position.X) + Mathf.Abs(cell.Y - enemy.Position.Y);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = cell;
            }

            if (best.HasValue)
                enemy.FaceToward(best.Value);
        }
        public void ClearPlayers(bool destroyGameObjects = false)
        {
            if (destroyGameObjects)
            {
                foreach (var player in playerUnits)
                {
                    if (player != null)
                        Destroy(player.gameObject);
                }
            }
            playerUnits.Clear();
            NotifyCurrentPhaseChanged();
        }
        public bool RegisterPlayer(TacticalUnit player)
        {
            if (player == null || player.Team != UnitTeam.Player)
                return false;
            if (!playerUnits.Contains(player))
                playerUnits.Add(player);
            Observe(player);
            NotifyCurrentPhaseChanged();
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
            BeginUnitsBattle(playerUnits);
            BeginUnitsBattle(enemyUnits);
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

                var attackPosition = ResolveEnemyAttackPosition(enemy, intent);
                if (!attackPosition.HasValue && intent.WillMove && enemy.TryMoveTo(intent.MoveDestination, intent.AllowLadders))
                {
                    while (enemy.IsMoving) yield return null;
                    while (board != null && board.HasUnitsResolvingForcedMovement()) yield return null;
                    attackPosition = ResolveEnemyAttackPosition(enemy, intent);
                }

                if (attackPosition.HasValue && enemy.TryAttackPosition(attackPosition.Value, true))
                {
                    while (enemy.IsAttacking) yield return null;
                    while (board != null && board.HasUnitsResolvingForcedMovement()) yield return null;
                }

                yield return new WaitForSeconds(0.2f);
            }

            if (CheckBattleEnd())
                yield break;

            if (stageType == TacticalStageType.Defense || stageType == TacticalStageType.Escort)
            {
                DefenseTurnsSurvived++;
                if (CheckBattleEnd())
                    yield break;
            }

            BeginUnitsTurn(playerUnits);
            SetPhase(BattlePhase.PlayerTurn);
            GenerateEnemyIntents();
        }

        private GridPosition? ResolveEnemyAttackPosition(TacticalUnit enemy, EnemyIntent intent)
        {
            if (enemy == null || !enemy.IsAlive || !enemy.IsPlaced)
                return null;

            var currentTarget = FindBestAttackablePlayer(enemy, enemy.Position);
            if (currentTarget != null)
                return currentTarget.Position;

            if (!intent.WillAttack || !enemy.IsPositionAttackableFrom(enemy.Position, intent.TargetPosition))
                return null;

            if (board != null && board.TryGetOccupant(intent.TargetPosition, out var originalTarget) &&
                originalTarget != null && originalTarget != enemy && originalTarget.IsAlive &&
                originalTarget.Team != enemy.Team)
                return intent.TargetPosition;

            return null;
        }
        private void GenerateEnemyIntents()
        {
            enemyIntents.Clear();
            var plan = new EnemyPlanState(this);
            var remainingEnemies = new List<TacticalUnit>();
            foreach (var enemy in enemyUnits)
            {
                if (enemy == null || !enemy.IsAlive || !enemy.IsPlaced)
                    continue;
                CaptureEnemyStartPosition(enemy);
                remainingEnemies.Add(enemy);
            }

            while (remainingEnemies.Count > 0)
            {
                EnemyPlanCandidate best = null;
                foreach (var enemy in remainingEnemies)
                {
                    var candidate = BuildEnemyPlanCandidate(enemy, plan);
                    if (candidate == null)
                        continue;
                    if (best == null || candidate.Score > best.Score)
                        best = candidate;
                }

                if (best == null)
                    break;

                enemyIntents.Add(best.Intent);
                plan.Apply(best);
                remainingEnemies.Remove(best.Enemy);
            }

            EnemyIntentsChanged?.Invoke();
        }

        private EnemyPlanCandidate BuildEnemyPlanCandidate(TacticalUnit enemy, EnemyPlanState plan)
        {
            var origin = plan.GetPosition(enemy);
            var allowLadders = stageType != TacticalStageType.Attack || ShouldAllowAttackStageLadders();
            var destination = origin;
            TacticalUnit target = null;
            GridPosition targetPosition;
            var willAttack = false;

            if (stageType == TacticalStageType.Attack)
            {
                target = FindBestAttackablePlayer(enemy, origin, plan) ?? FindEnemyTargetForPlan(enemy, plan);
                if (target == null)
                {
                    var returnMove = FindBestReturnToStartMove(enemy, plan.ReservedDestinations);
                    if (!returnMove.HasValue)
                        return null;
                    destination = returnMove.Value;
                    allowLadders = true;
                    return CreateEnemyPlanCandidate(
                        enemy, origin, destination, destination, false, allowLadders, plan);
                }

                var plannedMove = FindBestDefensiveMoveToPosition(
                    enemy, plan.GetPosition(target), plan.ReservedDestinations, allowLadders);
                if (plannedMove.HasValue)
                    destination = plannedMove.Value;

                target = FindBestAttackablePlayer(enemy, destination, plan) ?? target;
                targetPosition = plan.GetPosition(target);
                willAttack = enemy.IsPositionAttackableFrom(destination, targetPosition);
                allowLadders = allowLadders || IsReturningToStartFloor(enemy, destination);
            }
            else
            {
                target = FindBestAttackablePlayer(enemy, origin, plan) ?? FindEnemyTargetForPlan(enemy, plan);
                if (target == null)
                    return null;

                var plannedMove = FindBestMoveToPosition(
                    enemy, plan.GetPosition(target), plan.ReservedDestinations);
                if (plannedMove.HasValue)
                    destination = plannedMove.Value;

                target = FindBestAttackablePlayer(enemy, destination, plan) ?? target;
                targetPosition = plan.GetPosition(target);
                willAttack = enemy.IsPositionAttackableFrom(destination, targetPosition);
            }

            return CreateEnemyPlanCandidate(
                enemy, origin, destination, targetPosition, willAttack, allowLadders, plan);
        }

        private EnemyPlanCandidate CreateEnemyPlanCandidate(
            TacticalUnit enemy, GridPosition origin, GridPosition destination,
            GridPosition targetPosition, bool willAttack, bool allowLadders, EnemyPlanState plan)
        {
            var forcedDestinations = PredictForcedDestinations(enemy, destination, targetPosition, willAttack, plan);
            var previewDestinations = plan.GetChangedPositions();
            foreach (var pair in forcedDestinations)
                previewDestinations[pair.Key] = pair.Value;
            var score = ScoreEnemyPlan(enemy, origin, destination, targetPosition, willAttack, forcedDestinations, plan);
            var intent = new EnemyIntent(
                enemy, destination, destination, targetPosition,
                destination != origin, willAttack, allowLadders, previewDestinations);
            return new EnemyPlanCandidate(enemy, intent, score, forcedDestinations);
        }

        private Dictionary<TacticalUnit, GridPosition> PredictForcedDestinations(
            TacticalUnit enemy, GridPosition origin, GridPosition targetPosition, bool willAttack, EnemyPlanState plan)
        {
            var result = new Dictionary<TacticalUnit, GridPosition>();
            if (!willAttack || enemy == null || enemy.KnockbackDistance <= 0)
                return result;
            if (!plan.TryGetUnitAt(targetPosition, out var target) ||
                target == null || target == enemy || target.Team == enemy.Team || target.IsObjective)
                return result;

            var direction = targetPosition.X == origin.X
                ? enemy.FacingDirection
                : targetPosition.X > origin.X ? 1 : -1;
            var current = targetPosition;
            for (var i = 0; i < enemy.KnockbackDistance; i++)
            {
                var landingType = board.ResolveKnockbackLanding(
                    current, direction, target, out var landing, out _, out var blockingUnit);
                if (landingType != KnockbackLandingType.Landing)
                    break;
                if (plan.IsOccupiedByOther(landing, target))
                    break;
                current = landing;
            }

            if (current != targetPosition)
                result[target] = current;
            return result;
        }

        private int ScoreEnemyPlan(
            TacticalUnit enemy, GridPosition origin, GridPosition destination,
            GridPosition targetPosition, bool willAttack,
            IReadOnlyDictionary<TacticalUnit, GridPosition> forcedDestinations, EnemyPlanState plan)
        {
            var score = destination == origin ? 0 : 10;
            if (willAttack)
            {
                score += 1000;
                if (plan.TryGetUnitAt(targetPosition, out var target) && target != null)
                    score += EstimateAttackDamageToPlayer(enemy, destination, target) * 100;
            }

            if (forcedDestinations != null && forcedDestinations.Count > 0)
            {
                score += forcedDestinations.Count * 250;
                var simulated = plan.Clone();
                foreach (var pair in forcedDestinations)
                    simulated.SetPosition(pair.Key, pair.Value);
                score += CountAttackableEnemiesAfterPlan(simulated) * 350;
            }

            score -= origin.ManhattanDistance(destination);
            return score;
        }

        private int CountAttackableEnemiesAfterPlan(EnemyPlanState plan)
        {
            var count = 0;
            foreach (var enemy in enemyUnits)
            {
                if (enemy == null || !enemy.IsAlive || !enemy.IsPlaced || plan.HasPlanned(enemy))
                    continue;
                if (FindBestAttackablePlayer(enemy, plan.GetPosition(enemy), plan) != null)
                    count++;
            }
            return count;
        }

        private TacticalUnit FindBestAttackablePlayer(TacticalUnit enemy, GridPosition origin, EnemyPlanState plan)
        {
            TacticalUnit best = null;
            var bestCanKill = false;
            var bestDamage = -1;
            var bestHealth = int.MaxValue;
            var bestDistance = int.MaxValue;
            foreach (var player in playerUnits)
            {
                if (player == null || !player.IsAlive || !player.IsPlaced)
                    continue;
                var playerPosition = plan.GetPosition(player);
                if (!enemy.IsPositionAttackableFrom(origin, playerPosition))
                    continue;

                var damage = EstimateAttackDamageToPlayer(enemy, origin, player);
                var canKill = damage >= player.CurrentHealth;
                var distance = origin.ManhattanDistance(playerPosition);
                if (!IsBetterAttackTarget(
                        canKill, damage, player.CurrentHealth, distance,
                        bestCanKill, bestDamage, bestHealth, bestDistance))
                    continue;

                bestCanKill = canKill;
                bestDamage = damage;
                bestHealth = player.CurrentHealth;
                bestDistance = distance;
                best = player;
            }
            return best;
        }

        private TacticalUnit FindEnemyTargetForPlan(TacticalUnit enemy, EnemyPlanState plan)
        {
            if (stageType == TacticalStageType.Defense)
                return FindNearestLivingDefenseObjective(plan.GetPosition(enemy)) ?? FindNearestPlayer(enemy);
            if (stageType == TacticalStageType.Escort)
                return FindEscortUnit() ?? FindNearestPlayer(enemy);
            if (stageType != TacticalStageType.Attack)
                return FindNearestPlayer(enemy);

            var objective = FindNearestLivingObjective(plan.GetPosition(enemy));
            if (objective == null)
                return FindNearestPlayer(enemy);

            TacticalUnit nearestThreat = null;
            var bestThreatDistance = int.MaxValue;
            foreach (var player in playerUnits)
            {
                if (player == null || !player.IsAlive || !player.IsPlaced)
                    continue;
                if (!IsThreateningAttackObjective(player, objective))
                    continue;
                var distance = plan.GetPosition(enemy).ManhattanDistance(plan.GetPosition(player));
                if (distance >= bestThreatDistance)
                    continue;
                bestThreatDistance = distance;
                nearestThreat = player;
            }
            return nearestThreat;
        }

        private GridPosition? FindBestMoveToPosition(
            TacticalUnit enemy, GridPosition targetPosition, HashSet<GridPosition> reservedDestinations = null)
        {
            var reachable = GridPathfinder.FindReachable(board, enemy.Position, enemy.MovementPoints, enemy);
            if (enemy.AttackDistanceRule == AttackDistanceRule.DistantOnly)
                return FindBestDistantMoveToPosition(enemy, targetPosition, reachable, reservedDestinations);

            var bestDistance = enemy.Position.ManhattanDistance(targetPosition);
            GridPosition? best = null;
            foreach (var pair in reachable)
            {
                if (pair.Key == enemy.Position)
                    continue;
                if (reservedDestinations != null && reservedDestinations.Contains(pair.Key))
                    continue;
                var distance = pair.Key.ManhattanDistance(targetPosition);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = pair.Key;
            }
            return best;
        }

        private GridPosition? FindBestDefensiveMoveToPosition(
            TacticalUnit enemy, GridPosition targetPosition,
            HashSet<GridPosition> reservedDestinations, bool allowLadders)
        {
            var start = GetEnemyStartPosition(enemy);
            var reachable = GridPathfinder.FindReachable(
                board, enemy.Position, enemy.MovementPoints, enemy, allowLadders);
            if (enemy.AttackDistanceRule == AttackDistanceRule.DistantOnly)
            {
                var defensiveReachable = new Dictionary<GridPosition, int>();
                foreach (var pair in reachable)
                    if (pair.Key == enemy.Position || pair.Key.ManhattanDistance(start) <= GetDefensiveMaxDistanceFromStart(enemy))
                        defensiveReachable[pair.Key] = pair.Value;
                return FindBestDistantMoveToPosition(enemy, targetPosition, defensiveReachable, reservedDestinations);
            }

            var bestDistance = enemy.Position.ManhattanDistance(targetPosition);
            var bestTravelCost = int.MaxValue;
            GridPosition? best = null;
            foreach (var pair in reachable)
            {
                var position = pair.Key;
                if (position == enemy.Position ||
                    reservedDestinations != null && reservedDestinations.Contains(position) ||
                    position.ManhattanDistance(start) > GetDefensiveMaxDistanceFromStart(enemy))
                    continue;

                var distance = position.ManhattanDistance(targetPosition);
                if (distance > bestDistance ||
                    distance == bestDistance && pair.Value >= bestTravelCost)
                    continue;

                bestDistance = distance;
                bestTravelCost = pair.Value;
                best = position;
            }
            return best;
        }

        private GridPosition? FindBestDistantMoveToPosition(
            TacticalUnit enemy, GridPosition targetPosition,
            IReadOnlyDictionary<GridPosition, int> reachable,
            HashSet<GridPosition> reservedDestinations = null)
        {
            var best = enemy.Position;
            var bestCanAttack = enemy.IsPositionAttackableFrom(best, targetPosition);
            var bestPenalty = GetDistantRangePenalty(enemy, best, targetPosition);
            var bestTravelCost = 0;
            var bestDistance = best.ManhattanDistance(targetPosition);

            foreach (var pair in reachable)
            {
                var position = pair.Key;
                if (position != enemy.Position && reservedDestinations != null && reservedDestinations.Contains(position))
                    continue;

                var canAttack = enemy.IsPositionAttackableFrom(position, targetPosition);
                var penalty = GetDistantRangePenalty(enemy, position, targetPosition);
                var distance = position.ManhattanDistance(targetPosition);
                if (!IsBetterDistantPosition(
                        canAttack, penalty, pair.Value, distance, position == enemy.Position,
                        bestCanAttack, bestPenalty, bestTravelCost, bestDistance, best == enemy.Position))
                    continue;

                best = position;
                bestCanAttack = canAttack;
                bestPenalty = penalty;
                bestTravelCost = pair.Value;
                bestDistance = distance;
            }
            return best == enemy.Position ? null : best;
        }

        private sealed class EnemyPlanCandidate
        {
            public EnemyPlanCandidate(
                TacticalUnit enemy, EnemyIntent intent, int score,
                IReadOnlyDictionary<TacticalUnit, GridPosition> forcedDestinations)
            {
                Enemy = enemy;
                Intent = intent;
                Score = score;
                ForcedDestinations = forcedDestinations;
            }

            public TacticalUnit Enemy { get; }
            public EnemyIntent Intent { get; }
            public int Score { get; }
            public IReadOnlyDictionary<TacticalUnit, GridPosition> ForcedDestinations { get; }
        }

        private sealed class EnemyPlanState
        {
            private readonly DeploymentManager owner;
            private readonly Dictionary<TacticalUnit, GridPosition> positions = new();
            private readonly HashSet<TacticalUnit> plannedEnemies = new();

            public EnemyPlanState(DeploymentManager owner)
            {
                this.owner = owner;
                foreach (var unit in owner.playerUnits)
                    if (unit != null && unit.IsPlaced)
                        positions[unit] = unit.Position;
                foreach (var unit in owner.enemyUnits)
                    if (unit != null && unit.IsPlaced)
                        positions[unit] = unit.Position;
                if (owner.board != null)
                    foreach (var objective in owner.board.ObjectiveUnits)
                        if (objective != null && objective.IsPlaced)
                            positions[objective] = objective.Position;
            }

            private EnemyPlanState(DeploymentManager owner, Dictionary<TacticalUnit, GridPosition> sourcePositions,
                HashSet<TacticalUnit> sourcePlanned, HashSet<GridPosition> sourceReserved)
            {
                this.owner = owner;
                positions = new Dictionary<TacticalUnit, GridPosition>(sourcePositions);
                plannedEnemies = new HashSet<TacticalUnit>(sourcePlanned);
                ReservedDestinations = new HashSet<GridPosition>(sourceReserved);
            }

            public HashSet<GridPosition> ReservedDestinations { get; } = new();

            public GridPosition GetPosition(TacticalUnit unit)
            {
                if (unit != null && positions.TryGetValue(unit, out var position))
                    return position;
                return unit != null ? unit.Position : default;
            }

            public void SetPosition(TacticalUnit unit, GridPosition position)
            {
                if (unit != null)
                    positions[unit] = position;
            }

            public bool TryGetUnitAt(GridPosition position, out TacticalUnit unit)
            {
                foreach (var pair in positions)
                {
                    if (pair.Key == null || !pair.Key.IsAlive || pair.Value != position)
                        continue;
                    unit = pair.Key;
                    return true;
                }
                unit = null;
                return false;
            }

            public bool IsOccupiedByOther(GridPosition position, TacticalUnit ignoredUnit)
            {
                return TryGetUnitAt(position, out var unit) && unit != ignoredUnit;
            }

            public bool HasPlanned(TacticalUnit enemy) => plannedEnemies.Contains(enemy);

            public Dictionary<TacticalUnit, GridPosition> GetChangedPositions()
            {
                var changed = new Dictionary<TacticalUnit, GridPosition>();
                foreach (var pair in positions)
                {
                    if (pair.Key == null || !pair.Key.IsAlive || !pair.Key.IsPlaced)
                        continue;
                    if (pair.Value != pair.Key.Position)
                        changed[pair.Key] = pair.Value;
                }
                return changed;
            }

            public void Apply(EnemyPlanCandidate candidate)
            {
                if (candidate == null || candidate.Enemy == null)
                    return;
                plannedEnemies.Add(candidate.Enemy);
                SetPosition(candidate.Enemy, candidate.Intent.MoveDestination);
                ReservedDestinations.Add(candidate.Intent.MoveDestination);
                if (candidate.ForcedDestinations != null)
                    foreach (var pair in candidate.ForcedDestinations)
                        SetPosition(pair.Key, pair.Value);
            }

            public EnemyPlanState Clone()
            {
                return new EnemyPlanState(owner, positions, plannedEnemies, ReservedDestinations);
            }
        }
        private GridPosition? FindBestMove(TacticalUnit enemy, TacticalUnit target,
            HashSet<GridPosition> reservedDestinations = null, bool allowEqualDistance = false)
        {
            // Intent planning predicts the next enemy turn, so use the full movement
            // budget even if this enemy spent movement during its previous turn.
            var reachable = GridPathfinder.FindReachable(board, enemy.Position, enemy.MovementPoints, enemy);
            if (enemy.AttackDistanceRule == AttackDistanceRule.DistantOnly)
                return FindBestDistantMove(enemy, target, reachable, reservedDestinations);

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

        private GridPosition? FindBestDistantMove(
            TacticalUnit enemy,
            TacticalUnit target,
            IReadOnlyDictionary<GridPosition, int> reachable,
            HashSet<GridPosition> reservedDestinations = null)
        {
            var best = enemy.Position;
            var bestCanAttack = enemy.IsPositionAttackableFrom(best, target.Position);
            var bestPenalty = GetDistantRangePenalty(enemy, best, target.Position);
            var bestTravelCost = 0;
            var bestDistance = best.ManhattanDistance(target.Position);

            foreach (var pair in reachable)
            {
                var position = pair.Key;
                if (position != enemy.Position && reservedDestinations != null && reservedDestinations.Contains(position))
                    continue;

                var canAttack = enemy.IsPositionAttackableFrom(position, target.Position);
                var penalty = GetDistantRangePenalty(enemy, position, target.Position);
                var distance = position.ManhattanDistance(target.Position);
                if (!IsBetterDistantPosition(
                        canAttack, penalty, pair.Value, distance, position == enemy.Position,
                        bestCanAttack, bestPenalty, bestTravelCost, bestDistance, best == enemy.Position))
                    continue;

                best = position;
                bestCanAttack = canAttack;
                bestPenalty = penalty;
                bestTravelCost = pair.Value;
                bestDistance = distance;
            }

            return best == enemy.Position ? null : best;
        }

        private static bool IsBetterDistantPosition(
            bool canAttack, int penalty, int travelCost, int distance, bool isCurrent,
            bool bestCanAttack, int bestPenalty, int bestTravelCost, int bestDistance, bool bestIsCurrent)
        {
            if (canAttack != bestCanAttack)
                return canAttack;
            if (penalty != bestPenalty)
                return penalty < bestPenalty;
            if (canAttack)
            {
                if (isCurrent != bestIsCurrent)
                    return isCurrent;
                if (travelCost != bestTravelCost)
                    return travelCost < bestTravelCost;
                return distance > bestDistance;
            }

            if (distance != bestDistance)
                return penalty == 0 ? distance > bestDistance : distance < bestDistance;
            return travelCost < bestTravelCost;
        }

        private static int GetDistantRangePenalty(TacticalUnit enemy, GridPosition origin, GridPosition target)
        {
            var horizontalDistance = Mathf.Abs(origin.X - target.X);
            var verticalDistance = Mathf.Abs(origin.Y - target.Y);
            var distance = Mathf.Max(horizontalDistance, verticalDistance);
            var minimumDistance = enemy.AttackDistanceRule == AttackDistanceRule.DistantOnly
                ? enemy.MinimumAttackRange
                : 0;
            var penalty = 0;
            if (distance < minimumDistance)
                penalty += minimumDistance - distance;
            if (horizontalDistance > enemy.AttackRange)
                penalty += horizontalDistance - enemy.AttackRange;
            if (verticalDistance > enemy.VerticalAttackRange)
                penalty += verticalDistance - enemy.VerticalAttackRange;
            return penalty;
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

            var objective = FindNearestLivingObjective(enemy.Position);
            var start = GetEnemyStartPosition(enemy);
            var reachable = GridPathfinder.FindReachable(
                board, enemy.Position, enemy.MovementPoints, enemy, ShouldAllowAttackStageLadders());

            var bestThreatensObjective = false;
            var bestCanKill = false;
            var bestDamage = -1;
            var bestTargetHealth = int.MaxValue;
            var bestTravelCost = int.MaxValue;
            var bestAnchorDistance = int.MaxValue;
            var bestTargetDistance = int.MaxValue;
            foreach (var pair in reachable)
            {
                var position = pair.Key;
                if (reservedDestinations != null && reservedDestinations.Contains(position))
                    continue;
                if (position.ManhattanDistance(start) > GetDefensiveMaxDistanceFromStart(enemy))
                    continue;
                foreach (var player in playerUnits)
                {
                    if (player == null || !player.IsAlive || !player.IsPlaced)
                        continue;
                    var threatensObjective = objective != null && IsThreateningAttackObjective(player, objective);
                    if (!enemy.IsPositionAttackableFrom(position, player.Position))
                        continue;

                    var damage = EstimateAttackDamageToPlayer(enemy, position, player);
                    var canKill = damage >= player.CurrentHealth;
                    var anchorDistance = position.ManhattanDistance(start);
                    var targetDistance = position.ManhattanDistance(player.Position);
                    if (threatensObjective != bestThreatensObjective)
                    {
                        if (!threatensObjective)
                            continue;
                    }
                    else if (!IsBetterAttackTarget(
                                 canKill, damage, player.CurrentHealth, targetDistance,
                                 bestCanKill, bestDamage, bestTargetHealth, bestTargetDistance) ||
                             canKill == bestCanKill && damage == bestDamage &&
                             player.CurrentHealth == bestTargetHealth && pair.Value > bestTravelCost ||
                             canKill == bestCanKill && damage == bestDamage &&
                             player.CurrentHealth == bestTargetHealth && pair.Value == bestTravelCost &&
                             anchorDistance > bestAnchorDistance)
                    {
                        continue;
                    }

                    bestThreatensObjective = threatensObjective;
                    bestCanKill = canKill;
                    bestDamage = damage;
                    bestTargetHealth = player.CurrentHealth;
                    bestTravelCost = pair.Value;
                    bestAnchorDistance = anchorDistance;
                    bestTargetDistance = targetDistance;
                    target = player;
                    destination = position;
                }
            }

            return target != null;
        }

        private GridPosition? FindBestDefensiveMove(
            TacticalUnit enemy, TacticalUnit target,
            HashSet<GridPosition> reservedDestinations)
        {
            var start = GetEnemyStartPosition(enemy);
            var reachable = GridPathfinder.FindReachable(
                board, enemy.Position, enemy.MovementPoints, enemy, ShouldAllowAttackStageLadders());
            if (enemy.AttackDistanceRule == AttackDistanceRule.DistantOnly)
            {
                var defensiveReachable = new Dictionary<GridPosition, int>();
                foreach (var pair in reachable)
                    if (pair.Key == enemy.Position || pair.Key.ManhattanDistance(start) <= GetDefensiveMaxDistanceFromStart(enemy))
                        defensiveReachable[pair.Key] = pair.Value;
                return FindBestDistantMove(enemy, target, defensiveReachable, reservedDestinations);
            }
            var bestDistance = enemy.Position.ManhattanDistance(target.Position);
            var bestTravelCost = int.MaxValue;
            GridPosition? best = null;
            foreach (var pair in reachable)
            {
                var position = pair.Key;
                if (position == enemy.Position ||
                    reservedDestinations != null && reservedDestinations.Contains(position) ||
                    position.ManhattanDistance(start) > GetDefensiveMaxDistanceFromStart(enemy))
                    continue;

                var distance = position.ManhattanDistance(target.Position);
                if (distance > bestDistance ||
                    distance == bestDistance && pair.Value >= bestTravelCost)
                    continue;

                bestDistance = distance;
                bestTravelCost = pair.Value;
                best = position;
            }

            return best;
        }

        private GridPosition? FindBestReturnToStartMove(
            TacticalUnit enemy, HashSet<GridPosition> reservedDestinations)
        {
            var start = GetEnemyStartPosition(enemy);
            if (enemy == null || !enemy.IsPlaced || enemy.Position.Y == start.Y)
                return null;

            var reachable = GridPathfinder.FindReachable(
                board, enemy.Position, enemy.MovementPoints, enemy, allowLadders: true);
            var best = enemy.Position;
            var bestVerticalDistance = Mathf.Abs(enemy.Position.Y - start.Y);
            var bestDistance = enemy.Position.ManhattanDistance(start);
            var bestTravelCost = 0;
            foreach (var pair in reachable)
            {
                var position = pair.Key;
                if (position == enemy.Position ||
                    reservedDestinations != null && reservedDestinations.Contains(position))
                    continue;

                var verticalDistance = Mathf.Abs(position.Y - start.Y);
                var distance = position.ManhattanDistance(start);
                if (verticalDistance > bestVerticalDistance ||
                    verticalDistance == bestVerticalDistance && distance > bestDistance ||
                    verticalDistance == bestVerticalDistance && distance == bestDistance && pair.Value >= bestTravelCost)
                    continue;

                best = position;
                bestVerticalDistance = verticalDistance;
                bestDistance = distance;
                bestTravelCost = pair.Value;
            }

            return best == enemy.Position ? null : best;
        }

        private bool IsReturningToStartFloor(TacticalUnit enemy, GridPosition destination)
        {
            if (stageType != TacticalStageType.Attack || enemy == null || !enemy.IsPlaced)
                return false;
            var start = GetEnemyStartPosition(enemy);
            return enemy.Position.Y != start.Y && destination.Y == start.Y;
        }

        private static int GetDefensiveMaxDistanceFromStart(TacticalUnit enemy)
        {
            return enemy != null ? Mathf.Max(0, enemy.MovementPoints) : 0;
        }

        private bool ShouldAllowAttackStageLadders()
        {
            if (stageType != TacticalStageType.Attack)
                return true;
            if (board == null)
                return false;

            foreach (var objective in board.ObjectiveUnits)
            {
                if (objective == null || !objective.IsAlive || !objective.IsPlaced)
                    continue;
                foreach (var player in playerUnits)
                    if (player != null && player.IsAlive && player.IsPlaced && player.Position.Y == objective.Position.Y)
                        return true;
            }
            return false;
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

        private TacticalUnit FindBestAttackablePlayer(TacticalUnit enemy, GridPosition origin)
        {
            TacticalUnit best = null;
            var bestCanKill = false;
            var bestDamage = -1;
            var bestHealth = int.MaxValue;
            var bestDistance = int.MaxValue;
            foreach (var player in playerUnits)
            {
                if (player == null || !player.IsAlive || !player.IsPlaced)
                    continue;
                if (!enemy.IsPositionAttackableFrom(origin, player.Position))
                    continue;

                var damage = EstimateAttackDamageToPlayer(enemy, origin, player);
                var canKill = damage >= player.CurrentHealth;
                var distance = origin.ManhattanDistance(player.Position);
                if (!IsBetterAttackTarget(
                        canKill, damage, player.CurrentHealth, distance,
                        bestCanKill, bestDamage, bestHealth, bestDistance))
                    continue;

                bestCanKill = canKill;
                bestDamage = damage;
                bestHealth = player.CurrentHealth;
                bestDistance = distance;
                best = player;
            }
            return best;
        }

        private static bool IsBetterAttackTarget(
            bool canKill, int damage, int health, int distance,
            bool bestCanKill, int bestDamage, int bestHealth, int bestDistance)
        {
            if (canKill != bestCanKill)
                return canKill;
            if (damage != bestDamage)
                return damage > bestDamage;
            if (health != bestHealth)
                return health < bestHealth;
            return distance < bestDistance;
        }

        private static int EstimateAttackDamageToPlayer(
            TacticalUnit enemy, GridPosition origin, TacticalUnit player)
        {
            if (enemy == null || player == null)
                return 0;
            if (enemy.IsThrustAttackPosition(origin, player.Position))
            {
                var direction = player.Position.X > origin.X ? 1 : -1;
                var front = new GridPosition(origin.X + direction, origin.Y);
                if (player.Position == front)
                    return enemy.ThrustFrontDamage;
                return enemy.ThrustBackDamage;
            }
            return enemy.AttackDamage;
        }

        private TacticalUnit FindEnemyTarget(TacticalUnit enemy)
        {
            var attackableTarget = FindBestAttackablePlayer(enemy, enemy.Position);
            if (attackableTarget != null)
                return attackableTarget;

            if (stageType == TacticalStageType.Defense)
                return FindNearestLivingDefenseObjective(enemy.Position) ??
                       FindNearestPlayer(enemy);

            if (stageType == TacticalStageType.Escort)
                return FindEscortUnit() ?? FindNearestPlayer(enemy);

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

                if (!IsThreateningAttackObjective(player, objective))
                    continue;

                var enemyToPlayer = enemy.Position.ManhattanDistance(player.Position);
                if (enemyToPlayer >= bestThreatDistance)
                    continue;

                bestThreatDistance = enemyToPlayer;
                nearestThreat = player;
            }

            return nearestThreat;
        }

        private bool IsThreateningAttackObjective(TacticalUnit player, TacticalUnit objective)
        {
            if (player == null || objective == null)
                return false;

            return player.Position.ManhattanDistance(objective.Position) <= defensiveObjectiveRadius ||
                   player.IsPositionAttackableFrom(player.Position, objective.Position);
        }

        private TacticalUnit FindEscortUnit()
        {
            if (escortUnit != null)
                return escortUnit;

            TacticalUnit fallback = null;
            foreach (var player in playerUnits)
            {
                if (player == null)
                    continue;
                fallback ??= player;
                if (!string.IsNullOrWhiteSpace(escortUnitProgressKey) &&
                    string.Equals(player.ProgressKey, escortUnitProgressKey, StringComparison.OrdinalIgnoreCase))
                {
                    escortUnit = player;
                    return escortUnit;
                }
            }

            if (!string.IsNullOrWhiteSpace(escortUnitProgressKey))
                return null;
            escortUnit = fallback;
            return escortUnit;
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
                if (player == null || !player.IsAlive || !player.IsPlaced)
                    continue;
                var distance = enemy.Position.ManhattanDistance(player.Position);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                nearest = player;
            }
            return nearest;
        }

private void BeginUnitsBattle(IEnumerable<TacticalUnit> units)
        {
            foreach (var unit in units)
                if (unit != null && unit.IsAlive)
                    unit.BeginBattle();
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
            unit.LeveledUp -= OnUnitLeveledUp;
            unit.LeveledUp += OnUnitLeveledUp;
            unit.Healed -= OnUnitHealed;
            unit.Healed += OnUnitHealed;
            unit.AttackUsed -= OnUnitAttackUsed;
            unit.AttackUsed += OnUnitAttackUsed;
            unit.BeforeAttack -= OnUnitBeforeAttack;
            unit.BeforeAttack += OnUnitBeforeAttack;
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

        private void OnUnitLeveledUp(TacticalUnit unit, int level)
        {
            if (unit == null)
                return;

            levelUpPreparationCount++;
            pendingLevelUpUnits.Enqueue(unit);
            if (!processingLevelUpQueue)
                StartCoroutine(ProcessLevelUpQueueRoutine());
        }

        private IEnumerator ProcessLevelUpQueueRoutine()
        {
            processingLevelUpQueue = true;
            while (pendingLevelUpUnits.Count > 0)
            {
                var unit = pendingLevelUpUnits.Dequeue();
                if (unit != null && unit.IsAlive)
                {
                    if (BeforeLevelUp != null)
                    {
                        foreach (Func<TacticalUnit, IEnumerator> handler in BeforeLevelUp.GetInvocationList())
                        {
                            var routine = handler(unit);
                            if (routine != null)
                                yield return routine;
                        }
                    }

                    EnsureLevelUpUpgradePresenter();
                    levelUpUpgradePresenter.Enqueue(unit);
                }

                levelUpPreparationCount = Mathf.Max(0, levelUpPreparationCount - 1);
                while (levelUpUpgradePresenter != null && levelUpUpgradePresenter.HasPendingSelection)
                    yield return null;
            }
            processingLevelUpQueue = false;
        }

        private void OnLevelUpUpgradeSelected(TacticalUnit unit)
        {
            if (unit != null && unit.IsAlive)
                LevelUpUpgradeSelected?.Invoke(unit);
        }

        private IEnumerator OnUnitBeforeAttack(TacticalUnit unit, string skillKey, GridPosition targetPosition)
        {
            if (BeforeAttack == null)
                yield break;

            foreach (Func<TacticalUnit, string, GridPosition, IEnumerator> handler in BeforeAttack.GetInvocationList())
            {
                var routine = handler(unit, skillKey, targetPosition);
                if (routine != null)
                    yield return routine;
            }
        }

        private void OnUnitAttackUsed(TacticalUnit unit, string skillKey)
        {
            if (unit != null && !string.IsNullOrWhiteSpace(skillKey))
                SkillUsed?.Invoke(unit, skillKey);
        }

        private void OnUnitHealed(TacticalUnit target, TacticalUnit source, int amount)
        {
            if (target != null && amount > 0)
                UnitHealed?.Invoke(target, source, amount);
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
            var killer = unit != null ? unit.LastDamageSource : null;
            if (unit != null)
            {
                if (unit.Team == UnitTeam.Enemy && killer != null && killer.Team == UnitTeam.Player && killer.IsAlive)
                    EnemyKilled?.Invoke(killer, unit, unit.LastDamageSkillKey);
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
            killer.TryTriggerCourageHeal();
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

            if (stageType == TacticalStageType.Escort)
            {
                var escort = FindEscortUnit();
                if (escort == null || !escort.IsAlive)
                {
                    SetPhase(BattlePhase.Defeat);
                    return true;
                }

                if ((!HasLivingUnit(enemyUnits) || DefenseTurnsSurvived >= escortTurnsToSurvive) &&
                    !WaitForPendingRounds)
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

        private void NotifyCurrentPhaseChanged()
        {
            PhaseChanged?.Invoke(Phase);
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
            if (levelUpUpgradePresenter != null)
                levelUpUpgradePresenter.UpgradeSelected -= OnLevelUpUpgradeSelected;
        }
    }
}

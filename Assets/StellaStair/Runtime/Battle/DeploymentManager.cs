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
        private readonly List<EnemyIntent> enemyIntents = new();

        public BattlePhase Phase { get; private set; } = BattlePhase.Deployment;
        public TacticalBoard Board => board;
        public IReadOnlyList<TacticalUnit> PlayerUnits => playerUnits;
        public IReadOnlyList<TacticalUnit> EnemyUnits => enemyUnits;
        public IReadOnlyList<EnemyIntent> EnemyIntents => enemyIntents;
        public event Action<BattlePhase> PhaseChanged;
        public event Action EnemyIntentsChanged;

        private void Awake()
        {
            if (GetComponent<EnemyIntentPresenter>() == null)
                gameObject.AddComponent<EnemyIntentPresenter>();
        }

        public void Configure(TacticalBoard targetBoard, IEnumerable<TacticalUnit> players,
            IEnumerable<TacticalUnit> enemies = null)
        {
            board = targetBoard;
            playerUnits.Clear();
            playerUnits.AddRange(players);
            enemyUnits.Clear();
            if (enemies != null)
                foreach (var enemy in enemies)
                    RegisterEnemy(enemy);
            foreach (var player in playerUnits)
                Observe(player);
        }

        public bool RegisterEnemy(TacticalUnit enemy, GridPosition? startingPosition = null)
        {
            if (enemy == null || enemy.Team != UnitTeam.Enemy)
                return false;
            if (!enemyUnits.Contains(enemy))
                enemyUnits.Add(enemy);
            Observe(enemy);
            return !startingPosition.HasValue || enemy.TryPlace(board, startingPosition.Value, false);
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
            return Phase == BattlePhase.Deployment && unit != null && unit.Team == UnitTeam.Player &&
                   playerUnits.Contains(unit) && unit.TryPlace(board, position, true);
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
            if (!CanStartBattle())
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

                var target = FindNearestPlayer(enemy);
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
            if (Phase != BattlePhase.PlayerTurn)
                return false;
            StartCoroutine(EnemyTurnRoutine());
            return true;
        }

        public IEnumerable<TacticalUnit> GetAttackableEnemies(TacticalUnit attacker)
        {
            foreach (var enemy in enemyUnits)
                if (enemy != null && attacker.CanAttack(enemy))
                    yield return enemy;
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

                if (intent.WillAttack && enemy.TryAttackPosition(intent.TargetPosition, true))
                {
                    while (enemy.IsAttacking) yield return null;
                }

                if (intent.WillMove && enemy.TryMoveTo(intent.MoveDestination))
                {
                    while (enemy.IsMoving) yield return null;
                }

                yield return new WaitForSeconds(0.2f);
            }

            if (CheckBattleEnd())
                yield break;

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

                var target = FindNearestPlayer(enemy);
                if (target == null)
                    continue;

                var willAttack = enemy.IsPositionAttackableFrom(enemy.Position, target.Position);
                var destination = enemy.Position;
                var plannedMove = FindBestMove(
                    enemy, target, reservedDestinations, allowEqualDistance: willAttack);
                if (plannedMove.HasValue)
                    destination = plannedMove.Value;
                reservedDestinations.Add(destination);

                enemyIntents.Add(new EnemyIntent(
                    enemy, enemy.Position, destination, target.Position,
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

        private void OnUnitMoveCompleted(TacticalUnit unit)
        {
            if (Phase != BattlePhase.PlayerTurn || unit == null || unit.Team != UnitTeam.Enemy)
                return;

            var changed = false;
            for (var i = 0; i < enemyIntents.Count; i++)
            {
                if (enemyIntents[i].Enemy != unit)
                    continue;
                enemyIntents[i] = enemyIntents[i].ShiftAttackWithEnemy(unit.Position);
                changed = true;
            }

            if (changed)
                EnemyIntentsChanged?.Invoke();
        }

        private void OnUnitDied(TacticalUnit unit)
        {
            enemyIntents.RemoveAll(intent => intent.Enemy == null || !intent.Enemy.IsAlive);
            EnemyIntentsChanged?.Invoke();
            CheckBattleEnd();
        }

        private bool CheckBattleEnd()
        {
            if (!HasLivingUnit(enemyUnits))
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
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}

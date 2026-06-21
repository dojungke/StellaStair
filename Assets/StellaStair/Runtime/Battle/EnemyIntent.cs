using StellaStair.Grid;
using StellaStair.Units;

namespace StellaStair.Battle
{
    public readonly struct EnemyIntent
    {
        public TacticalUnit Enemy { get; }
        public GridPosition AttackOrigin { get; }
        public GridPosition MoveDestination { get; }
        public GridPosition TargetPosition { get; }
        public bool WillMove { get; }
        public bool WillAttack { get; }

        public EnemyIntent(TacticalUnit enemy, GridPosition attackOrigin, GridPosition moveDestination,
            GridPosition targetPosition, bool willMove, bool willAttack)
        {
            Enemy = enemy;
            AttackOrigin = attackOrigin;
            MoveDestination = moveDestination;
            TargetPosition = targetPosition;
            WillMove = willMove;
            WillAttack = willAttack;
        }

        public EnemyIntent ShiftAttackWithEnemy(GridPosition newOrigin)
        {
            var deltaX = newOrigin.X - AttackOrigin.X;
            var deltaY = newOrigin.Y - AttackOrigin.Y;
            var shiftedTarget = new GridPosition(TargetPosition.X + deltaX, TargetPosition.Y + deltaY);
            return new EnemyIntent(
                Enemy, newOrigin, MoveDestination, shiftedTarget, WillMove, WillAttack);
        }
    }
}

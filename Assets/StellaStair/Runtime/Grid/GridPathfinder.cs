using System.Collections.Generic;
using StellaStair.Units;

namespace StellaStair.Grid
{
    public static class GridPathfinder
    {
        public static IReadOnlyDictionary<GridPosition, int> FindReachable(
            TacticalBoard board, GridPosition start, int movementPoints, TacticalUnit mover,
            bool allowLadders = true)
        {
            var traversalCosts = new Dictionary<GridPosition, int> { [start] = 0 };
            var reachable = new Dictionary<GridPosition, int> { [start] = 0 };
            var frontier = new Queue<GridPosition>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                var nextCost = traversalCosts[current] + 1;
                if (nextCost > movementPoints)
                    continue;

                foreach (var next in board.GetNeighbors(current, mover, true, allowLadders))
                {
                    if (traversalCosts.ContainsKey(next))
                        continue;

                    traversalCosts[next] = nextCost;
                    if (board.CanEnter(next, mover))
                        reachable[next] = nextCost;
                    frontier.Enqueue(next);
                }
            }

            return reachable;
        }

        public static bool TryFindPath(
            TacticalBoard board, GridPosition start, GridPosition goal, int maxCost,
            TacticalUnit mover, out List<GridPosition> path, bool allowLadders = true)
        {
            path = new List<GridPosition>();
            if (!board.CanEnter(goal, mover))
                return false;

            var previous = new Dictionary<GridPosition, GridPosition>();
            var cost = new Dictionary<GridPosition, int> { [start] = 0 };
            var frontier = new List<GridPosition> { start };

            while (frontier.Count > 0)
            {
                var bestIndex = 0;
                var bestPriority = int.MaxValue;
                for (var i = 0; i < frontier.Count; i++)
                {
                    var priority = cost[frontier[i]] + frontier[i].ManhattanDistance(goal);
                    if (priority >= bestPriority) continue;
                    bestPriority = priority;
                    bestIndex = i;
                }
                var current = frontier[bestIndex];
                frontier.RemoveAt(bestIndex);
                if (current == goal)
                {
                    Reconstruct(previous, start, goal, path);
                    return true;
                }

                foreach (var next in board.GetNeighbors(current, mover, true, allowLadders))
                {
                    var newCost = cost[current] + 1;
                    if (newCost > maxCost || cost.TryGetValue(next, out var oldCost) && newCost >= oldCost)
                        continue;

                    cost[next] = newCost;
                    previous[next] = current;
                    if (!frontier.Contains(next))
                        frontier.Add(next);
                }
            }

            return false;
        }

        private static void Reconstruct(
            Dictionary<GridPosition, GridPosition> previous, GridPosition start,
            GridPosition goal, List<GridPosition> result)
        {
            var current = goal;
            while (current != start)
            {
                result.Add(current);
                current = previous[current];
            }
            result.Reverse();
        }
    }
}

using System;
using UnityEngine;

namespace StellaStair.Grid
{
    [Serializable]
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public readonly int X;
        public readonly int Y;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int ManhattanDistance(GridPosition other) => Mathf.Abs(X - other.X) + Mathf.Abs(Y - other.Y);
        public Vector3Int ToVector3Int() => new(X, Y, 0);
        public static GridPosition From(Vector3Int value) => new(value.x, value.y);
        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
        public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);
        public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
    }
}

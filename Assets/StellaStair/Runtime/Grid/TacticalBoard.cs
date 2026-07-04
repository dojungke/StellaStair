using System;
using System.Collections;
using System.Collections.Generic;
using StellaStair.Units;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StellaStair.Grid
{
    public enum KnockbackLandingType { Landing, Collision, Void }

    public sealed class TacticalBoard : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Grid grid;
        [SerializeField] private Tilemap walkableTilemap;
        [SerializeField] private Tilemap playerDeploymentTilemap;
        [SerializeField] private Tilemap ladderTilemap;
        [SerializeField] private Tilemap woodTilemap;
        [SerializeField] private Tilemap crateTilemap;
        [SerializeField] private Tilemap bombCrateTilemap;
        [SerializeField] private Tilemap objectiveTilemap;
        [SerializeField] private Tilemap defenseObjectiveTilemap;
        [SerializeField] private TacticalObjectDatabase objectDatabase;
        [SerializeField, Min(1)] private int woodMaxHealth = 2;
        [SerializeField, Min(1)] private int objectiveMaxHealth = 8;
        [SerializeField, Min(1)] private int defenseObjectiveMaxHealth = 12;
        [SerializeField, Min(0)] private int maximumStepUp = 1;
        [SerializeField, Min(0)] private int maximumDrop = 2;

        private readonly Dictionary<GridPosition, TacticalUnit> occupants = new();
        private readonly Dictionary<Vector3Int, int> woodHealth = new();
        private readonly List<TacticalUnit> objectiveUnits = new();
        private readonly List<TacticalUnit> defenseObjectiveUnits = new();
        private static readonly int[] HorizontalDirections = { -1, 1 };
        private static Sprite explosionSprite;

        public event Action OccupancyChanged;

        public UnityEngine.Grid Grid => grid;
        public Tilemap WalkableTilemap => walkableTilemap;
        public Tilemap PlayerDeploymentTilemap => playerDeploymentTilemap;
        public Tilemap LadderTilemap => ladderTilemap;
        public Tilemap WoodTilemap => woodTilemap;
        public Tilemap CrateTilemap => crateTilemap;
        public Tilemap BombCrateTilemap => bombCrateTilemap;
        public Tilemap ObjectiveTilemap => objectiveTilemap;
        public Tilemap DefenseObjectiveTilemap => defenseObjectiveTilemap;
        public TacticalObjectDatabase ObjectDatabase => objectDatabase;
        public int WoodMaxHealth => objectDatabase != null
            ? objectDatabase.WoodMaxHealth
            : woodMaxHealth;
        public int CrateMaxHealth => objectDatabase != null
            ? objectDatabase.CrateMaxHealth
            : 2;
        public int BombCrateMaxHealth => objectDatabase != null
            ? objectDatabase.BombCrateMaxHealth
            : 1;
        public int BombCrateExplosionDamage => objectDatabase != null
            ? objectDatabase.BombCrateExplosionDamage
            : 3;
        public int ObjectiveMaxHealth => objectDatabase != null
            ? objectDatabase.AttackObjectiveMaxHealth
            : objectiveMaxHealth;
        public int DefenseObjectiveMaxHealth => objectDatabase != null
            ? objectDatabase.DefenseObjectiveMaxHealth
            : defenseObjectiveMaxHealth;
        public IReadOnlyList<TacticalUnit> ObjectiveUnits => objectiveUnits;
        public IReadOnlyList<TacticalUnit> DefenseObjectiveUnits => defenseObjectiveUnits;

        private void Awake()
        {
            if (grid == null || walkableTilemap == null)
                throw new InvalidOperationException($"{name}: Grid와 Walkable Tilemap을 연결해야 합니다.");
            InitializeWoodHealth();
            SpawnCratesFromTilemap(crateTilemap, false);
            SpawnCratesFromTilemap(bombCrateTilemap, true);
            SpawnObjectivesFromTilemap(objectiveTilemap, objectiveUnits, "Attack Objective", ObjectiveMaxHealth);
        }

        public void Configure(UnityEngine.Grid targetGrid, Tilemap walkable, Tilemap deployment)
        {
            grid = targetGrid;
            walkableTilemap = walkable;
            playerDeploymentTilemap = deployment;
        }

        public void ConfigureObjectDatabase(TacticalObjectDatabase database)
        {
            objectDatabase = database;
        }

        public void ConfigureLadder(Tilemap ladders) => ladderTilemap = ladders;

        public void ConfigureWood(Tilemap wood, int maxHealth = 2)
        {
            woodTilemap = wood;
            woodMaxHealth = Mathf.Max(1, maxHealth);
            InitializeWoodHealth();
        }

        public void ConfigureCrates(Tilemap crates)
        {
            crateTilemap = crates;
            if (Application.isPlaying)
                SpawnCratesFromTilemap(crateTilemap, false);
        }

        public void ConfigureBombCrates(Tilemap crates)
        {
            bombCrateTilemap = crates;
            if (Application.isPlaying)
                SpawnCratesFromTilemap(bombCrateTilemap, true);
        }

        public void ConfigureObjectives(Tilemap objectives, int maxHealth = 8)
        {
            objectiveTilemap = objectives;
            objectiveMaxHealth = Mathf.Max(1, maxHealth);
            if (Application.isPlaying)
                SpawnObjectivesFromTilemap(
                    objectiveTilemap, objectiveUnits, "Attack Objective", ObjectiveMaxHealth);
        }

        public void ConfigureDefenseObjectives(Tilemap objectives, int maxHealth = 12)
        {
            defenseObjectiveTilemap = objectives;
            defenseObjectiveMaxHealth = Mathf.Max(1, maxHealth);
        }

        public void SpawnRuntimeMarkers()
        {
            SpawnCrateMarkers();
            SpawnObjectivesFromTilemap(
                objectiveTilemap, objectiveUnits, "Attack Objective", ObjectiveMaxHealth);
        }

        public void SpawnCrateMarkers()
        {
            SpawnCratesFromTilemap(crateTilemap, false);
            SpawnCratesFromTilemap(bombCrateTilemap, true);
        }

        public void SpawnDefenseObjectiveMarkers()
        {
            SpawnObjectivesFromTilemap(
                defenseObjectiveTilemap, defenseObjectiveUnits,
                "Defense Objective", DefenseObjectiveMaxHealth);
        }

        private void SpawnCratesFromTilemap(Tilemap source, bool explosive)
        {
            if (source == null || grid == null || walkableTilemap == null)
                return;

            var crateCells = new List<Vector3Int>();
            foreach (var cell in source.cellBounds.allPositionsWithin)
                if (source.HasTile(cell))
                    crateCells.Add(cell);

            foreach (var cell in crateCells)
            {
                if (!TryGetCrateSpawnPosition(
                        cell, out var spawnPosition, out var position,
                        out var fallDistance, out var landingType, out var blockingUnit))
                    continue;

                var crateObject = new GameObject(
                    $"{(explosive ? "Bomb Crate" : "Crate")} {cell.x},{cell.y}",
                    typeof(SpriteRenderer), typeof(BoxCollider2D));
                crateObject.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
                var renderer = crateObject.GetComponent<SpriteRenderer>();
                renderer.sprite = source.GetSprite(cell);
                renderer.color = source.GetColor(cell);
                renderer.sortingOrder = 18;
                crateObject.GetComponent<BoxCollider2D>().size = Vector2.one;
                var crate = crateObject.AddComponent<TacticalUnit>();
                crate.ConfigureAsCrate(
                    explosive ? BombCrateMaxHealth : CrateMaxHealth,
                    explosive, BombCrateExplosionDamage);

                if (landingType == KnockbackLandingType.Collision && blockingUnit != null)
                {
                    var startWorld = PositionToStandingWorld(spawnPosition);
                    crate.PrepareUnoccupiedSpawnFall(this, position, startWorld);
                    StartCoroutine(CrateSpawnCollisionFallRoutine(
                        crate, startWorld, PositionToStandingWorld(position), fallDistance, blockingUnit));
                    source.SetTile(cell, null);
                }
                else if (crate.TryPlace(this, position, false))
                {
                    if (fallDistance > 0)
                        StartCoroutine(CrateSpawnFallRoutine(crate, spawnPosition, position, fallDistance));
                    source.SetTile(cell, null);
                }
                else
                {
                    Destroy(crateObject);
                }
            }
        }

        private bool TryGetCrateSpawnPosition(
            Vector3Int cell, out GridPosition spawnPosition,
            out GridPosition landingPosition, out int fallDistance,
            out KnockbackLandingType landingType, out TacticalUnit blockingUnit)
        {
            spawnPosition = new GridPosition(cell.x, cell.y - 1);
            landingPosition = spawnPosition;
            fallDistance = 0;
            landingType = KnockbackLandingType.Landing;
            blockingUnit = null;

            if (CanEnter(spawnPosition))
                return true;

            if (TryGetOccupant(spawnPosition, out blockingUnit) &&
                blockingUnit != null && blockingUnit.IsAlive)
            {
                landingType = KnockbackLandingType.Collision;
                landingPosition = spawnPosition;
                return true;
            }

            if (IsWalkable(spawnPosition))
                return false;

            landingType = ResolveVerticalFall(
                spawnPosition, null, out landingPosition, out fallDistance, out blockingUnit);
            return landingType == KnockbackLandingType.Landing && CanEnter(landingPosition) ||
                   landingType == KnockbackLandingType.Collision &&
                   blockingUnit != null && blockingUnit.IsAlive;
        }

        private IEnumerator CrateSpawnFallRoutine(
            TacticalUnit crate, GridPosition spawnPosition,
            GridPosition landingPosition, int fallDistance)
        {
            if (crate == null)
                yield break;

            var start = PositionToStandingWorld(spawnPosition);
            var end = PositionToStandingWorld(landingPosition);
            crate.transform.position = start;
            crate.BeginExternalForcedMovement();

            try
            {
                var distance = Vector3.Distance(start, end);
                var duration = Mathf.Max(0.08f, distance / 5f);
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    if (crate == null || !crate.IsAlive)
                        yield break;
                    elapsed += Time.deltaTime;
                    crate.transform.position = Vector3.Lerp(
                        start, end, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                    yield return null;
                }

                if (crate == null || !crate.IsAlive)
                    yield break;

                crate.transform.position = end;
                var fallDamage = Mathf.Min(crate.MaxHealth, Mathf.Max(0, fallDistance - 1));
                if (fallDamage > 0)
                    crate.TakeDamage(fallDamage);
            }
            finally
            {
                if (crate != null)
                    crate.EndExternalForcedMovement();
            }
        }

        private IEnumerator CrateSpawnCollisionFallRoutine(
            TacticalUnit crate, Vector3 startWorld, Vector3 impactWorld,
            int fallDistance, TacticalUnit lowerUnit)
        {
            if (crate == null)
                yield break;

            crate.BeginExternalForcedMovement();
            try
            {
                if (crate == null || !crate.IsAlive)
                    yield break;

                var distance = Vector3.Distance(startWorld, impactWorld);
                var duration = Mathf.Max(0.08f, distance / 5f);
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    if (crate == null || !crate.IsAlive)
                        yield break;
                    elapsed += Time.deltaTime;
                    crate.transform.position = Vector3.Lerp(
                        startWorld, impactWorld, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                    yield return null;
                }

                if (crate == null || !crate.IsAlive)
                    yield break;

                crate.transform.position = impactWorld;
                var upperDamage = lowerUnit != null && lowerUnit.IsAlive
                    ? lowerUnit.CurrentHealth
                    : crate.MaxHealth;
                var lowerDamage = crate.CurrentHealth;
                var fallDamage = Mathf.Min(crate.MaxHealth, Mathf.Max(0, fallDistance - 1));

                if (lowerUnit != null && lowerUnit.IsAlive)
                    lowerUnit.TakeDamage(Mathf.Max(lowerDamage, fallDamage));
                crate.TakeDamage(Mathf.Max(upperDamage, fallDamage));
            }
            finally
            {
                if (crate != null)
                    crate.EndExternalForcedMovement();
            }
        }

        private void SpawnObjectivesFromTilemap(
            Tilemap source, List<TacticalUnit> destination, string objectName, int maxHealth)
        {
            if (source == null || grid == null || walkableTilemap == null)
                return;

            var targetCells = new List<Vector3Int>();
            foreach (var cell in source.cellBounds.allPositionsWithin)
                if (source.HasTile(cell))
                    targetCells.Add(cell);

            foreach (var cell in targetCells)
            {
                var position = new GridPosition(cell.x, cell.y - 1);
                if (!CanEnter(position))
                    continue;

                var targetObject = new GameObject(
                    $"{objectName} {cell.x},{cell.y}",
                    typeof(SpriteRenderer), typeof(BoxCollider2D));
                targetObject.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
                var renderer = targetObject.GetComponent<SpriteRenderer>();
                renderer.sprite = source.GetSprite(cell);
                renderer.color = source.GetColor(cell);
                renderer.sortingOrder = 19;
                targetObject.GetComponent<BoxCollider2D>().size = Vector2.one;
                var objective = targetObject.AddComponent<TacticalUnit>();
                objective.ConfigureAsObjective(maxHealth);
                if (objective.TryPlace(this, position, false))
                {
                    destination.Add(objective);
                    source.SetTile(cell, null);
                }
                else
                {
                    Destroy(targetObject);
                }
            }
        }

        public GridPosition WorldToPosition(Vector3 world) => GridPosition.From(grid.WorldToCell(world));

        public GridPosition StandingWorldToPosition(Vector3 world)
        {
            var standingCell = grid.WorldToCell(world);
            return new GridPosition(standingCell.x, standingCell.y - 1);
        }

        public Vector3 PositionToWorld(GridPosition position) =>
            grid.GetCellCenterWorld(position.ToVector3Int());

        public Vector3 PositionToStandingWorld(GridPosition position) =>
            PositionToWorld(position) + Vector3.up * grid.cellSize.y;

        public bool IsWalkable(GridPosition position) =>
            walkableTilemap.HasTile(position.ToVector3Int()) ||
            woodTilemap != null && woodTilemap.HasTile(position.ToVector3Int());

        public bool IsWoodTile(GridPosition position) =>
            woodTilemap != null && woodTilemap.HasTile(position.ToVector3Int());

        public int GetWoodHealth(GridPosition position)
        {
            var cell = position.ToVector3Int();
            if (woodTilemap == null || !woodTilemap.HasTile(cell))
                return 0;
            if (!woodHealth.TryGetValue(cell, out var health))
            {
                health = WoodMaxHealth;
                woodHealth[cell] = health;
            }
            return health;
        }

        public bool DamageWood(GridPosition position, int damage)
        {
            if (damage <= 0 || !IsWoodTile(position))
                return false;

            var cell = position.ToVector3Int();
            var remaining = Mathf.Max(0, GetWoodHealth(position) - damage);
            if (remaining == 0)
            {
                occupants.TryGetValue(position, out var supportedUnit);
                woodHealth.Remove(cell);
                woodTilemap.SetTile(cell, null);
                if (supportedUnit != null && supportedUnit.IsAlive)
                    supportedUnit.TryFallAfterSupportDestroyed();
                OccupancyChanged?.Invoke();
                return true;
            }

            woodHealth[cell] = remaining;
            woodTilemap.SetTileFlags(cell, TileFlags.None);
            var ratio = remaining / (float)WoodMaxHealth;
            woodTilemap.SetColor(cell, new Color(1f, Mathf.Lerp(0.45f, 1f, ratio),
                Mathf.Lerp(0.35f, 1f, ratio), 1f));
            return true;
        }

        public void Detonate(
            GridPosition center, TacticalUnit source, int damage)
        {
            damage = Mathf.Max(1, damage);
            var targets = new List<TacticalUnit>();
            foreach (var pair in occupants)
            {
                if (pair.Value == null || pair.Value == source || !pair.Value.IsAlive)
                    continue;
                if (Mathf.Abs(pair.Key.X - center.X) <= 1 &&
                    Mathf.Abs(pair.Key.Y - center.Y) <= 1)
                    targets.Add(pair.Value);
            }

            StartCoroutine(ExplosionVisualRoutine(center));
            foreach (var target in targets)
                if (target != null && target.IsAlive)
                    target.TakeDamage(damage);

            for (var x = center.X - 1; x <= center.X + 1; x++)
                for (var y = center.Y - 1; y <= center.Y + 1; y++)
                    DamageWood(new GridPosition(x, y), damage);
        }

        private IEnumerator ExplosionVisualRoutine(GridPosition center)
        {
            if (explosionSprite == null)
            {
                explosionSprite = Sprite.Create(
                    Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f), 1f);
                explosionSprite.name = "Runtime Explosion Sprite";
            }

            var root = new GameObject("Bomb Explosion");
            var renderers = new List<SpriteRenderer>();
            for (var x = center.X - 1; x <= center.X + 1; x++)
            {
                for (var y = center.Y - 1; y <= center.Y + 1; y++)
                {
                    var cell = new GameObject("Explosion Cell");
                    cell.transform.SetParent(root.transform);
                    cell.transform.position = PositionToStandingWorld(new GridPosition(x, y));
                    cell.transform.localScale = grid.cellSize * 0.9f;
                    var renderer = cell.AddComponent<SpriteRenderer>();
                    renderer.sprite = explosionSprite;
                    renderer.color = new Color(1f, 0.28f, 0.03f, 0.75f);
                    renderer.sortingOrder = 45;
                    renderers.Add(renderer);
                }
            }

            var elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var alpha = 0.75f * (1f - Mathf.Clamp01(elapsed / duration));
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    var color = renderer.color;
                    color.a = alpha;
                    renderer.color = color;
                }
                yield return null;
            }
            Destroy(root);
        }

        private void InitializeWoodHealth()
        {
            woodHealth.Clear();
            if (woodTilemap == null)
                return;
            foreach (var cell in woodTilemap.cellBounds.allPositionsWithin)
                if (woodTilemap.HasTile(cell))
                    woodHealth[cell] = WoodMaxHealth;
        }

        public bool IsPlayerDeploymentCell(GridPosition position) =>
            playerDeploymentTilemap != null && playerDeploymentTilemap.HasTile(position.ToVector3Int());

        public IEnumerable<GridPosition> GetPlayerDeploymentCells()
        {
            if (playerDeploymentTilemap == null)
                yield break;

            foreach (var cell in playerDeploymentTilemap.cellBounds.allPositionsWithin)
            {
                if (playerDeploymentTilemap.HasTile(cell))
                    yield return GridPosition.From(cell);
            }
        }

        public IEnumerable<GridPosition> GetCellsInAttackRange(
            GridPosition center, int horizontalRange, int verticalRange)
        {
            if (horizontalRange < 1 || verticalRange < 0)
                yield break;

            for (var x = center.X - horizontalRange; x <= center.X + horizontalRange; x++)
            {
                for (var y = center.Y - verticalRange; y <= center.Y + verticalRange; y++)
                {
                    var position = new GridPosition(x, y);

                    if (position != center)
                        yield return position;
                }
            }
        }

        public bool TryGetOccupant(GridPosition position, out TacticalUnit unit) =>
            occupants.TryGetValue(position, out unit);

        public bool HasUnitBetween(GridPosition start, GridPosition target, TacticalUnit ignoredUnit = null)
        {
            var deltaX = target.X - start.X;
            var deltaY = target.Y - start.Y;
            var steps = Mathf.Max(Mathf.Abs(deltaX), Mathf.Abs(deltaY));

            if (steps <= 1)
                return false;

            var visited = new HashSet<GridPosition>();

            for (var i = 1; i < steps; i++)
            {
                var t = (float)i / steps;

                var position = new GridPosition(
                    Mathf.RoundToInt(Mathf.Lerp(start.X, target.X, t)),
                    Mathf.RoundToInt(Mathf.Lerp(start.Y, target.Y, t)));

                if (!visited.Add(position))
                    continue;

                if (TryGetOccupant(position, out var occupant) && occupant != null &&
                    occupant != ignoredUnit && occupant.IsAlive)
                    return true;
            }

            return false;
        }

        public IEnumerable<TacticalUnit> GetOccupantsInRange(GridPosition center, int range)
        {
            if (range < 1)
                yield break;

            foreach (var pair in occupants)
            {
                if (pair.Value != null && pair.Value.IsAlive &&
                    pair.Key != center && pair.Key.ManhattanDistance(center) <= range)
                    yield return pair.Value;
            }
        }

        public bool HasUnitsResolvingForcedMovement()
        {
            foreach (var unit in occupants.Values)
            {
                if (unit != null && unit.IsResolvingForcedMovement)
                    return true;
            }

            return false;
        }

        public void ResolveUnsupportedOccupants()
        {
            var units = new List<TacticalUnit>(occupants.Values);
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive || !unit.IsPlaced)
                    continue;
                if (!IsWalkable(unit.Position))
                    unit.TryFallAfterSupportDestroyed();
            }
        }

        public bool CanEnter(GridPosition position, TacticalUnit mover = null)
        {
            return IsWalkable(position) &&
                   (!occupants.TryGetValue(position, out var occupant) || occupant == mover);
        }

        public IEnumerable<GridPosition> GetNeighbors(
            GridPosition position, TacticalUnit mover = null, bool allowOccupiedTraversal = false)
        {
            foreach (var direction in HorizontalDirections)
            {
                var targetX = position.X + direction;

                // A ladder is a traversal-only floor during path searches.
                // CanEnter still rejects it, so a unit cannot stop on it.
                var ladderTraversal = new GridPosition(targetX, position.Y);
                if (allowOccupiedTraversal && IsLadderTraversalCell(ladderTraversal))
                {
                    yield return ladderTraversal;
                    continue;
                }

                // 가장 높은 유효 표면을 먼저 선택한다.
                // 같은 x에서 수직으로 올라가는 경로를 만들지 않아 단차 타일을 관통하지 않는다.
                for (var y = position.Y + maximumStepUp; y >= position.Y - maximumDrop; y--)
                {
                    var candidate = new GridPosition(targetX, y);

                    if (!IsSurface(candidate) || !allowOccupiedTraversal && !CanEnter(candidate, mover))
                        continue;

                    yield return candidate;
                    break;
                }
            }

            foreach (var ladderDestination in GetLadderDestinations(position, mover, allowOccupiedTraversal))
                yield return ladderDestination;
        }

        private bool IsLadderTraversalCell(GridPosition position)
        {
            if (ladderTilemap == null || IsWalkable(position))
                return false;

            var cell = position.ToVector3Int();
            return ladderTilemap.HasTile(cell) ||
                   ladderTilemap.HasTile(cell + Vector3Int.up);
        }

        private IEnumerable<GridPosition> GetLadderDestinations(
            GridPosition position, TacticalUnit mover, bool allowOccupiedTraversal)
        {
            if (ladderTilemap == null)
                yield break;

            var connectedLadders = new HashSet<Vector3Int>();
            var frontier = new Queue<Vector3Int>();

            for (var xOffset = -1; xOffset <= 1; xOffset++)
            {
                for (var yOffset = 0; yOffset <= 1; yOffset++)
                {
                    var ladderCell = new Vector3Int(
                        position.X + xOffset,
                        position.Y + yOffset,
                        0);

                    if (ladderTilemap.HasTile(ladderCell) && connectedLadders.Add(ladderCell))
                        frontier.Enqueue(ladderCell);
                }
            }

            if (frontier.Count == 0)
                yield break;

            var directions = new[]
            {
                Vector3Int.left,
                Vector3Int.right,
                Vector3Int.up,
                Vector3Int.down
            };

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                foreach (var direction in directions)
                {
                    var next = current + direction;

                    if (ladderTilemap.HasTile(next) && connectedLadders.Add(next))
                        frontier.Enqueue(next);
                }
            }

            var yieldedDestinations = new HashSet<GridPosition>();

            foreach (var ladderCell in connectedLadders)
            {
                // 사다리는 바로 아래 바닥 또는 좌우에 닿은 표면으로 출입할 수 있다.
                for (var xOffset = -1; xOffset <= 1; xOffset++)
                {
                    for (var yOffset = -1; yOffset <= 0; yOffset++)
                    {
                        var destination = new GridPosition(
                            ladderCell.x + xOffset,
                            ladderCell.y + yOffset);

                        if (destination == position)
                            continue;

                        // 핵심 수정:
                        // 같은 높이의 옆칸 이동은 일반 이동으로 처리한다.
                        // 이 조건이 없으면 사다리 1블럭 전/옆에서도 사다리 이동 애니메이션이 시작될 수 있다.
                        if (destination.Y == position.Y)
                            continue;

                        if (!yieldedDestinations.Add(destination))
                            continue;

                        if (!IsSurface(destination))
                            continue;

                        // 실제 경로 탐색에서는 사다리 입구/출구가 비어 있어야 한다.
                        // IsLadderConnection처럼 판정만 할 때는 allowOccupiedTraversal=true로 통과 가능.
                        if (allowOccupiedTraversal || CanEnter(destination, mover))
                            yield return destination;
                    }
                }
            }
        }

        public bool IsLadderConnection(GridPosition from, GridPosition to)
        {
            if (ladderTilemap == null)
                return false;

            // 핵심 수정:
            // 높이가 변하지 않는 이동은 사다리 연결이 아니다.
            if (from.Y == to.Y)
                return false;

            foreach (var destination in GetLadderDestinations(from, null, true))
            {
                if (destination == to)
                    return true;
            }

            return false;
        }

        public bool TryGetLadderWorldX(GridPosition from, GridPosition to, out float worldX)
        {
            worldX = 0f;

            if (ladderTilemap == null)
                return false;

            // 같은 높이 이동은 사다리 이동이 아니므로 ladder X가 필요 없다.
            if (from.Y == to.Y)
                return false;

            Vector3Int? bestEntry = null;
            var bestScore = int.MaxValue;

            for (var xOffset = -1; xOffset <= 1; xOffset++)
            {
                for (var yOffset = 0; yOffset <= 1; yOffset++)
                {
                    var entry = new Vector3Int(
                        from.X + xOffset,
                        from.Y + yOffset,
                        0);

                    if (!ladderTilemap.HasTile(entry))
                        continue;

                    var component = CollectLadderComponent(entry);

                    foreach (var ladderCell in component)
                    {
                        if (!IsSurfaceTouchingLadderCell(to, ladderCell))
                            continue;

                        // from에서 가까운 사다리 진입칸,
                        // to와 이어지는 사다리칸,
                        // 그리고 같은 세로 사다리 컬럼을 우선한다.
                        var score =
                            Mathf.Abs(entry.x - from.X) +
                            Mathf.Abs(entry.y - from.Y) +
                            Mathf.Abs(ladderCell.x - to.X) +
                            Mathf.Abs(ladderCell.y - to.Y) +
                            Mathf.Abs(entry.x - ladderCell.x) * 2;

                        if (score >= bestScore)
                            continue;

                        bestScore = score;
                        bestEntry = entry;
                    }
                }
            }

            if (!bestEntry.HasValue)
                return false;

            worldX = grid.GetCellCenterWorld(bestEntry.Value).x;
            return true;
        }

        private HashSet<Vector3Int> CollectLadderComponent(Vector3Int entry)
        {
            var component = new HashSet<Vector3Int>();
            var frontier = new Queue<Vector3Int>();

            component.Add(entry);
            frontier.Enqueue(entry);

            var directions = new[]
            {
                Vector3Int.left,
                Vector3Int.right,
                Vector3Int.up,
                Vector3Int.down
            };

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                foreach (var direction in directions)
                {
                    var next = current + direction;

                    if (ladderTilemap.HasTile(next) && component.Add(next))
                        frontier.Enqueue(next);
                }
            }

            return component;
        }

        private static bool IsSurfaceTouchingLadderCell(GridPosition surface, Vector3Int ladderCell)
        {
            return Mathf.Abs(ladderCell.x - surface.X) <= 1 &&
                   (surface.Y == ladderCell.y || surface.Y == ladderCell.y - 1);
        }

        public bool TryGetDirectionalNeighbor(
            GridPosition position,
            int horizontalDirection,
            TacticalUnit mover,
            out GridPosition neighbor)
        {
            var targetX = position.X + (horizontalDirection < 0 ? -1 : 1);

            foreach (var candidate in GetNeighbors(position, mover))
            {
                if (candidate.X != targetX || IsLadderConnection(position, candidate))
                    continue;

                neighbor = candidate;
                return true;
            }

            neighbor = default;
            return false;
        }

        public KnockbackLandingType ResolveKnockbackLanding(
            GridPosition position,
            int horizontalDirection,
            TacticalUnit mover,
            out GridPosition landing,
            out int fallDistance,
            out TacticalUnit blockingUnit)
        {
            var targetX = position.X + (horizontalDirection < 0 ? -1 : 1);

            blockingUnit = null;
            fallDistance = 0;
            landing = default;

            // 일반 이동과 달리 넉백은 큰 낙하도 허용한다.
            for (var y = position.Y + maximumStepUp; y >= walkableTilemap.cellBounds.yMin; y--)
            {
                var candidate = new GridPosition(targetX, y);

                if (!IsSurface(candidate))
                    continue;

                if (TryGetOccupant(candidate, out var occupant) && occupant != mover)
                {
                    blockingUnit = occupant;
                    landing = candidate;
                    fallDistance = Mathf.Max(0, position.Y - candidate.Y);
                    return KnockbackLandingType.Collision;
                }

                landing = candidate;
                fallDistance = Mathf.Max(0, position.Y - candidate.Y);
                return KnockbackLandingType.Landing;
            }

            // 해당 열에 지형은 있는데 도달 가능한 표면이 없으면 벽으로 본다.
            if (HasTerrainInColumn(walkableTilemap, targetX) ||
                HasTerrainInColumn(woodTilemap, targetX))
                return KnockbackLandingType.Collision;

            return KnockbackLandingType.Void;
        }

        public KnockbackLandingType ResolveVerticalFall(
            GridPosition position,
            TacticalUnit mover,
            out GridPosition landing,
            out int fallDistance,
            out TacticalUnit blockingUnit)
        {
            landing = default;
            fallDistance = 0;
            blockingUnit = null;

            for (var y = position.Y - 1; y >= walkableTilemap.cellBounds.yMin; y--)
            {
                var candidate = new GridPosition(position.X, y);
                if (!IsSurface(candidate))
                    continue;

                landing = candidate;
                fallDistance = Mathf.Max(0, position.Y - candidate.Y);
                if (TryGetOccupant(candidate, out var occupant) && occupant != mover)
                {
                    blockingUnit = occupant;
                    return KnockbackLandingType.Collision;
                }
                return KnockbackLandingType.Landing;
            }

            return KnockbackLandingType.Void;
        }

        private static bool HasTerrainInColumn(Tilemap tilemap, int x)
        {
            if (tilemap == null)
                return false;
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
                if (cell.x == x && tilemap.HasTile(cell))
                    return true;
            return false;
        }

        private bool IsSurface(GridPosition position)
        {
            if (!IsWalkable(position))
                return false;

            var directlyAbove = new GridPosition(position.X, position.Y + 1);
            return !IsWalkable(directlyAbove);
        }

        public bool TryOccupy(TacticalUnit unit, GridPosition destination)
        {
            if (unit == null || !CanEnter(destination, unit))
                return false;

            RemoveOccupancy(unit);
            occupants[destination] = unit;
            OccupancyChanged?.Invoke();

            return true;
        }

        public bool TrySwapOccupants(TacticalUnit first, TacticalUnit second)
        {
            if (first == null || second == null || first == second)
                return false;

            GridPosition? firstPosition = null;
            GridPosition? secondPosition = null;

            foreach (var pair in occupants)
            {
                if (pair.Value == first)
                    firstPosition = pair.Key;
                else if (pair.Value == second)
                    secondPosition = pair.Key;
            }

            if (!firstPosition.HasValue || !secondPosition.HasValue)
                return false;

            occupants[firstPosition.Value] = second;
            occupants[secondPosition.Value] = first;
            OccupancyChanged?.Invoke();
            return true;
        }

        public void RemoveOccupancy(TacticalUnit unit)
        {
            GridPosition? found = null;

            foreach (var pair in occupants)
            {
                if (pair.Value == unit)
                {
                    found = pair.Key;
                    break;
                }
            }

            if (found.HasValue)
            {
                occupants.Remove(found.Value);
                OccupancyChanged?.Invoke();
            }
        }

        private void OnDestroy()
        {
            occupants.Clear();
            woodHealth.Clear();
            objectiveUnits.Clear();
            defenseObjectiveUnits.Clear();
        }
    }
}

using ElementCommons;
using Units;
using Units.Worker;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine.AI;

namespace Units.MovementSystems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [UpdateAfter(typeof(PlayerInputs.UnitMoveInputSystem))]
    [UpdateBefore(typeof(Navigation.NavMeshPathfindingSystem))]
    public partial class FormationSystem : SystemBase
    {
        private const float GRID_SPACING          = 3.0f;
        private const float OFFSET_MULTIPLIER     = 0.5f;
        private const float NAVMESH_SAMPLE_RADIUS = 5.0f;
        private const float OCCUPIED_RADIUS       = 1.0f;
        private const float SPIRAL_DISTANCE       = 2.5f;
        private const int   SPIRAL_ATTEMPTS       = 12;

        private int _walkableMask;

        protected override void OnCreate()
        {
            _walkableMask = 1 << NavMesh.GetAreaFromName("Walkable");
            RequireForUpdate<UnitTagComponent>();
        }

        protected override void OnUpdate()
        {
            NativeList<Entity> commandedUnits   = new NativeList<Entity>(Allocator.Temp);
            NativeList<float3> requestedTargets = new NativeList<float3>(Allocator.Temp);
            NativeList<int>    teams            = new NativeList<int>(Allocator.Temp);

            foreach ((RefRO<SetInputStateTargetComponent> input,
                     RefRO<ElementSelectionComponent> selection,
                     RefRO<ElementTeamComponent> team,
                     Entity entity)
                     in SystemAPI.Query<RefRO<SetInputStateTargetComponent>,
                                       RefRO<ElementSelectionComponent>,
                                       RefRO<ElementTeamComponent>>()
                         .WithAll<UnitTagComponent>()
                         .WithEntityAccess())
            {
                if (!input.ValueRO.HasNewTarget || !selection.ValueRO.IsSelected)
                    continue;

                commandedUnits.Add(entity);
                requestedTargets.Add(input.ValueRO.TargetPosition);
                teams.Add((int)team.ValueRO.Team);
            }

            if (commandedUnits.Length == 0)
            {
                commandedUnits.Dispose();
                requestedTargets.Dispose();
                teams.Dispose();
                return;
            }

            NativeHashSet<int> processed = new NativeHashSet<int>(commandedUnits.Length, Allocator.Temp);

            for (int i = 0; i < commandedUnits.Length; i++)
            {
                if (processed.Contains(i)) continue;

                float3 baseTarget = requestedTargets[i];
                int    unitTeam   = teams[i];

                NativeList<int> group = new NativeList<int>(Allocator.Temp);
                for (int j = i; j < commandedUnits.Length; j++)
                {
                    if (teams[j] == unitTeam &&
                        math.distancesq(requestedTargets[j], baseTarget) < 1.0f)
                    {
                        group.Add(j);
                        processed.Add(j);
                    }
                }

                AssignFormationSlots(group, commandedUnits, baseTarget);
                group.Dispose();
            }

            processed.Dispose();
            commandedUnits.Dispose();
            requestedTargets.Dispose();
            teams.Dispose();
        }

        private void AssignFormationSlots(
            NativeList<int> group,
            NativeList<Entity> entities,
            float3 baseTarget)
        {
            int total = group.Length;
            NativeList<float3> claimed = new NativeList<float3>(total, Allocator.Temp);

            for (int gi = 0; gi < total; gi++)
            {
                int unitListIndex = group[gi];
                float3 rawSlot    = baseTarget + CalculateGridOffset(gi, total);
                float3 slot       = FindFreeNavMeshPosition(rawSlot, claimed);

                claimed.Add(slot);

                SetInputStateTargetComponent input =
                    EntityManager.GetComponentData<SetInputStateTargetComponent>(entities[unitListIndex]);
                input.TargetPosition = slot;
                input.HasNewTarget   = true;
                EntityManager.SetComponentData(entities[unitListIndex], input);
            }

            claimed.Dispose();
        }

        private float3 FindFreeNavMeshPosition(float3 candidate, NativeList<float3> claimed)
        {
            for (int attempt = 0; attempt <= SPIRAL_ATTEMPTS; attempt++)
            {
                float3 probe = attempt == 0
                    ? candidate
                    : candidate + SpiralOffset(attempt);

                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, NAVMESH_SAMPLE_RADIUS, _walkableMask))
                    continue;

                float3 snapped = hit.position;
                if (!IsClaimed(snapped, claimed))
                    return snapped;
            }

            if (NavMesh.SamplePosition(candidate, out NavMeshHit fallback, NAVMESH_SAMPLE_RADIUS, _walkableMask))
                return fallback.position;

            return candidate;
        }

        private static bool IsClaimed(float3 position, NativeList<float3> claimed)
        {
            for (int i = 0; i < claimed.Length; i++)
            {
                if (math.distancesq(position, claimed[i]) < OCCUPIED_RADIUS * OCCUPIED_RADIUS)
                    return true;
            }
            return false;
        }

        private static float3 SpiralOffset(int attempt)
        {
            float angle = math.radians(attempt * (360f / SPIRAL_ATTEMPTS));
            return new float3(math.cos(angle) * SPIRAL_DISTANCE, 0f,
                              math.sin(angle) * SPIRAL_DISTANCE);
        }

        private static float3 CalculateGridOffset(int index, int totalUnits)
        {
            int columns = (int)math.ceil(math.sqrt(totalUnits));
            int row = index / columns;
            int col = index % columns;
            int colCount = index < (totalUnits / columns) * columns ? columns
                         : totalUnits - (totalUnits / columns) * columns;
            colCount = math.max(colCount, 1);

            float offsetX = (col - (colCount  - 1) * OFFSET_MULTIPLIER) * GRID_SPACING;
            float offsetZ = (row - (totalUnits / columns * OFFSET_MULTIPLIER)) * GRID_SPACING;

            return new float3(offsetX, 0f, offsetZ);
        }
    }
}

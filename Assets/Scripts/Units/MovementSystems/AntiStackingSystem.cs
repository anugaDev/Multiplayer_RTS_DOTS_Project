using ElementCommons;
using Units;
using Units.Worker;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine.AI;

namespace Units.MovementSystems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [UpdateAfter(typeof(FormationSystem))]
    [UpdateBefore(typeof(Navigation.NavMeshPathfindingSystem))]
    public partial class AntiStackingSystem : SystemBase
    {
        private const float STACKING_THRESHOLD    = 1.5f;
        private const float PUSH_DISTANCE         = 2.5f;
        private const int   SPIRAL_STEPS          = 8;
        private const float NAVMESH_SAMPLE_RADIUS = 4.0f;
        private const float MIN_PUSH_DISTANCE     = 0.5f;

        private int _walkableMask;

        protected override void OnCreate()
        {
            _walkableMask = 1 << NavMesh.GetAreaFromName("Walkable");
            RequireForUpdate<UnitTagComponent>();
        }

        protected override void OnUpdate()
        {
            NativeList<Entity> idleEntities  = new NativeList<Entity>(Allocator.Temp);
            NativeList<float3> idlePositions = new NativeList<float3>(Allocator.Temp);
            NativeList<int>    idleTeams     = new NativeList<int>(Allocator.Temp);

            foreach ((RefRO<LocalTransform>               transform,
                      RefRO<PathComponent>                path,
                      RefRO<SetInputStateTargetComponent> input,
                      RefRO<ElementTeamComponent>         team,
                      Entity                              entity)
                     in SystemAPI.Query<RefRO<LocalTransform>,
                                        RefRO<PathComponent>,
                                        RefRO<SetInputStateTargetComponent>,
                                        RefRO<ElementTeamComponent>>()
                         .WithAll<UnitTagComponent>()
                         .WithNone<WorkerGatheringTagComponent,
                                   WorkerStoringTagComponent,
                                   WorkerConstructionTagComponent>()
                         .WithEntityAccess())
            {
                if (path.ValueRO.HasPath || input.ValueRO.HasNewTarget)
                    continue;

                idleEntities.Add(entity);
                idlePositions.Add(transform.ValueRO.Position);
                idleTeams.Add((int)team.ValueRO.Team);
            }

            if (idleEntities.Length < 2)
            {
                idleEntities.Dispose();
                idlePositions.Dispose();
                idleTeams.Dispose();
                return;
            }

            NativeHashSet<int> pushed = new NativeHashSet<int>(idleEntities.Length, Allocator.Temp);

            for (int i = 0; i < idleEntities.Length; i++)
            {
                for (int j = i + 1; j < idleEntities.Length; j++)
                {
                    if (idleTeams[i] != idleTeams[j]) continue;

                    float dist = math.distance(idlePositions[i], idlePositions[j]);
                    if (dist >= STACKING_THRESHOLD) continue;

                    if (!pushed.Contains(j))
                    {
                        float3 freePos = FindFreeNavMeshPosition(idlePositions[j], idlePositions, j);

                        if (math.distance(freePos, idlePositions[j]) >= MIN_PUSH_DISTANCE)
                        {
                            SetInputStateTargetComponent target =
                                EntityManager.GetComponentData<SetInputStateTargetComponent>(idleEntities[j]);
                            target.TargetPosition    = freePos;
                            target.TargetEntity      = Unity.Entities.Entity.Null;
                            target.IsFollowingTarget = false;
                            target.StoppingDistance  = 0.1f;
                            target.HasNewTarget      = true;
                            EntityManager.SetComponentData(idleEntities[j], target);

                            pushed.Add(j);
                        }
                    }
                }
            }

            pushed.Dispose();
            idleEntities.Dispose();
            idlePositions.Dispose();
            idleTeams.Dispose();
        }

        private float3 FindFreeNavMeshPosition(float3 origin, NativeList<float3> occupiedPositions, int skipIndex)
        {
            float angleStep = 360f / SPIRAL_STEPS;

            for (int attempt = 0; attempt < SPIRAL_STEPS; attempt++)
            {
                float angle  = math.radians(angleStep * attempt);
                float3 probe = origin + new float3(math.cos(angle) * PUSH_DISTANCE,
                                                   0f,
                                                   math.sin(angle) * PUSH_DISTANCE);

                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, NAVMESH_SAMPLE_RADIUS, _walkableMask))
                    continue;

                float3 snapped = hit.position;

                if (!IsOccupied(snapped, occupiedPositions, skipIndex))
                    return snapped;
            }

            float fallbackAngle = math.radians(UnityEngine.Random.Range(0f, 360f));
            float3 fallbackProbe = origin + new float3(math.cos(fallbackAngle) * PUSH_DISTANCE,
                                                       0f,
                                                       math.sin(fallbackAngle) * PUSH_DISTANCE);

            if (NavMesh.SamplePosition(fallbackProbe, out NavMeshHit fb, NAVMESH_SAMPLE_RADIUS, _walkableMask))
                return fb.position;

            return fallbackProbe;
        }

        private static bool IsOccupied(float3 position, NativeList<float3> positions, int skipIndex)
        {
            float threshSq = STACKING_THRESHOLD * STACKING_THRESHOLD;
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == skipIndex) continue;
                if (math.distancesq(position, positions[i]) < threshSq)
                    return true;
            }
            return false;
        }
    }
}

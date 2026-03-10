using Units.Worker;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Units.MovementSystems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    public partial struct ServerUnitMoveSystem : ISystem
    {
        private const float FINAL_POSITION_THRESHOLD = 0.1f;
        private const float WAYPOINT_THRESHOLD = 0.5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnitTagComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach ((RefRW<LocalTransform> transform,
                     RefRO<UnitWaypointsInputComponent> waypointsInput,
                     RefRW<PathComponent> pathComponent,
                     RefRO<UnitMoveSpeedComponent> moveSpeed)
                     in SystemAPI.Query<RefRW<LocalTransform>,
                                       RefRO<UnitWaypointsInputComponent>,
                                       RefRW<PathComponent>,
                                       RefRO<UnitMoveSpeedComponent>>()
                         .WithAll<Simulate, UnitTagComponent>())
            {
                MoveUnit(transform, waypointsInput, pathComponent, moveSpeed, deltaTime);
            }
        }

        [BurstCompile]
        private static void MoveUnit(
            RefRW<LocalTransform> transform,
            RefRO<UnitWaypointsInputComponent> waypointsInput,
            RefRW<PathComponent> pathComponent,
            RefRO<UnitMoveSpeedComponent> moveSpeed,
            float deltaTime)
        {
            int count = waypointsInput.ValueRO.WaypointCount;

            if (count == 0)
            {
                if (!pathComponent.ValueRO.HasPath)
                    return;

                float3 lastTarget = pathComponent.ValueRO.LastTargetPosition;
                lastTarget.y = transform.ValueRO.Position.y;

                float3 toTarget = lastTarget - transform.ValueRO.Position;
                toTarget.y = 0f;
                float dist = math.length(toTarget);

                if (dist < FINAL_POSITION_THRESHOLD)
                {
                    pathComponent.ValueRW.HasPath = false;
                    return;
                }

                if (dist < 0.001f)
                {
                    pathComponent.ValueRW.HasPath = false;
                    return;
                }

                float3 dir = toTarget / dist;
                float move = moveSpeed.ValueRO.Speed * deltaTime;
                if (move > dist) move = dist;
                transform.ValueRW.Position += dir * move;
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(dir, math.up());
                return;
            }

            float3 lastWaypoint = waypointsInput.ValueRO.GetWaypoint(count - 1);
            bool isNewPath = !pathComponent.ValueRO.HasPath ||
                             math.distancesq(pathComponent.ValueRO.LastTargetPosition, lastWaypoint) > 0.01f;

            if (isNewPath)
            {
                float3 toFinal = lastWaypoint - transform.ValueRO.Position;
                toFinal.y = 0f;
                if (math.lengthsq(toFinal) < FINAL_POSITION_THRESHOLD * FINAL_POSITION_THRESHOLD)
                {
                    pathComponent.ValueRW.HasPath = false;
                    return;
                }

                pathComponent.ValueRW.HasPath = true;
                pathComponent.ValueRW.CurrentWaypointIndex = 0;
                pathComponent.ValueRW.LastTargetPosition = lastWaypoint;
            }

            int startIndex = math.clamp(pathComponent.ValueRO.CurrentWaypointIndex, 0, count - 1);
            float3 pos = transform.ValueRO.Position;

            for (int i = startIndex; i < count; i++)
            {
                float3 waypoint = waypointsInput.ValueRO.GetWaypoint(i);
                waypoint.y = pos.y;

                float3 toWaypoint = waypoint - pos;
                toWaypoint.y = 0f;
                float distance = math.length(toWaypoint);

                bool isLast = i == count - 1;
                float threshold = isLast ? FINAL_POSITION_THRESHOLD : WAYPOINT_THRESHOLD;

                if (distance < threshold)
                {
                    if (isLast)
                    {
                        pathComponent.ValueRW.HasPath = false;
                        return;
                    }
                    pathComponent.ValueRW.CurrentWaypointIndex = i + 1;
                    continue;
                }

                if (distance < 0.001f) return;

                float3 dir = toWaypoint / distance;
                float move = moveSpeed.ValueRO.Speed * deltaTime;
                if (move > distance) move = distance;
                transform.ValueRW.Position += dir * move;
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(dir, math.up());
                return;
            }
        }
    }
}

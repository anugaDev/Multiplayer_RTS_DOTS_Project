using ElementCommons;
using Units.Worker;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Units.MovementSystems
{
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(Navigation.NavMeshPathfindingSystem))]
    [BurstCompile]
    public partial struct UnitMoveSystem : ISystem
    {
        private const float FINAL_POSITION_THRESHOLD = 0.1f;
        private const float WAYPOINT_THRESHOLD = 0.5f;

        private DynamicBuffer<PathWaypointBuffer> _currentPathBuffer;
        private RefRO<UnitMoveSpeedComponent> _currentMoveSpeed;
        private RefRW<PathComponent> _currentPathComponent;
        private RefRW<LocalTransform> _currentTransform;

        private float3 _desiredDirection;
        private float _currentDeltaTime;
        private Entity _entity;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            _currentDeltaTime = SystemAPI.Time.DeltaTime;


            foreach ((RefRW<LocalTransform> transform,
                     RefRW<PathComponent> pathComponent,
                     DynamicBuffer<PathWaypointBuffer> pathBuffer,
                     RefRO<UnitMoveSpeedComponent> moveSpeed,
                     Entity entity)
                     in SystemAPI.Query<RefRW<LocalTransform>,
                                       RefRW<PathComponent>,
                                       DynamicBuffer<PathWaypointBuffer>,
                                       RefRO<UnitMoveSpeedComponent>>()
                         .WithAll<Simulate, UnitTagComponent>()
                         .WithEntityAccess())
            {
                _currentPathComponent = pathComponent;
                _currentPathBuffer = pathBuffer;
                _currentTransform = transform;
                _currentMoveSpeed = moveSpeed;
                _entity = entity;

                MoveUnit();
            }
        }

        private void MoveUnit()
        {
            if (!_currentPathComponent.ValueRO.HasPath)
                return;

            int count = _currentPathBuffer.Length;

            if (count == 0)
            {
                _currentPathComponent.ValueRW.HasPath = false;
                return;
            }

            int startIndex = math.clamp(_currentPathComponent.ValueRO.CurrentWaypointIndex, 0, count - 1);

            for (int i = startIndex; i < count; i++)
            {
                float3 waypoint = _currentPathBuffer[i].Position;
                waypoint.y = _currentTransform.ValueRO.Position.y;

                float3 toWaypoint = waypoint - _currentTransform.ValueRO.Position;
                toWaypoint.y = 0f;
                float distance = math.length(toWaypoint);

                bool isLast = i == count - 1;
                float threshold = isLast ? FINAL_POSITION_THRESHOLD : WAYPOINT_THRESHOLD;

                if (distance < threshold)
                {
                    if (isLast)
                    {
                        _currentPathComponent.ValueRW.HasPath = false;
                        return;
                    }
                    _currentPathComponent.ValueRW.CurrentWaypointIndex = i + 1;
                    continue;
                }

                MoveTowardsWaypoint(toWaypoint, distance);
                return;
            }
        }

        private void MoveTowardsWaypoint(float3 toWaypoint, float distanceToWaypoint)
        {
            if (distanceToWaypoint < 0.001f)
                return;

            _desiredDirection = math.normalize(toWaypoint);
            float speed = _currentMoveSpeed.ValueRO.Speed;
            float moveDistance = speed * _currentDeltaTime;

            _currentTransform.ValueRW.Position += _desiredDirection * moveDistance;
            _currentTransform.ValueRW.Rotation = quaternion.LookRotationSafe(_desiredDirection, math.up());
        }
    }
}
using ElementCommons;
using Units;
using Units.Worker;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

namespace Navigation
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [UpdateAfter(typeof(PlayerInputs.UnitMoveInputSystem))]
    public partial class NavMeshPathfindingSystem : SystemBase
    {
        private NavMeshPath _reusablePath;
        private int _walkableAreaMask;

        protected override void OnCreate()
        {
            _reusablePath = new NavMeshPath();
            _walkableAreaMask = 1 << NavMesh.GetAreaFromName("Walkable");
        }

        protected override void OnUpdate()
        {
            const float TARGET_CHANGE_THRESHOLD = 0.1f;

            foreach ((RefRO<LocalTransform> transform,
                     RefRW<SetInputStateTargetComponent> inputTarget,
                     RefRW<PathComponent> pathComponent,
                     DynamicBuffer<PathWaypointBuffer> pathBuffer,
                     RefRW<UnitWaypointsInputComponent> waypointsInput,
                     RefRO<ElementSelectionComponent> selection,
                     Entity entity)
                     in SystemAPI.Query<RefRO<LocalTransform>,
                                       RefRW<SetInputStateTargetComponent>,
                                       RefRW<PathComponent>,
                                       DynamicBuffer<PathWaypointBuffer>,
                                       RefRW<UnitWaypointsInputComponent>,
                                       RefRO<ElementSelectionComponent>>()
                         .WithAll<UnitTagComponent>()
                         .WithEntityAccess())
            {
                UpdateUnitPathfinding(inputTarget, pathComponent, TARGET_CHANGE_THRESHOLD,
                    pathBuffer, waypointsInput, transform, selection.ValueRO.IsSelected, entity);
            }
        }

        private void UpdateUnitPathfinding(RefRW<SetInputStateTargetComponent> inputTarget,
            RefRW<PathComponent> pathComponent, float TARGET_CHANGE_THRESHOLD,
            DynamicBuffer<PathWaypointBuffer> pathBuffer,
            RefRW<UnitWaypointsInputComponent> waypointsInput,
            RefRO<LocalTransform> transform, bool isSelected, Entity entity)
        {
            if (!inputTarget.ValueRO.HasNewTarget)
            {
                if (!pathComponent.ValueRO.HasPath && waypointsInput.ValueRO.WaypointCount > 0)
                    waypointsInput.ValueRW.WaypointCount = 0;
                return;
            }

            CheckUnitTarget(inputTarget, pathComponent, TARGET_CHANGE_THRESHOLD,
                pathBuffer, waypointsInput, transform, isSelected, entity);
        }

        private void CheckUnitTarget(RefRW<SetInputStateTargetComponent> inputTarget,
            RefRW<PathComponent> pathComponent, float TARGET_CHANGE_THRESHOLD,
            DynamicBuffer<PathWaypointBuffer> pathBuffer,
            RefRW<UnitWaypointsInputComponent> waypointsInput,
            RefRO<LocalTransform> transform, bool isSelected, Entity entity)
        {
            float3 targetPosition = inputTarget.ValueRO.TargetPosition;

            if (math.lengthsq(targetPosition) < 0.01f)
                return;

            const float NAVMESH_PRECISION_THRESHOLD = 1.0f;

            if (pathComponent.ValueRO.HasPath && pathBuffer.Length > 0)
            {
                float3 lastTarget = pathComponent.ValueRO.LastTargetPosition;
                if (math.lengthsq(lastTarget) > 0.01f)
                {
                    float distanceToLastTarget = math.distance(targetPosition, lastTarget);
                    
                    bool targetEntityChanged = inputTarget.ValueRO.IsFollowingTarget && 
                                             inputTarget.ValueRO.TargetEntity != pathComponent.ValueRO.LastTargetEntity;

                    if (distanceToLastTarget < NAVMESH_PRECISION_THRESHOLD && !targetEntityChanged)
                        return;
                }
            }

            SetUnitPath(inputTarget, pathComponent, pathBuffer, waypointsInput, transform, targetPosition, isSelected, entity);
        }

        private void SetUnitPath(RefRW<SetInputStateTargetComponent> inputTarget,
            RefRW<PathComponent> pathComponent,
            DynamicBuffer<PathWaypointBuffer> pathBuffer,
            RefRW<UnitWaypointsInputComponent> waypointsInput,
            RefRO<LocalTransform> transform,
            float3 targetPosition, bool isSelected, Entity entity)
        {
            pathBuffer.Clear();
            pathComponent.ValueRW.CurrentWaypointIndex = 0;
            pathComponent.ValueRW.HasPath = false;

            Vector3 startPos = transform.ValueRO.Position;
            Vector3 endPos = targetPosition;

            if (inputTarget.ValueRO.TargetEntity != Entity.Null && EntityManager.Exists(inputTarget.ValueRO.TargetEntity))
            {
                if (EntityManager.HasComponent<Buildings.BuildingObstacleSizeComponent>(inputTarget.ValueRO.TargetEntity) &&
                    EntityManager.HasComponent<LocalTransform>(inputTarget.ValueRO.TargetEntity))
                {
                    var buildingTransform = EntityManager.GetComponentData<LocalTransform>(inputTarget.ValueRO.TargetEntity);
                    var buildingSize = EntityManager.GetComponentData<Buildings.BuildingObstacleSizeComponent>(inputTarget.ValueRO.TargetEntity).Size;

                    float3 buildingCenter = buildingTransform.Position;
                    float3 halfExtents    = buildingSize * 0.5f;

                    float3 dir = new float3(startPos.x - buildingCenter.x, 0f, startPos.z - buildingCenter.z);

                    if (math.lengthsq(dir) < 0.001f)
                        dir = new float3(1f, 0f, 0f);

                    dir = math.normalize(dir);
                    float tx = dir.x != 0f ? math.abs(halfExtents.x / dir.x) : float.MaxValue;
                    float tz = dir.z != 0f ? math.abs(halfExtents.z / dir.z) : float.MaxValue;
                    float t  = math.min(tx, tz);

                    endPos = (Vector3)(buildingCenter + dir * (t + 0.5f));
                    endPos.y = startPos.y;
                }
            }

            if (NavMesh.SamplePosition(endPos, out NavMeshHit hit, 20.0f, _walkableAreaMask))
            {
                endPos = hit.position;
            }

            bool pathFound = NavMesh.CalculatePath(startPos, endPos, _walkableAreaMask, _reusablePath);

            if (pathFound)
                OnPathCalculated(inputTarget, pathComponent, pathBuffer, waypointsInput, endPos, isSelected, entity);
            else
                OnPathNotAvailable(inputTarget, pathBuffer, pathComponent, waypointsInput, endPos);
        }

        private void OnPathCalculated(RefRW<SetInputStateTargetComponent> inputTarget,
            RefRW<PathComponent> pathComponent,
            DynamicBuffer<PathWaypointBuffer> pathBuffer,
            RefRW<UnitWaypointsInputComponent> waypointsInput,
            float3 targetPosition, bool isSelected, Entity entity)
        {
            if (_reusablePath.status == NavMeshPathStatus.PathComplete)
                CompletePath(inputTarget, pathBuffer, pathComponent, waypointsInput, targetPosition, isSelected, entity);
            else
                RecalculatePath(inputTarget, pathBuffer, pathComponent, waypointsInput, targetPosition);
        }

        private void OnPathNotAvailable(RefRW<SetInputStateTargetComponent> inputTarget,
            DynamicBuffer<PathWaypointBuffer> pathBuffer,
            RefRW<PathComponent> pathComponent,
            RefRW<UnitWaypointsInputComponent> waypointsInput,
            float3 targetPosition)
        {
            pathBuffer.Clear();
            pathBuffer.Add(new PathWaypointBuffer { Position = targetPosition });
            pathComponent.ValueRW.HasPath = true;
            pathComponent.ValueRW.CurrentWaypointIndex = 0;
            pathComponent.ValueRW.LastTargetPosition = targetPosition;
            pathComponent.ValueRW.LastTargetEntity = inputTarget.ValueRO.TargetEntity;
            WriteWaypointsInput(waypointsInput, pathBuffer);
            inputTarget.ValueRW.HasNewTarget = false;
        }

        private void RecalculatePath(RefRW<SetInputStateTargetComponent> inputTarget,
            DynamicBuffer<PathWaypointBuffer> pathBuffer,
            RefRW<PathComponent> pathComponent,
            RefRW<UnitWaypointsInputComponent> waypointsInput,
            float3 targetPosition)
        {
            pathBuffer.Clear();
            pathBuffer.Add(new PathWaypointBuffer { Position = targetPosition });
            pathComponent.ValueRW.HasPath = true;
            pathComponent.ValueRW.CurrentWaypointIndex = 0;
            pathComponent.ValueRW.LastTargetPosition = targetPosition;
            pathComponent.ValueRW.LastTargetEntity = inputTarget.ValueRO.TargetEntity;
            WriteWaypointsInput(waypointsInput, pathBuffer);
            inputTarget.ValueRW.HasNewTarget = false;
        }

        private void CompletePath(RefRW<SetInputStateTargetComponent> inputTarget,
            DynamicBuffer<PathWaypointBuffer> pathBuffer,
            RefRW<PathComponent> pathComponent,
            RefRW<UnitWaypointsInputComponent> waypointsInput,
            float3 targetPosition, bool isSelected, Entity entity)
        {
            pathBuffer.Clear();

            for (int i = 1; i < _reusablePath.corners.Length; i++)
                pathBuffer.Add(new PathWaypointBuffer { Position = _reusablePath.corners[i] });

            if (pathBuffer.Length == 0)
                pathBuffer.Add(new PathWaypointBuffer { Position = targetPosition });

            pathComponent.ValueRW.HasPath = true;
            pathComponent.ValueRW.CurrentWaypointIndex = 0;
            pathComponent.ValueRW.LastTargetPosition = targetPosition;
            pathComponent.ValueRW.LastTargetEntity = inputTarget.ValueRO.TargetEntity;

            WriteWaypointsInput(waypointsInput, pathBuffer);

            inputTarget.ValueRW.HasNewTarget = false;
        }

        private static void WriteWaypointsInput(
            RefRW<UnitWaypointsInputComponent> waypointsInput,
            DynamicBuffer<PathWaypointBuffer> pathBuffer)
        {
            int count = math.min(pathBuffer.Length, 8);
            var w = waypointsInput.ValueRW;
            w.WaypointCount = count;
            for (int i = 0; i < count; i++)
                w.SetWaypoint(i, pathBuffer[i].Position);
            waypointsInput.ValueRW = w;
        }
    }
}

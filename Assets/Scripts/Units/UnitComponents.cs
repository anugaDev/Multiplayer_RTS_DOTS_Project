using Types;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Units
{
    public struct UnitTagComponent : IComponentData
    {
    }

    public struct NewUnitTagComponent : IComponentData
    {
    }

    public struct UnitMoveSpeedComponent : IComponentData
    {
        public float Speed;
    }

    public struct UnitTypeComponent : IComponentData
    {
        public UnitType Type;
    }

    public class UnitMaterialsComponent : IComponentData
    {
        public Material BlueTeamMaterial;

        public Material RedTeamMaterial;
    }
    
    /// <summary>
    /// CLIENT-ONLY: Path data for NavMesh pathfinding.
    /// Not synchronized because server doesn't have NavMesh.
    /// Server receives final positions through LocalTransform synchronization.
    /// </summary>
    /// <summary>
    /// AllPredicted (not PredictedClient) so PathComponent exists on SERVER entities too.
    /// ServerUnitMoveSystem uses PathComponent.CurrentWaypointIndex to track progress
    /// through the NavMesh waypoints received via UnitWaypointsInputComponent.
    /// No [GhostField] attributes = nothing is serialized; component just exists on both sides.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PathComponent : IComponentData
    {
        public float3 LastTargetPosition;
        public Entity LastTargetEntity;
        public int CurrentWaypointIndex;
        public bool HasPath;
    }

    /// <summary>
    /// CLIENT-ONLY: Waypoint buffer for NavMesh paths.
    /// Not synchronized because server doesn't have NavMesh.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
    public struct PathWaypointBuffer : IBufferElementData
    {
        public float3 Position;
    }
    
    public struct UnitAttackingTagComponent : IComponentData
    {
        [GhostField]
        public Entity TargetEntity;
    }

    public enum UnitState : byte
    {
        Idle   = 0,
        Moving = 1,
        Acting = 2
    }

    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct UnitStateComponent : IComponentData
    {
        [GhostField] public UnitState State;
    }
}
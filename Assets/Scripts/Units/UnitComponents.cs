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

    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PathComponent : IComponentData
    {
        public float3 LastTargetPosition;

        public Entity LastTargetEntity;

        public int CurrentWaypointIndex;

        public bool HasPath;
    }

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

    public struct UnitCombatPropertiesComponent : IComponentData
    {
        public int Damage;

        public float AttackRange;
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
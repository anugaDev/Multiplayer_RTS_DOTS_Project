using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Units.MovementSystems
{
    public struct UnitTargetRadius : IComponentData
    {
        public float Value;
    }

    public struct UnitAttackRange : IComponentData
    {
        public float Value;
    }

    public struct UnitTargetEntity : IComponentData
    {
        [GhostField] public Entity Value;
    }
    
    public struct UnitAttackProperties : IComponentData
    {
        public float3 FirePointOffset;

        public uint CooldownTickCount;

        public int Damage;
    }

    public struct UnitAttackCooldown : ICommandData
    {
        public NetworkTick Tick { get; set; }

        public NetworkTick Value;
    }
}
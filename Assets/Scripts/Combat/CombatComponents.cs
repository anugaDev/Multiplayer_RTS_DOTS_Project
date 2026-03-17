using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Combat
{
    public struct MaxHitPointsComponent : IComponentData
    {
        public int Value;
    }

    public struct CurrentHitPointsComponent : IComponentData
    {
        [GhostField] 
        public int Value;
    }

    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct DamageBufferElement : IBufferElementData
    {
        public int Value;
    }

    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted, OwnerSendType = SendToOwnerType.SendToNonOwner)]
    public struct CurrentTickDamageCommand : ICommandData
    {
        public NetworkTick Tick { get; set; }

        public int Value;
    }

    public struct DestroyOnTimer : IComponentData
    {
        public float Value;
    }

    public struct DestroyAtTick : IComponentData
    {
        [GhostField] 
        public NetworkTick Value;
    }

    public struct DestroyEntityTag : IComponentData
    {
        
    }

    public struct DamageOnTrigger : IComponentData
    {
        public int Value;
    }

    public struct AlreadyDamagedEntity : IBufferElementData
    {
        public Entity Value;
    }
}
using Audio;
using ElementCommons;
using Types;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Combat
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderLast = true)]
    public partial struct DestroyEntitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();

            if (!networkTime.IsFirstTimeFullyPredictingTick)
            {
                return;
            }

            BeginSimulationEntityCommandBufferSystem.Singleton ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach ((RefRO<CurrentHitPointsComponent> hp, Entity entity) in SystemAPI
                         .Query<RefRO<CurrentHitPointsComponent>>()
                         .WithAll<Simulate>()
                         .WithNone<DestroyEntityTag>()
                         .WithEntityAccess())
            {
                if (hp.ValueRO.Value <= 0)
                {
                    SetDestroyedEntitySound(entity, state);
                    entityCommandBuffer.AddComponent<DestroyEntityTag>(entity);
                }
            }

            foreach ((RefRW<LocalTransform> transform, Entity entity) in SystemAPI.Query<RefRW<LocalTransform>>()
                         .WithAll<DestroyEntityTag, Simulate>().WithEntityAccess())
            {
                if (state.World.IsServer())
                {

                    entityCommandBuffer.DestroyEntity(entity);
                }
                else
                {
                    transform.ValueRW.Position = new float3(1000f, 1000f, 1000f);
                }
            }
        }

        private void SetDestroyedEntitySound(Entity entity, SystemState state)
        {
            AudioSourceType audioSourceType = GetAudioSourceType(entity, state);
            float3 position = SystemAPI.GetComponent<LocalTransform>(entity).Position;
            SetDestroyedSoundFeedback(audioSourceType, position, state);
        }

        private AudioSourceType GetAudioSourceType(Entity entity, SystemState state)
        {
            SelectableElementType elementType = SystemAPI.GetComponent<SelectableElementTypeComponent>(entity).Type;

            if (elementType == SelectableElementType.Unit)
            {
                return AudioSourceType.DeathShout;
            }

            return AudioSourceType.DestroyedBuilding;
        }

        private void SetDestroyedSoundFeedback(AudioSourceType audioSourceType, float3 position, SystemState state)
        {
            Entity audioEntity = SystemAPI.ManagedAPI.GetSingletonEntity<AudioManagerReferenceComponent>();
            AudioRequestComponent audioRequest = new AudioRequestComponent
            {
                AudioId = audioSourceType,
                Is3D = true
            };

            SystemAPI.SetComponent(audioEntity, audioRequest);
        }
    }
}
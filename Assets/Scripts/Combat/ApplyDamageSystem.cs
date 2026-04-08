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
    [UpdateBefore(typeof(DestroyEntitySystem))]
    public partial struct ApplyDamageSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();

            if (!networkTime.IsFirstTimeFullyPredictingTick) return;

            foreach ((RefRW<CurrentHitPointsComponent>   hp,
                      DynamicBuffer<DamageBufferElement> damageBuffer,
                      Entity entity)
                     in SystemAPI.Query<RefRW<CurrentHitPointsComponent>, 
                             DynamicBuffer<DamageBufferElement>>().WithAll<Simulate>().WithEntityAccess())
            {
                SetCurrentHealth(damageBuffer, hp, entity, state);
            }
        }

        private void SetCurrentHealth(DynamicBuffer<DamageBufferElement> damageBuffer,
            RefRW<CurrentHitPointsComponent> hp, Entity entity, SystemState state)
        {
            if (damageBuffer.Length == 0)
            {
                return;
            }

            SetDamagedEntitySound(entity, state);
            int totalDamage = GetTotalDamageBuffer(damageBuffer);
            hp.ValueRW.Value -= totalDamage;
            damageBuffer.Clear();
        }

        private int GetTotalDamageBuffer(DynamicBuffer<DamageBufferElement> damageBuffer)
        {
            int totalDamage = 0;
            for (int i = 0; i < damageBuffer.Length; i++)
            {
                totalDamage += damageBuffer[i].Value;
            }
            return totalDamage;
        }
        
        
        private void SetDamagedEntitySound(Entity entity, SystemState state)
        {
            AudioSourceType audioSourceType = GetAudioSourceType(entity, state);
            float3 position = SystemAPI.GetComponent<LocalTransform>(entity).Position;
            SetDamagedSoundFeedback(audioSourceType, position, state);
        }

        private AudioSourceType GetAudioSourceType(Entity entity, SystemState state)
        {
            SelectableElementType elementType = SystemAPI.GetComponent<SelectableElementTypeComponent>(entity).Type;

            if (elementType == SelectableElementType.Unit)
            {
                return AudioSourceType.DamageShout;
            }

            return AudioSourceType.DamageBuilding;
        }

        private void SetDamagedSoundFeedback(AudioSourceType audioSourceType, float3 position, SystemState state)
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

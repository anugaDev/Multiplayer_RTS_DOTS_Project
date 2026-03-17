using Unity.Entities;
using Unity.NetCode;

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
                      Entity                             entity)
                     in SystemAPI.Query<RefRW<CurrentHitPointsComponent>,
                                        DynamicBuffer<DamageBufferElement>>()
                         .WithAll<Simulate>()
                         .WithEntityAccess())
            {
                SetCurrentHealth(damageBuffer, hp);
            }
        }

        private void SetCurrentHealth(DynamicBuffer<DamageBufferElement> damageBuffer, RefRW<CurrentHitPointsComponent> hp)
        {
            if (damageBuffer.Length == 0)
            {
                return;
            }

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
    }
}

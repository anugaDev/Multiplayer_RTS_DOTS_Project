using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

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
                if (damageBuffer.Length == 0) continue;

                int totalDamage = 0;
                for (int i = 0; i < damageBuffer.Length; i++)
                    totalDamage += damageBuffer[i].Value;

                int hpBefore = hp.ValueRO.Value;
                hp.ValueRW.Value -= totalDamage;
                damageBuffer.Clear();

                Debug.Log($"[DAMAGE] Entity {entity.Index}: HP {hpBefore} → {hp.ValueRO.Value}  (took {totalDamage} damage)");
            }
        }
    }
}

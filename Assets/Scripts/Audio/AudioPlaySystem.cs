using Unity.Entities;

namespace Audio
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class AudioPlaySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.TryGetSingleton<AudioManagerReferenceComponent>(out AudioManagerReferenceComponent managerComp))
            {
                return; 
            }
            
            AudioManager audioManager = managerComp.ManagerReference;

            EntityCommandBuffer ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach ((RefRO<AudioRequestComponent> request, Entity entity) in SystemAPI.Query<RefRO<AudioRequestComponent>>().WithEntityAccess())
            {
                audioManager.PlaySound(request.ValueRO.AudioId, request.ValueRO.Position, request.ValueRO.Is3D);
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
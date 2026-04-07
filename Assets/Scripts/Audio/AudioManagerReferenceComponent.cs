using Unity.Entities;

namespace Audio
{
    public class AudioManagerReferenceComponent : IComponentData
    {
        public AudioManager ManagerReference;
    }
}

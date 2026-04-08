using Unity.Entities;
using Unity.Mathematics;

namespace Audio
{
    public struct AudioRequestComponent : IComponentData
    {
        public AudioSourceType AudioId;

        public float3 Position;

        public bool Is3D;
    }
}

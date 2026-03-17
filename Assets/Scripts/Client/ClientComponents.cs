using Types;
using Unity.Entities;

namespace Client
{
    public struct ClientTeamRequest : IComponentData
    {
        public TeamType Value;
    }
}
using Types;
using Unity.Entities;
using Unity.NetCode;

namespace Combat
{
    public struct GameOverTag : IComponentData
    {
        [GhostField]
        public TeamType WinnerTeam;
    }

    public struct CheckVictoryDelay : IComponentData
    {
        public int FramesRemaining;
    }
}

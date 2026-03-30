using Types;
using UI.UIControllers;
using Unity.Entities;

namespace Combat
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct GameOverSystem : ISystem
    {
        private bool _gameOverHandled;

        public void OnCreate(ref SystemState state)
        {
            _gameOverHandled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_gameOverHandled)
                return;

            foreach ((RefRO<GameOverTag> gameOverTag, RefRO<UI.PlayerTeamComponent> playerTeam) in
                     SystemAPI.Query<RefRO<GameOverTag>, RefRO<UI.PlayerTeamComponent>>()
                              .WithAll<UI.PlayerTagComponent, ElementCommons.OwnerTagComponent>())
            {
                if (gameOverTag.ValueRO.WinnerTeam != TeamType.None)
                {
                    bool isVictory = playerTeam.ValueRO.Team == gameOverTag.ValueRO.WinnerTeam;

                    GameOverScreenController screenController = GameOverScreenController.Instance;
                    if (screenController != null)
                    {
                        screenController.Show(isVictory);
                    }

                    _gameOverHandled = true;
                }
                break;
            }
        }
    }
}

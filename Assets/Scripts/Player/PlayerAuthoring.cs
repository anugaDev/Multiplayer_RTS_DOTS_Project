using Buildings;
using ElementCommons;
using UI;
using Unity.Entities;
using UnityEngine;

namespace Player
{
    public class PlayerAuthoring : MonoBehaviour
    {
        private const int STARTING_POPULATION = 1;

        [SerializeField]
        private int _startingFood;
        
        [SerializeField]
        private int _startingWood;

        [SerializeField]
        private int _startingMaxPopulation;

        public int StartingFood => _startingFood;

        public int StartingWood => _startingWood;

        public int StartingMaxPopulation => _startingMaxPopulation;

        public class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring playerAuthoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddBuffer<UpdateUIActionPayload>(entity);
                AddBuffer<PlaceBuildingCommand>(entity);
                AddBuffer<SpawnUnitCommand>(entity);
                AddBuffer<EnableUIActionBuffer>(entity);
                AddBuffer<DisableUIActionBuffer>(entity);
                AddBuffer<QueueUnitCommand>(entity);
                AddComponent<OwnerTagComponent>(entity);
                AddComponent<PlayerTeamComponent>(entity);
                AddComponent<UpdateResourcesPanelTag>(entity);
                AddComponent(entity, new PlayerTagComponent());
                AddComponent(entity, GetCurrentWoodComponent(playerAuthoring));
                AddComponent(entity, GetCurrentFoodComponent(playerAuthoring));
                AddComponent(entity, GetCurrentPopulationComponent(playerAuthoring));
                AddComponent(entity, new FoodGenerationComponent { FoodPerSecond = 0 });
                AddComponent(entity, new Combat.GameOverTag { WinnerTeam = Types.TeamType.None });
            }

            private CurrentFoodComponent GetCurrentFoodComponent(PlayerAuthoring playerAuthoring)
            {
                return new CurrentFoodComponent
                {
                    Value = playerAuthoring.StartingFood
                };
            }

            private CurrentWoodComponent GetCurrentWoodComponent(PlayerAuthoring playerAuthoring)
            {
                return new CurrentWoodComponent
                {
                    Value = playerAuthoring.StartingWood
                };
            }

            private CurrentPopulationComponent GetCurrentPopulationComponent(PlayerAuthoring playerAuthoring)
            {
                return new CurrentPopulationComponent
                {
                    MaxPopulation = playerAuthoring.StartingMaxPopulation,
                    CurrentPopulation = STARTING_POPULATION
                };
            }
        }
    }
}
using ElementCommons;
using PlayerInputs;
using UI.UIControllers;
using Unity.Collections;
using Unity.Entities;

namespace UI
{
    [UpdateAfter(typeof(UnitMoveInputSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class UserInterfaceResourcesPanelSystem : SystemBase
    {
        private EntityCommandBuffer _entityCommandBuffer;

        private ResourcesPanelController _resourcesPanelController;

        protected override void OnCreate()
        {
            RequireForUpdate<UISceneReferenceComponent>();
            RequireForUpdate<OwnerTagComponent>();
            base.OnCreate();
        }

        protected override void OnStartRunning()
        {
            UISceneReferenceComponent uiSceneReferenceComponent = SystemAPI.ManagedAPI.GetSingleton<UISceneReferenceComponent>();
            _resourcesPanelController = uiSceneReferenceComponent.UIReference.ResourcesPanelController;
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            _entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach ((UpdateResourcesPanelTag updateTag, OwnerTagComponent _, Entity entity) in 
                     SystemAPI.Query<UpdateResourcesPanelTag, OwnerTagComponent>().WithEntityAccess())
            {
                UpdatePanelResources(entity); 
                _entityCommandBuffer.RemoveComponent<UpdateResourcesPanelTag>(entity);
            }
            
            _entityCommandBuffer.Playback(EntityManager);
        }

        private void UpdatePanelResources(Entity entity)
        {
            
            UpdateFood(entity);
            UpdateWood(entity);
            UpdatePopulation(entity);
        }

        private void UpdatePopulation(Entity entity)
        {
            CurrentPopulationComponent currentPopulation = EntityManager.GetComponentData<CurrentPopulationComponent>(entity);
            _resourcesPanelController.SetPopulationText(currentPopulation.CurrentPopulation, currentPopulation.MaxPopulation);
        }

        private void UpdateWood(Entity entity)
        {
            CurrentWoodComponent currentWood = EntityManager.GetComponentData<CurrentWoodComponent>(entity);
            _resourcesPanelController.SetWoodText(currentWood.Value);
        }

        private void UpdateFood(Entity entity)
        {
            CurrentFoodComponent currentFood = EntityManager.GetComponentData<CurrentFoodComponent>(entity);
            _resourcesPanelController.SetFoodText(currentFood.Value);
        }
    }
}
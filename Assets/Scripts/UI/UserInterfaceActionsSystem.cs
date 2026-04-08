using Audio;
using Buildings;
using ElementCommons;
using ScriptableObjects;
using UI.UIControllers;
using Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace UI
{
    [UpdateAfter(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(UserInterfaceActionValidateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class UserInterfaceActionsSystem : SystemBase
    {
        private SelectionActionsDisplayController _selectionActionsController;

        protected override void OnCreate()
        {
            RequireForUpdate<OwnerTagComponent>();
            RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<BuildingsConfigurationComponent>();
            RequireForUpdate<UnitsConfigurationComponent>();
            RequireForUpdate<UISceneReferenceComponent>();
        }

        protected override void OnStartRunning()
        {
            UISceneReferenceComponent uiSceneReferenceComponent = SystemAPI.ManagedAPI.GetSingleton<UISceneReferenceComponent>();
            _selectionActionsController = uiSceneReferenceComponent.UIReference.SelectionActionsDisplayerController;
            _selectionActionsController.OnActionSelected += SetPlayerUIActionComponent;
            _selectionActionsController.OnActionEnter += SetActonPopUpEnabled;
            _selectionActionsController.OnActionExit += SetActionPopUpDisabled;
            SetBuildingActions();
            SetRecruitmentActions();
            base.OnStartRunning();
        }

        private void SetActionPopUpDisabled()
        {
            _selectionActionsController.CostPopUpView.Disable();
        }

        private void SetActonPopUpEnabled(ActionPopUpPayload popUpPayload, Vector2 screenPosition)
        {
            ActionCostPopUpView popUpView = _selectionActionsController.CostPopUpView;
            popUpView.Enable();
            popUpView.SetPosition(screenPosition);
            popUpView.SetTitleText(popUpPayload.Name);
            popUpView.SetDescription(popUpPayload.Description);
            popUpView.SetCostTexts(popUpPayload.ResourceCost);
        }

        private void SetRecruitmentActions()
        {
            UnitsScriptableObject configuration = SystemAPI.ManagedAPI.GetSingleton<UnitsConfigurationComponent>().Configuration;
            _selectionActionsController.SetRecruitmentActions(configuration);
        }

        private void SetBuildingActions()
        {
            BuildingsScriptableObject configuration = SystemAPI.ManagedAPI.GetSingleton<BuildingsConfigurationComponent>().Configuration;
            _selectionActionsController.SetBuildingActions(configuration);
        }

        private void SetPlayerUIActionComponent(SetPlayerUIActionComponent actionComponent)
        {
            SetSelectionSoundFeedback();
            Entity uiEntity = SystemAPI.GetSingletonEntity<PlayerTagComponent>();
            if (!EntityManager.HasComponent<SetPlayerUIActionComponent>(uiEntity))
            {
                EntityManager.AddComponentData(uiEntity, actionComponent);
            }
        }

        private void SetSelectionSoundFeedback()
        {
            Entity audioRequestEntity = EntityManager.CreateEntity(typeof(AudioRequestComponent));
            EntityManager.SetComponentData(audioRequestEntity, new AudioRequestComponent
            {
                AudioId = AudioSourceType.SelectedAction,
                Is3D = false
            });
        }

        protected override void OnStopRunning()
        {
            _selectionActionsController.OnActionSelected -= SetPlayerUIActionComponent;
            base.OnStopRunning();
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach ((DynamicBuffer<EnableUIActionBuffer> enableBuffer, Entity entity)
                in SystemAPI.Query<DynamicBuffer<EnableUIActionBuffer>>().WithEntityAccess())
            {
                foreach (EnableUIActionBuffer enableAction in enableBuffer)
                {
                    _selectionActionsController.EnableAction(enableAction);
                }
                enableBuffer.Clear();
            }

            foreach ((DynamicBuffer<DisableUIActionBuffer> disableBuffer, Entity entity)
                     in SystemAPI.Query<DynamicBuffer<DisableUIActionBuffer>>().WithEntityAccess())
            {
                foreach (DisableUIActionBuffer disableAction in disableBuffer)
                {
                    _selectionActionsController.DisableAction(disableAction);
                }
                disableBuffer.Clear();
            }

            foreach ((RefRO<UpdateUIActionTag> updateUIActionTag, DynamicBuffer<UpdateUIActionPayload> buffer, Entity entity) in
                     SystemAPI.Query<RefRO<UpdateUIActionTag>, DynamicBuffer<UpdateUIActionPayload>>()
                         .WithEntityAccess())
            {
                _selectionActionsController.SetActionsActive(buffer);
                entityCommandBuffer.RemoveComponent<UpdateUIActionTag>(entity);
            }

            entityCommandBuffer.Playback(EntityManager);
        }
    }
}
using System;
using System.Collections.Generic;
using Buildings;
using ElementCommons;
using ScriptableObjects;
using Types;
using UI.Entities;
using UI.UIControllers;
using Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace UI
{
    [UpdateAfter(typeof(TransformSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class UserInterfaceGroupSystem : SystemBase
    {
        private const float DEFAULT_FILL_AMOUNT = 0F;

        private SelectionActionsDisplayController selectionActionsController;

        private List<UnitUIGroupQueue> _trackedElementQueue;

        private SelectedGroupDisplayController _selectionGroupsController;

        private Dictionary<SelectableElementType, Action<Entity, bool>> _selectableToAction;

        private Dictionary<UnitType, int> _currentUnitSelection;

        private SelectableElementType _currentSelection;

        private RecruitmentProgressComponent _currentTrackedRecruitment;
        
        private bool _isRecruitmentTracked;

        private bool _anyUnitSelected;
        
        private EntityCommandBuffer _entityCommandBuffer;

        protected override void OnCreate()
        {
            RequireForUpdate<UnitsConfigurationComponent>();
            RequireForUpdate<UISceneReferenceComponent>();
            RequireForUpdate<OwnerTagComponent>();
            InitializeSelectionDictionary();
            InitializeActionDictionary();
        }

        private void InitializeActionDictionary()
        {
            _selectableToAction = new Dictionary<SelectableElementType, Action<Entity, bool>>
            {
                [SelectableElementType.Building] = SetBuildingQueue,
                [SelectableElementType.Unit] = SetUnitGroup,
                [SelectableElementType.Resource] = SetResourceGroup
            };
        }

        private void InitializeSelectionDictionary()
        {
            _currentUnitSelection = new Dictionary<UnitType, int>
            {
                [UnitType.Archer] = 0,
                [UnitType.Ballista] = 0,
                [UnitType.Worker] = 0,
                [UnitType.Warrior] = 0
            };
        }

        private void SetUnitGroup(Entity entity, bool isSelected)
        {
            UnitType unitType = SystemAPI.GetComponent<UnitTypeComponent>(entity).Type;
            int currentSelectionCount = _currentUnitSelection[unitType];
            currentSelectionCount = GetCurrentSelectionCount(currentSelectionCount, isSelected);
            _currentUnitSelection[unitType] = currentSelectionCount;
        }
        private void SetResourceGroup(Entity entity, bool isSelected)
        {
            return;
        }

        private int GetCurrentSelectionCount(int currentSelectionCount, bool isSelected)
        {
            if (!isSelected)
            {
                return GetNegativeSelectionCount(currentSelectionCount);
            }
            
            return currentSelectionCount + 1;
        }

        private int GetNegativeSelectionCount(int currentSelectionCount)
        {
            if(currentSelectionCount <= 0)
            {
                return 0;
            }

            return currentSelectionCount - 1;
        }

        private void SetBuildingQueue(Entity entity, bool isSelected)
        {
            if(!isSelected)
            {
                return;
            }

            DynamicBuffer<RecruitmentQueueBufferComponent> recruitmentBuffer = EntityManager.GetBuffer<RecruitmentQueueBufferComponent>(entity);
            _currentTrackedRecruitment = EntityManager.GetComponentData<RecruitmentProgressComponent>(entity);
            _isRecruitmentTracked = recruitmentBuffer.Length > 0;
            ResetSelection();
            UpdateSelectionToBuffer(recruitmentBuffer);
        }

        private void UpdateSelectionToBuffer(DynamicBuffer<RecruitmentQueueBufferComponent> recruitmentBuffer)
        {
            foreach (RecruitmentQueueBufferComponent queueComponent in recruitmentBuffer)
            {
                _currentUnitSelection[queueComponent.unitType]++;
            }
        }

        private void ResetSelection()
        {

            List<UnitType> keys = new List<UnitType>(_currentUnitSelection.Keys);
            foreach (UnitType unitType in keys)
            {
                _currentUnitSelection[unitType] = 0;
            }
        }

        protected override void OnStartRunning()
        {
            InitializeController();
            base.OnStartRunning();
        }

        private void InitializeController()
        {
            UISceneReferenceComponent uiSceneReferenceComponent = SystemAPI.ManagedAPI.GetSingleton<UISceneReferenceComponent>();
            UnitsScriptableObject configuration = SystemAPI.ManagedAPI.GetSingleton<UnitsConfigurationComponent>().Configuration;
            _selectionGroupsController = uiSceneReferenceComponent.UIReference.SelectedGroupController;
            _selectionGroupsController.SetUnitsGroups(configuration);
        }

        protected override void OnUpdate()
        {
            GetSelectedElements();
        }

        private void GetSelectedElements()
        {
            _entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach ((ElementSelectionComponent selectionComponent, SelectableElementTypeComponent typeComponent, Entity entity) 
                     in SystemAPI.Query<ElementSelectionComponent, SelectableElementTypeComponent>().WithEntityAccess())
            {
                CheckAnyUnitSelected(typeComponent, selectionComponent);
                SetGroupOnUpdateUI(selectionComponent, entity, typeComponent);
            }

            _entityCommandBuffer.Playback(EntityManager);
            UpdateSelectedGroups();
            UpdateRecruitmentProgress();
            ResetOnUpdate();
            _isRecruitmentTracked = false;
        }

        private void SetGroupOnUpdateUI(ElementSelectionComponent selectionComponent, Entity entity,
            SelectableElementTypeComponent typeComponent)
        {
            if (!selectionComponent.MustUpdateGroup)
            {
                return;
            }

            ElementSelectionComponent newSelectionComponent = selectionComponent;
            newSelectionComponent.MustUpdateGroup = false;
            _entityCommandBuffer.SetComponent(entity, newSelectionComponent);
            _selectableToAction[typeComponent.Type]?.Invoke(entity, selectionComponent.IsSelected);
        }

        private void CheckAnyUnitSelected(SelectableElementTypeComponent typeComponent, ElementSelectionComponent selectionComponent)
        {
            if(_anyUnitSelected)
            {
                return;
            }

            _anyUnitSelected = selectionComponent.IsSelected && typeComponent.Type is SelectableElementType.Unit;
        }

        private void ResetOnUpdate()
        {
            if (_anyUnitSelected)
            {
                _anyUnitSelected = false;
                return;
            }

            ResetSelection();
        }

        private void UpdateRecruitmentProgress()
        {
            if (!_isRecruitmentTracked)
            {
                return;
            }

            _selectionGroupsController.SetGroupFill(_currentTrackedRecruitment.UnitType, _currentTrackedRecruitment.Value);
        }

        private void UpdateSelectedGroups()
        {
            List<UnitType> keys = new List<UnitType>(_currentUnitSelection.Keys);
            foreach (UnitType unitType in keys)
            {
                _selectionGroupsController.SetGroupValue(unitType, _currentUnitSelection[unitType]);
                _selectionGroupsController.SetGroupFill(unitType, DEFAULT_FILL_AMOUNT);
            }
        }
    }
}
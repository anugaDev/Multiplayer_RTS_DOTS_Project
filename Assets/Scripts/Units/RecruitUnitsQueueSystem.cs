using System.Collections.Generic;
using System.Linq;
using Buildings;
using ElementCommons;
using GatherableResources;
using ScriptableObjects;
using Types;
using UI;
using UI.Entities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Units
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial class RecruitUnitsQueueSystem : SystemBase
    {
        private BuildingFactoryActionsFactory _buildingActionsFactory;

        private Dictionary<UnitType, UnitScriptableObject> _unitsConfiguration;
        
        private List<RecruitmentEntity> _recruitmentList;
        
        private List<RecruitmentEntity> _recuritmentQueue;

        private List<RecruitmentEntity> _endRecruitmentUnits;
        
        private EntityCommandBuffer _entityCommandBuffer;
        
        private ElementResourceCostPolicy _elementResourceCostPolicy;

        private int _pendingWoodCost;
        private int _pendingFoodCost;
        private int _pendingPopulationCost;
        private int _previousServerWood;
        private int _previousServerFood;
        private int _previousServerPopulation;

        protected override void OnCreate()
        {
            RequireForUpdate<UnitsConfigurationComponent>();
            RequireForUpdate<PlayerTagComponent>();
            _buildingActionsFactory = new BuildingFactoryActionsFactory();
            _elementResourceCostPolicy = new ElementResourceCostPolicy();
            base.OnCreate();
        }

        protected override void OnStartRunning()
        {
            _unitsConfiguration = SystemAPI.ManagedAPI.GetSingleton<UnitsConfigurationComponent>().Configuration.GetUnitsDictionary();
            _recruitmentList = new List<RecruitmentEntity>();
            _recuritmentQueue = new List<RecruitmentEntity>();
            _endRecruitmentUnits = new List<RecruitmentEntity>();
            Entity playerEntity = SystemAPI.GetSingletonEntity<PlayerTagComponent>();
            _previousServerWood = SystemAPI.GetComponent<CurrentWoodComponent>(playerEntity).Value;
            _previousServerFood = SystemAPI.GetComponent<CurrentFoodComponent>(playerEntity).Value;
            _previousServerPopulation = SystemAPI.GetComponent<CurrentPopulationComponent>(playerEntity).CurrentPopulation;
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            _entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            CheckRecruitmentActions();
            UpdateUnitRecruitment();
            CheckRecruitmentQueue();
            RemoveEndedRecruitmentUnits();
            _entityCommandBuffer.Playback(EntityManager);
            _entityCommandBuffer.Dispose();
        }

        private void UpdatePolicy(Entity playerEntity)
        {
            int serverWood = SystemAPI.GetComponent<CurrentWoodComponent>(playerEntity).Value;
            int serverFood = SystemAPI.GetComponent<CurrentFoodComponent>(playerEntity).Value;
            CurrentPopulationComponent populationComponent = SystemAPI.GetComponent<CurrentPopulationComponent>(playerEntity);
            int serverPopulation = populationComponent.CurrentPopulation;
            int maxPopulation = populationComponent.MaxPopulation;
            int serverWoodDrop = _previousServerWood - serverWood;
            int serverFoodDrop = _previousServerFood - serverFood;
            int serverPopGain  = serverPopulation - _previousServerPopulation;
            if (serverWoodDrop > 0) _pendingWoodCost       = math.max(0, _pendingWoodCost       - serverWoodDrop);
            if (serverFoodDrop > 0) _pendingFoodCost       = math.max(0, _pendingFoodCost       - serverFoodDrop);
            if (serverPopGain  > 0) _pendingPopulationCost = math.max(0, _pendingPopulationCost - serverPopGain);
            _previousServerWood = serverWood;
            _previousServerFood  = serverFood;
            _previousServerPopulation = serverPopulation;
            int effectiveWood = math.max(0, serverWood - _pendingWoodCost);
            int effectiveFood = math.max(0, serverFood - _pendingFoodCost);
            int effectivePopulation = serverPopulation + _pendingPopulationCost;
            _elementResourceCostPolicy.UpdateCost(effectiveWood, effectiveFood, effectivePopulation, maxPopulation);
        }

        private void UpdateUnitRecruitment()
        {
            foreach (RecruitmentEntity recruitmentEntity in _recruitmentList)
            {
                UpdateRecruitment(recruitmentEntity);
            }
        }

        private void UpdateRecruitment(RecruitmentEntity recruitmentEntity)
        {
            recruitmentEntity.Update(SystemAPI.Time.DeltaTime);
            Entity buildingEntity = recruitmentEntity.Entity;
            float progress = recruitmentEntity.GetProgress();
            UnitType recruitmentEntityUnit = recruitmentEntity.Unit;
            EntityManager.SetComponentData(buildingEntity, new RecruitmentProgressComponent
            {
                UnitType = recruitmentEntityUnit,
                Value = progress
            });

            SetBuildingUpdateUI(buildingEntity);
        }

        private void RemoveEndedRecruitmentUnits()
        {
            foreach (RecruitmentEntity recruitmentEntity in _endRecruitmentUnits)
            {
                _recruitmentList.Remove(recruitmentEntity);
            }

            _endRecruitmentUnits.Clear();
        }

        private void CheckRecruitmentActions()
        {
            foreach ((SetPlayerUIActionComponent actionComponent, Entity entity) in SystemAPI.Query<SetPlayerUIActionComponent>().WithEntityAccess())
            {
                if (actionComponent.Action != PlayerUIActionType.Recruit)
                {
                    continue;
                }

                UpdatePolicy(entity);
                StartRecruitment(actionComponent);
                _entityCommandBuffer.RemoveComponent<SetPlayerUIActionComponent>(entity);
            }
        }

        private void StartRecruitment(SetPlayerUIActionComponent actionComponent)
        {
            UnitType unitType = (UnitType) actionComponent.PayloadID;
            if (!IsRecruitmentAvailable(unitType))
            {
                return;
            }

            SetUpdatedCosts(unitType);
            RecruitUnit(unitType);
        }

        private void SetUpdatedCosts(UnitType unitType)
        {
            foreach (ResourceCostEntity cost in _unitsConfiguration[unitType].RecruitmentCost)
            {
                switch (cost.ResourceType)
                {
                    case ResourceType.Wood: _pendingWoodCost += cost.Cost; break;
                    case ResourceType.Food: _pendingFoodCost += cost.Cost; break;
                    case ResourceType.Population: _pendingPopulationCost += cost.Cost; break;
                }
            }
        }

        private bool IsRecruitmentAvailable(UnitType unitType)
        {
            return _unitsConfiguration[unitType].RecruitmentCost.All(IsCostAffordable);
        }

        private bool IsCostAffordable(ResourceCostEntity costEntity)
        {
            return _elementResourceCostPolicy.Get(costEntity);
        }

        private void RecruitUnit(UnitType unitType)
        {
            foreach ((BuildingTypeComponent buildingTypeComponent, ElementSelectionComponent selectionComponent, Entity entity) in
                     SystemAPI.Query<BuildingTypeComponent, ElementSelectionComponent>().WithEntityAccess())
            {
                if (!selectionComponent.IsSelected)
                {
                    continue;
                }
                
                _buildingActionsFactory.Set(buildingTypeComponent.Type);

                if(_buildingActionsFactory.GetPayload(PlayerUIActionType.Build).Contains((int)unitType))
                {
                    RecruitUnitAtBuilding(unitType, entity);
                    return;
                }
            }
        }

        private void RecruitUnitAtBuilding(UnitType unitType, Entity entity)
        {
            SendQueueCommand(unitType);

            float recruitmentTime = _unitsConfiguration[unitType].RecruitmentTime;
            RecruitmentEntity recruitmentEntity = new RecruitmentEntity(recruitmentTime, entity, unitType);
            recruitmentEntity.OnFinishedAction += OnUnitRecruitmentFinished;
            SetBuildingList(recruitmentEntity);
            SetBuildingBuffer(unitType, entity);
        }

        private void SetBuildingBuffer(UnitType unitType, Entity entity)
        {
            DynamicBuffer<RecruitmentQueueBufferComponent> recruitmentBuffer = GetRecruitmentBuffer(entity);
            recruitmentBuffer.Add(new RecruitmentQueueBufferComponent
            {
                unitType = unitType
            });

            SetBuildingUpdateUI(entity);
        }

        private void SetBuildingList(RecruitmentEntity recruitmentEntity)
        {
            if (_recruitmentList.Any(recruit => recruit.IsSameEntity(recruitmentEntity.Entity)))
            {
                _recuritmentQueue.Add(recruitmentEntity);
            }
            else
            { 
                _recruitmentList.Add(recruitmentEntity);
            }
        }

        private void OnUnitRecruitmentFinished(Entity building, UnitType unit, RecruitmentEntity recruitmentEntity)
        {
            SetRecruitmentEntityEnd(recruitmentEntity);
            LocalTransform buildingTransform = EntityManager.GetComponentData<LocalTransform>(building);
            SpawnUnitCommand buildingCommand = GetSpawnUnitCommand(buildingTransform.Position, unit);
            Entity entity = SystemAPI.GetSingletonEntity<PlayerTagComponent>();
            DynamicBuffer<SpawnUnitCommand> spawnUnitCommands = SystemAPI.GetBuffer<SpawnUnitCommand>(entity);
            spawnUnitCommands.AddCommandData(buildingCommand);
        }

        private void CheckRecruitmentQueue()
        {
            foreach (RecruitmentEntity endRecruitmentUnit in _endRecruitmentUnits)
            {
                CheckQueue(endRecruitmentUnit);
            }
        }
        private void CheckQueue(RecruitmentEntity doneEntity)
        {
            RecruitmentEntity queuedEntity = _recuritmentQueue.FirstOrDefault(recruit => recruit.IsSameEntity(doneEntity.Entity));

            if (queuedEntity == null)
            {
                return;
            }

            _recuritmentQueue.Remove(queuedEntity);
            _recruitmentList.Add(queuedEntity);
        }

        private void SetRecruitmentEntityEnd(RecruitmentEntity recruitmentEntity)
        {
            recruitmentEntity.OnFinishedAction -= OnUnitRecruitmentFinished;
            _endRecruitmentUnits.Add(recruitmentEntity);
            ClearRecruitmentBuffer(recruitmentEntity);
        }

        private void ClearRecruitmentBuffer(RecruitmentEntity recruitmentEntity)
        {
            Entity buildingEntity = recruitmentEntity.Entity;
            DynamicBuffer<RecruitmentQueueBufferComponent> recruitmentBuffer = GetRecruitmentBuffer(buildingEntity);
            recruitmentBuffer.RemoveAt(0);
            SetBuildingUpdateUI(buildingEntity);
        }

        private DynamicBuffer<RecruitmentQueueBufferComponent> GetRecruitmentBuffer(Entity entity)
        {
            return EntityManager.GetBuffer<RecruitmentQueueBufferComponent>(entity);
        }

        private SpawnUnitCommand GetSpawnUnitCommand(float3 buildingPosition, UnitType unit)
        {
            NetworkTick tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

            return new SpawnUnitCommand
            {
                Tick = tick,
                UnitType = unit,
                BuildingPosition = buildingPosition,
                CommandId = GetCommandId(buildingPosition, unit, tick)
            };
        }

        private static int GetCommandId(float3 buildingPosition, UnitType unit, NetworkTick tick)
        {
            int positionHash = (int)(buildingPosition.x * 1000 + buildingPosition.z * 100);
            int commandId = (int)tick.TickIndexForValidTick * 10000 + (int)unit * 100 + (positionHash % 100);
            return commandId;
        }

        private void SendQueueCommand(UnitType unitType)
        {
            Entity playerEntity = SystemAPI.GetSingletonEntity<PlayerTagComponent>();
            DynamicBuffer<QueueUnitCommand> queueCommands = SystemAPI.GetBuffer<QueueUnitCommand>(playerEntity);
            NetworkTick tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;

            queueCommands.AddCommandData(new QueueUnitCommand
            {
                Tick = tick,
                UnitType = unitType,
                CommandId = GetCommandId(unitType, tick)
            });
        }

        private static int GetCommandId(UnitType unitType, NetworkTick tick)
        {
            return (int)tick.TickIndexForValidTick * 1000 + (int)unitType;
        }

        public void SetBuildingUpdateUI(Entity entity)
        {
            ElementSelectionComponent elementSelectionComponent = EntityManager.GetComponentData<ElementSelectionComponent>(entity);
            elementSelectionComponent.MustUpdateGroup = true;
            EntityManager.SetComponentData(entity, elementSelectionComponent);
        }
    }
}
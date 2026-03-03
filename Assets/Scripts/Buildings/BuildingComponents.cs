using ScriptableObjects;
using Types;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Buildings
{
    public struct BuildingComponents : IComponentData
    {
    }

    public struct NewBuildingTagComponent : IComponentData
    {
    }

    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BuildingObstacleSizeComponent : IComponentData
    {
        [GhostField]
        public float3 Size;
    }
    
    public struct BuildingTypeComponent : IComponentData
    {
        public BuildingType Type;
    }

    public struct PlaceBuildingCommand : ICommandData
    {
        public BuildingType BuildingType;

        public float3 Position;

        public NetworkTick Tick { get; set; }
    }
    
    public struct SpawnUnitCommand : ICommandData
    {
        public UnitType UnitType;

        public float3 BuildingPosition;

        public int CommandId;

        public NetworkTick Tick { get; set; }
    }

    public struct QueueUnitCommand : ICommandData
    {
        public UnitType UnitType;

        public int CommandId;

        public NetworkTick Tick { get; set; }
    }


    public struct BuildingPrefabComponent : IComponentData
    {
        public Entity TownCenter;

        public Entity Barracks;

        public Entity House;

        public Entity Farm;
        
        public Entity Tower;
    }
    
    public class BuildingsConfigurationComponent : IComponentData
    {
        public BuildingsScriptableObject Configuration;
    }
    
    public struct BuildingPivotReferencesComponent : IComponentData
    {
        public Entity PivotEntity;

        public Entity ConstructionSiteEntity;
    }

    public class BuildingMaterialsConfigurationComponent : IComponentData
    {
        public BuildingMaterialsConfiguration Configuration;
    }
    
    public struct RecruitmentQueueBufferComponent : IBufferElementData
    {
        public UnitType unitType;
    }
    
    public struct RecruitmentProgressComponent : IComponentData
    {
        public UnitType UnitType;

        public float Value;
    }
    
    public struct BuildingConstructionProgressComponent : IComponentData
    {
        [GhostField]
        public float ConstructionTime;

        [GhostField] 
        public float Value;
    }

    public struct DelayWorkerToBuildingCommandComponent : IComponentData
    {
        public Entity WorkerEntity;
        public Entity BuildingEntity;
        public float3 TargetPosition;
        public TeamType PlayerTeam;
        public int FramesToWait;
    }
}
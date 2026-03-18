using System;
using System.Collections.Generic;
using Buildings;
using GatherableResources;
using Types;
using UnityEngine;

namespace ScriptableObjects
{
    [Serializable]
    public class BuildingScriptableObject
    {
        [SerializeField]
        private BuildingType _buildingType;
        
        [SerializeField]
        private string _name;

        [SerializeField]
        private string _description;

        [SerializeField]
        private Sprite _sprite;
        
        [SerializeField]
        private List<ResourceCostEntity> _constructionCost;
        
        [SerializeField]
        private GameObject _buildingPrefab;
        
        [SerializeField]
        private BuildingView _buildingTemplate;

        public BuildingType BuildingType => _buildingType;

        public GameObject BuildingPrefab => _buildingPrefab;

        public string Name => _name;

        public string Description => _description;

        public Sprite Sprite => _sprite;
        
        public List<ResourceCostEntity> ConstructionCost => _constructionCost;

        public BuildingView BuildingTemplate => _buildingTemplate;

        public ActionPopUpPayload GetActionPopUpPayload()
        {
            return new ActionPopUpPayload(_name, _description, _constructionCost);
        }
    }
}
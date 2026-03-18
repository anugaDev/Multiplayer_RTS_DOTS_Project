using System;
using System.Collections.Generic;
using GatherableResources;
using Types;
using UnityEngine;
using UnityEngine.UI;

namespace ScriptableObjects
{
    [Serializable]
    public class UnitScriptableObject
    {
        [SerializeField]
        private UnitType _unitType;
        
        [SerializeField]
        private GameObject _unitPrefab;

        [SerializeField]
        private string _name;
        
        [SerializeField]
        private Sprite _sprite;

        [SerializeField]
        private string _description;

        [SerializeField] 
        private float _recruitmentTime;
        
        [SerializeField]
        private List<ResourceCostEntity> _recruitmentCost;

        public UnitType UnitType => _unitType;

        public GameObject UnitPrefab => _unitPrefab;

        public string Name => _name;
        
        public Sprite Sprite => _sprite;

        public string Description => _description;

        public float RecruitmentTime => _recruitmentTime;

        public List<ResourceCostEntity> RecruitmentCost => _recruitmentCost;

        public ActionPopUpPayload GetActionPopUpPayload()
        {
            return new ActionPopUpPayload(_name, _description, _recruitmentCost);
        }
    }
}
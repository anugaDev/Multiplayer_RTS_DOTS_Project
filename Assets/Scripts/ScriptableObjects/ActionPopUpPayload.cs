using System.Collections.Generic;
using GatherableResources;

namespace ScriptableObjects
{
    public class ActionPopUpPayload
    {
        private string _name;

        private List<ResourceCostEntity> _resourceCost;

        private float _timeRequired;
        
        public List<ResourceCostEntity> ResourceCost => _resourceCost;
        
        public string Name => _name;

        public ActionPopUpPayload(string name, List<ResourceCostEntity> resourceCost)
        {
            _name = name;
            _resourceCost = resourceCost;
        }
    }
}
using System.Collections.Generic;
using GatherableResources;

namespace ScriptableObjects
{
    public class ActionPopUpPayload
    {
        private string _name;

        private List<ResourceCostEntity> _resourceCost;

        private string _description;

        public List<ResourceCostEntity> ResourceCost => _resourceCost;
        
        public string Name => _name;
        
        public string Description => _description;

        public ActionPopUpPayload(string name, string description, List<ResourceCostEntity> resourceCost)
        {
            _name = name;
            _description = description;
            _resourceCost = resourceCost;
        }
    }
}
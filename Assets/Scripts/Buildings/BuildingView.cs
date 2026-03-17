using System.Collections.Generic;
using UnityEngine;

namespace Buildings
{
    public class BuildingView : MonoBehaviour
    {
        [SerializeField] 
        private List<Renderer> _buildingRenderers;

        [SerializeField]
        private Transform _transform;
        
        [SerializeField]
        private GameObject _gameObject;

        public GameObject GameObject=> _gameObject;

        public void SetTeamColorMaterial(Material material)
        {
            foreach (Renderer renderer in _buildingRenderers)
            {
                renderer.material = material;
            }
        }
    }
}
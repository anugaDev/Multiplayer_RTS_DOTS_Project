using UI.UIControllers;
using Unity.Entities;
using UnityEngine;

namespace UI
{
    public class UIPrefabs : IComponentData
    {
        public UnitUIController UnitUI;

        public GameObject ResourceUI;
    }

    public class UISceneReferenceComponent : IComponentData
    {
        public UserInterfaceController UIReference;
    }
}
using System;
using UnityEngine;

namespace Audio
{
    [Serializable]
    public struct AudioEntry
    {
        public AudioSourceType Id;

        public AudioClip Clip;
    }
}

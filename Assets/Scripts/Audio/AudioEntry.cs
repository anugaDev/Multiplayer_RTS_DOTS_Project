using System;
using UnityEngine;

namespace Audio
{
    [Serializable]
    public struct AudioEntry
    {
        public AudioSourceEnum Id;
        public AudioClip Clip;
    }
}

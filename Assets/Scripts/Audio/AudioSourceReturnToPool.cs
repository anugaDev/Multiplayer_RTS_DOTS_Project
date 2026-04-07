using System.Collections.Generic;
using UnityEngine;

namespace Audio
{
    public class AudioSourceReturnToPool : MonoBehaviour
    {
        public Queue<AudioSource> Pool;
        public AudioSource Source;

        private void Update()
        {
            if (Source != null && !Source.isPlaying)
            {
                gameObject.SetActive(false);
                if (!Pool.Contains(Source))
                {
                    Pool.Enqueue(Source);
                }
            }
        }
    }
}

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Audio
{
    public class AudioManager : MonoBehaviour
    {
        [SerializeField]
        private List<AudioEntry> AudioClips;
        
        [SerializeField]
        private  int PoolSize = 10;
        
        [SerializeField]
        [Range(0f, 1f)]
        private float MasterVolume = 1f;

        private Dictionary<AudioSourceType, AudioClip> _clipCache;
        
        private Queue<AudioSource> _audioSourcePool;

        private EntityManager _entityManager;
        
        private Entity _managerEntity;

        private void Awake()
        {
            _clipCache = new Dictionary<AudioSourceType, AudioClip>();
            if (AudioClips != null)
            {
                foreach (AudioEntry entry in AudioClips)
                {
                    if (entry.Clip != null)
                    {
                        _clipCache[entry.Id] = entry.Clip;
                    }
                }
            }

            _audioSourcePool = new Queue<AudioSource>();
            for (int i = 0; i < PoolSize; i++)
            {
                CreatePoolItem();
            }

            if (World.DefaultGameObjectInjectionWorld != null)
            {
                RegisterEntity(World.DefaultGameObjectInjectionWorld.EntityManager);
            }
        }

        private void Start()
        {
            if (_managerEntity == Entity.Null && World.DefaultGameObjectInjectionWorld != null)
            {
                 RegisterEntity(World.DefaultGameObjectInjectionWorld.EntityManager);
            }
        }

        private void RegisterEntity(EntityManager em)
        {
            _entityManager = em;
            _managerEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentObject(_managerEntity, new AudioManagerReferenceComponent { ManagerReference = this });
        }

        private void OnDestroy()
        {
            if (World.DefaultGameObjectInjectionWorld != null && _entityManager.Exists(_managerEntity))
            {
                _entityManager.DestroyEntity(_managerEntity);
            }
        }

        private void CreatePoolItem()
        {
            GameObject go = new GameObject("AudioSourceObject");
            go.transform.SetParent(transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            
            AudioSourceReturnToPool returnToPool = go.AddComponent<AudioSourceReturnToPool>();
            returnToPool.Pool = _audioSourcePool;
            returnToPool.Source = source;

            go.SetActive(false);
            _audioSourcePool.Enqueue(source);
        }

        public void PlaySound(AudioSourceType id, Unity.Mathematics.float3 position, bool is3D)
        {
            if (!_clipCache.TryGetValue(id, out AudioClip clip))
            {
                return;
            }

            if (_audioSourcePool.Count == 0)
            {
                CreatePoolItem();
            }

            AudioSource source = _audioSourcePool.Dequeue();
            source.gameObject.SetActive(true);
            source.clip = clip;
            source.volume = MasterVolume;
            
            if (is3D)
            {
                source.spatialBlend = 1f;
                source.transform.position = position;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            source.Play();
        }
    }
}

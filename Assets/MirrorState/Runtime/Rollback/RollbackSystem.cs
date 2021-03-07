using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MirrorState.Scripts.Generation;
using Mirror;
using UnityEngine;

namespace MirrorState.Scripts.Rollback
{
    [DefaultExecutionOrder(-10)]
    public class RollbackSystem : MonoBehaviour
    {
        public static RollbackSystem Instance { get; private set; }
        private readonly Dictionary<GameObject, ITrackedEntity> _trackedEntities = new Dictionary<GameObject, ITrackedEntity>();

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void Register(ITrackedEntity entity)
        {
            _trackedEntities.Add(entity.gameObject, entity);
        }

        public void Unregister(ITrackedEntity entity)
        {
            _trackedEntities.Remove(entity.gameObject);
        }

        public IEnumerable TrackedEntities(GameObject[] gameObjects)
        {
            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (!_trackedEntities.ContainsKey(gameObjects[i]))
                {
                    Debug.LogWarning("TrackedEntities: Game Object " + gameObjects[i].name + " was not registered with Tracked Entities.");
                    continue;
                }

                yield return _trackedEntities[gameObjects[i]];
            }
        }

        public ITrackedEntity[] ToTrackedEntities(GameObject[] gameObjects)
        {
            List<ITrackedEntity> entities = new List<ITrackedEntity>();
            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (!_trackedEntities.ContainsKey(gameObjects[i]))
                {
                    Debug.LogWarning("ToTrackedEntities: Game Object " + gameObjects[i].name + " was not registered with Tracked Entities.");
                    continue;
                }

                entities.Add(_trackedEntities[gameObjects[i]]);
            }

            // Not ideal but I'm lazy.
            return entities.ToArray();
        }

        public ITrackedEntity[] ToTrackedEntities(int size, RaycastHit[] hits)
        {
            var entities = new List<ITrackedEntity>();
            for (int i = 0; i < size; i++)
            {
                GameObject obj = hits[i].collider.gameObject.transform.root.gameObject;

                if (!_trackedEntities.ContainsKey(obj))
                {
                    Debug.LogWarning("ToTrackedEntities: Game Object " + obj + " was not registered with Tracked Entities.");
                    continue;
                }

                entities.Add(_trackedEntities[obj]);
            }

            // Not ideal but I'm lazy.
            return entities.ToArray();
        }

        public void Rollback(uint tick, ITrackedEntity[] entities)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i].Rollback(tick);
            }
        }

        public void Restore(ITrackedEntity[] entities)
        {
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i].Restore();
            }
        }

        public void RollbackAndRestore(uint tick, ITrackedEntity[] entities, Action action)
        {
            Rollback(tick, entities);

            action();

            Restore(entities);
        }
    }
}

using System;
using Mirror;
using UnityEngine;

namespace MirrorState.Scripts.Demo
{
    [Serializable]
    public class GhostSpawnResults
    {
        public Quaternion TurretRotation;
        public uint Tick;
        public Vector3 HitPosition;

        public string ToString(bool client, Transform transform)
        {
            // Extra space in "Client " intentional
            var str = client ? "Client " : "Server";

            return str + " Cube Spawned at " + Tick + ", " + transform.position.ToString("F4") +
                   " and was hit at " + HitPosition.ToString("F4") +
                   ", rotation " + TurretRotation.ToString("F4");
        }
    }

    public class GhostSpawner : NetworkBehaviour
    {
        public GameObject Ghost;
        public GameObject GhostClient;

        public void SpawnGhost(GhostSpawnResults results)
        {
            if (NetworkServer.active)
            {
                var instance = Instantiate(Ghost, transform.position, transform.rotation);
                var cube = instance.GetComponent<GhostCube>();
                cube.Results = results;
                NetworkServer.Spawn(instance);
            }
            else
            {
                var instance = Instantiate(GhostClient, transform.position, transform.rotation);
                Debug.Log(results.ToString(true, transform));
            }
        }
    }
}

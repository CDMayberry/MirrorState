using System.Collections;
using System.Collections.Generic;
using Mirror;
using MirrorState.Scripts.Demo;
using UnityEngine;

public class GhostCube : NetworkBehaviour
{
    [SyncVar] public GhostSpawnResults Results;

    public override void OnStartClient()
    {
        Debug.Log(Results.ToString(false, transform));
        Debug.Log("-------");
    }
}

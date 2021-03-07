using UnityEngine;
using System.Collections;
using Mirror;

public class ServerDestroyAfter : NetworkBehaviour
{
    public float destroyAfter = 5;

    void OnEnable()
    {
        if (NetworkServer.active)
        {
            Invoke(nameof(DestroySelf), destroyAfter);
        }
    }

    void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyAfter : MonoBehaviour
{
    public float destroyAfter = 5;


    void OnEnable()
    {
        Invoke(nameof(DestroySelf), destroyAfter);
    }

    void DestroySelf()
    {
        GameObject.Destroy(gameObject);
    }
}

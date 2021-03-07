using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class MoveBetween : MonoBehaviour
{
    public float Speed = 1f;

    public bool GoTo1 = false;
    public GameObject Position1;
    public GameObject Position2;

    private GameObject Target;

    private void Awake()
    {
        if (Position1 == null || Position2 == null)
        {
            Debug.LogError("No Objects to follow.");
            Destroy(this);
            return;
        }

        Target = Position1;
    }

    private void FixedUpdate()
    {
        if (!NetworkServer.active)
        {
            return;
        }

        if (Vector3.Distance(transform.position, Target.transform.position) < 1f)
        {
            Target = GoTo1 ? Position1 : Position2;
            GoTo1 = !GoTo1;
        }

        float step = Speed * Time.fixedDeltaTime; // calculate distance to move
        transform.position = Vector3.MoveTowards(transform.position, Target.transform.position, step);
    }
}

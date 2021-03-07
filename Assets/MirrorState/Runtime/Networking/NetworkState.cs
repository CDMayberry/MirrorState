using Mirror;
using UnityEngine;

// Push it after default behaviours
[DefaultExecutionOrder(10)]
public class NetworkState : NetworkBehaviour
{
    public struct TransformState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public double Timestamp;
        public TransformState(Vector3 pos, Quaternion rot, double time)
        {
            this.Position = pos;
            this.Rotation = rot;
            this.Timestamp = time;
        }
    }

    public readonly TransformState[] StateBuffer = new TransformState[30];
    int stateCount = 0;
    [Range(0, 100)]
    public float UpdateRate = 20f;

    public float InterpolationBackTime = .1f;
    public float RotationSmooth = 15f;

    Rigidbody rb;
    float updateTimer;
    
    [SyncVar]
    Vector3 LastPosition = Vector3.zero;
    [SyncVar]
    Quaternion LastRotation = Quaternion.identity;

    void Awake()
    {
        rb = this.GetComponent<Rigidbody>();
    }

    public override void OnStartClient()
    {
        if (rb != null) { rb.isKinematic = true; }
        transform.position = LastPosition;
        transform.rotation = LastRotation;
    }

    private void BufferState(TransformState state)
    {
        // shift buffer contents to accommodate new state
        for (int i = StateBuffer.Length - 1; i < StateBuffer.Length && i > 0; --i)
        {
            StateBuffer[i] = StateBuffer[i - 1];
        }
        // save state to slot 0
        StateBuffer[0] = state;
        // increment state count
        stateCount = Mathf.Min(stateCount + 1, StateBuffer.Length);
    }

    private void Update()
    {
        if (NetworkServer.active)
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= 1 / UpdateRate)
            {
                updateTimer = 0f;
                var newState = new TransformState(transform.position, transform.rotation, NetworkTime.time);

                LastPosition = newState.Position;
                LastRotation = newState.Rotation;
                RpcServerState(newState);
                //BufferState(newState); // I don't think this is needed if it's on the server? interpolation is used by clients without authority over objects.
            }
            return;
        }

        if (hasAuthority || stateCount == 0)
        {
            return;
        }

        double currentTime = NetworkTime.time;
        double interpolationTime = currentTime - InterpolationBackTime;

        // the latest packet is newer than interpolation time - we have enough packets to interpolate
        if (StateBuffer[0].Timestamp > interpolationTime)
        {
            for (int i = 0; i < stateCount; i++)
            {
                // find the closest state that matches network time, or use oldest state
                if (StateBuffer[i].Timestamp <= interpolationTime || i == stateCount - 1)
                {
                    // the state closest to network time
                    TransformState lhs = StateBuffer[i];
                    // the state one slot newer
                    TransformState rhs = StateBuffer[Mathf.Max(i - 1, 0)];
                    // use time between lhs and rhs to interpolate
                    double length = rhs.Timestamp - lhs.Timestamp;

                    float t = 0f;
                    if (length > 0.0001)
                    {
                        t = (float)((interpolationTime - lhs.Timestamp) / length);
                    }
                    transform.position = Vector3.Lerp(lhs.Position, rhs.Position, t);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rhs.Rotation, Time.deltaTime * RotationSmooth);
                    break;
                }
            }
        }
    }


    // TODO: Add to Reflyn
    /*public override void AfterDeserialize()
    {
        if (hasAuthority)
        {
            return;
        }

        Debug.Log("Deserialized at: " + LastTimestamp + ", Last Buffer: " + this.StateBuffer[0].Timestamp);
    }*/

    [ClientRpc]
    void RpcServerState(TransformState newState)
    {
        if (hasAuthority)
        {
            return;
        }

        BufferState(newState);
    }
}

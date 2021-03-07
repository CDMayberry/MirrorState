using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mirror;

public class NetworkController : NetworkBehaviour
{
    private struct MoveCommand
    {
        public float HorizontalAxis;
        public float VerticalAxis;
        public double Timestamp;
        public Vector3 pos;

        public MoveCommand(float horiz, float vert, double timestamp, Vector3 Pos)
        {
            this.HorizontalAxis = horiz;
            this.VerticalAxis = vert;
            this.Timestamp = timestamp;
            this.pos = Pos;
        }
    }

    public float MoveSpeed = 5f;
    public float MaxDistanceBetweenClientAndServerSide = 0.1f;

    [SerializeField]
    private float horizAxis = 0f;
    [SerializeField]
    private float vertAxis = 0f;

    // a history of move states sent from client to server
    List<MoveCommand> moveHistory = new List<MoveCommand>();
    Queue<MoveCommand> moves = new Queue<MoveCommand>();

    Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (hasAuthority)
        {
            horizAxis = Input.GetAxis("Horizontal");
            vertAxis = Input.GetAxis("Vertical");
        }
    }

    void FixedUpdate()
    {
        if (hasAuthority)
        {
            // get current move state
            MoveCommand moveState = new MoveCommand(horizAxis, vertAxis, NetworkTime.time, transform.position);

            // buffer move state
            moveHistory.Insert(0, moveState);

            // cap history at 200
            if (moveHistory.Count > 200)
            {
                moveHistory.RemoveAt(moveHistory.Count - 1);
            }

            // simulate
            Simulate();

            // send state to server
            CmdProcessInput(moveState);
        }
        else if (NetworkServer.active)
        {
            if (moves.Count != 0)
            {
                ValidateInput(moves.Dequeue());
            }
        }
    }

    [Command]
    void CmdProcessInput(MoveCommand move)
    {
        if (hasAuthority || !NetworkServer.active)
            return;

        moves.Enqueue(move);
    }

    [Server]
    void ValidateInput(MoveCommand move)
    {
        // execute input
        horizAxis = move.HorizontalAxis;
        vertAxis = move.VerticalAxis;

        Simulate();

        // compare results
        if (Vector3.Distance(transform.position, move.pos) > MaxDistanceBetweenClientAndServerSide)
        {
            // error is too big, tell client to rewind and replay
            TargetCorrectState(transform.position, NetworkTime.time);
        }
    }

    [TargetRpc]
    void TargetCorrectState(Vector3 correctPosition, double timestamp)
    {
        // find past state based on timestamp
        int pastState = 0;
        for (int i = 0; i < moveHistory.Count; i++)
        {
            if (moveHistory[i].Timestamp <= timestamp)
            {
                pastState = i;
                break;
            }
        }

        // rewind
        transform.position = correctPosition;

        // replay
        for (int i = pastState; i >= 0; i--)
        {
            horizAxis = moveHistory[i].HorizontalAxis;
            vertAxis = moveHistory[i].VerticalAxis;
            Simulate();
        }
        // clear
        moveHistory.Clear();
    }

    public void Simulate()
    {
        var forward = transform.forward * vertAxis * MoveSpeed * Time.fixedDeltaTime;
        var right = transform.right * horizAxis * MoveSpeed * Time.fixedDeltaTime;

        _rb.MovePosition(_rb.position + forward + right);
        //transform.Translate(new Vector3(horizAxis, 0, vertAxis) * MoveSpeed * Time.fixedDeltaTime);
    }
}

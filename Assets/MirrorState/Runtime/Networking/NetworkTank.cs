/*
using System;
using System.Collections;
using System.Collections.Generic;
using Mayberry.Scripts.Generated.States;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace Mayberry.Scripts.Networking
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class NetworkTank : NetworkBehaviour
    {
        private void OnValidate()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
        }

        [Flags]
        private enum KeyCommand
        {
            None = 0,
            Shooting = 1 << 0,
        }

        private struct Command
        {
            public float HorizontalAxis;
            public float VerticalAxis;
            public double Timestamp;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Shoot;
            public bool IsFirstUsage;

            public Command(float horiz, float vert, bool shoot, double timestamp, Vector3 position, Quaternion rotation)
            {
                this.HorizontalAxis = horiz;
                this.VerticalAxis = vert;
                this.Timestamp = timestamp;
                this.Position = position;
                this.Rotation = rotation;
                this.Shoot = shoot;
                this.IsFirstUsage = true;
            }

            /*public Command Extend(Command extendWith)
        {
            // Copy
            var cmd = this;
            cmd.HorizontalAxis = extendWith.HorizontalAxis;
            cmd.VerticalAxis = extendWith.VerticalAxis;
            cmd.Timestamp = extendWith.Timestamp;
            cmd.Position = extendWith.Position;
            cmd.Rotation = extendWith.Rotation;

            cmd.Shoot = cmd.Shoot || extendWith.Shoot;

            return cmd;
        }

        public Command Update(Command prevCommand)
        {
            this.Shoot = this.Shoot || prevCommand.Shoot;
            return this;
        }#1#
        }

        [Header("Components")]
        public NavMeshAgent agent;
        public Animator animator;

        [Header("Movement")]
        public float rotationSpeed = 100;
        public float MaxDistanceBetweenClientAndServerSide = 0.1f;
        public float AngleLimit = 5f; // This was my own guess...

        [Header("Firing")]
        public KeyCode shootKey = KeyCode.Space;
        public GameObject projectilePrefab;
        public Transform projectileMount;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Shoot = Animator.StringToHash("Shoot");

        List<Command> cmdHistory = new List<Command>();
        Queue<Command> commands = new Queue<Command>();

        Rigidbody _rb;
        private TankState _state;
        private Command _latest;
        private KeyCommand _keysSinceLastSend = KeyCommand.None;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _state = GetComponent<TankState>();
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            _latest = GetCommand();
            //StartCoroutine(Test());
        }

        /*IEnumerator Test()
        {
            while (true)
            {

                yield return new WaitForFixedUpdate();
            }
        }#1#

        public override void OnStartAuthority()
        {
            ScoreTracker.Instance.Player = netIdentity.netId;
        }

        private void OnEnable()
        {
            _state.OnFire += Fire;
        }

        private void OnDisable()
        {
            _state.OnFire -= Fire;
        }

        void Update()
        {
            // movement for local player
            if (!hasAuthority)
            {
                return;
            }

            _latest = GetUpdatedCommand(_latest);

        }

        void UpdateKeyCommands()
        {
            _keysSinceLastSend =
                _keysSinceLastSend | (Input.GetKeyDown(shootKey) ? KeyCommand.Shooting : 0);
        }

        Command GetCommand()
        {
            var command = new Command
            {
                HorizontalAxis = Input.GetAxis("Horizontal"),
                VerticalAxis = Input.GetAxis("Vertical"),
                Timestamp = NetworkTime.time,
                Position = transform.position,
                Rotation = transform.rotation,
                Shoot = Input.GetKey(shootKey),
                IsFirstUsage = true
            };

            return command;
        }

        Command GetUpdatedCommand(Command prevCommand)
        {
            prevCommand.Shoot = prevCommand.Shoot || Input.GetKeyDown(shootKey);

            return prevCommand;
        }

        Command GetSendCommand(Command prevCommand)
        {
            var command = new Command
            {
                HorizontalAxis = Input.GetAxis("Horizontal"),
                VerticalAxis = Input.GetAxis("Vertical"),
                Timestamp = NetworkTime.time,
                Position = transform.position,
                Rotation = transform.rotation,
                Shoot = prevCommand.Shoot || Input.GetKeyDown(shootKey),
                IsFirstUsage = true
            };

            return command;
        }

        void FixedUpdate()
        {
            if (hasAuthority)
            {

                // get current move state
                Command next = GetSendCommand(_latest);

                // simulate
                Simulate(next);

                // send state to server
                CmdProcessInput(next);

                next.IsFirstUsage = false;
                // buffer move state
                cmdHistory.Insert(0, next);

                // cap history at 200
                if (cmdHistory.Count > 200)
                {
                    cmdHistory.RemoveAt(cmdHistory.Count - 1);
                }

                _latest = GetCommand();

            }
            else if (NetworkServer.active)
            {
                if (commands.Count != 0)
                {
                    ValidateInput(commands.Dequeue());
                }
            }
        }

        [Command]
        void CmdProcessInput(Command move)
        {
            if (hasAuthority || !NetworkServer.active)
            {
                return;
            }

            commands.Enqueue(move);
        }

        [Server]
        void ValidateInput(Command move)
        {
            Simulate(move);
            float angleDiff = Quaternion.Angle(transform.rotation, move.Rotation);

            // compare results
            if (Vector3.Distance(transform.position, move.Position) > MaxDistanceBetweenClientAndServerSide || angleDiff > AngleLimit)
            {
                //Debug.Log("State Correction on " + gameObject.name);

                // error is too big, tell client to rewind and replay
                TargetCorrectState(transform.position, transform.rotation, NetworkTime.time);
            }
        }

        [TargetRpc]
        void TargetCorrectState(Vector3 correctPosition, Quaternion correctRotation, double timestamp)
        {
            // find past state based on timestamp
            int pastState = 0;
            for (int i = 0; i < cmdHistory.Count; i++)
            {
                if (cmdHistory[i].Timestamp <= timestamp)
                {
                    pastState = i;
                    break;
                }
            }

            // TODO: Instead of resetting here, call Simulate(command, true) to reset to that point, *then* replay the commands from there.
            // rewind
            transform.position = correctPosition;
            transform.rotation = correctRotation;

            if (cmdHistory.Count > 0)
            {
                // replay
                for (int i = pastState; i >= 0; i--)
                {
                    Simulate(cmdHistory[i]);
                }

                // clear
                cmdHistory.Clear();
            }
        }

        private void Simulate(Command command)
        {
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 velocity = forward * (Mathf.Max(command.VerticalAxis, 0) * agent.speed * Time.fixedDeltaTime);
        
            agent.Move(velocity);
            transform.Rotate(0, command.HorizontalAxis * rotationSpeed * Time.fixedDeltaTime, 0);
            animator.SetBool(Moving, velocity != Vector3.zero);

            if (command.IsFirstUsage)
            {
                // Shooting logic

                if (command.Shoot)
                {
                    FireWeapon(command);
                }

                command.IsFirstUsage = false;
            }
        }

        private double fireTimestamp = -1000f;
        private double refireRate = .33f;


        void FireWeapon(Command cmd)
        {
            if (fireTimestamp + refireRate <= NetworkTime.time)
            {


                //Debug.Log("Shooting");
                fireTimestamp = NetworkTime.time;
                
                _state.Fire(cmd.);

                // I think this is incorrect, if this is the server and the state is replicated, all child clients will also replicate it.
                //state.Fire();
                /*GameObject projectile = Instantiate(projectilePrefab, projectileMount.position, transform.rotation);
                animator.SetTrigger(Shoot);#1#

                /*if (NetworkServer.active)
                {
                    Debug.Log($"Server Fired At: {projectileMount.position.ToString("F4")}, {transform.rotation.ToString("F4")}, {fireTimestamp}");
                }
                else if(hasAuthority)
                {
                    CmdLogSpawn(projectileMount.position, transform.rotation, fireTimestamp);
                }#1#

                // if we are the owner and the active weapon is a hitscan weapon, do logic
                /*if (entity.IsOwner)
                {
                    activeWeapon.OnOwner(cmd, entity);
                }#1#
            }
        }

        void Fire(uint tick)
        {
            GameObject projectile = Instantiate(projectilePrefab, projectileMount.position, transform.rotation);
            var script = projectile.GetComponent<NonNetworkProjectile>();
            script.player = netIdentity.netId;

            animator.SetTrigger(Shoot);
        }

        // this is called on the server
        [Command]
        void CmdLogSpawn(Vector3 position, Quaternion rotation, double timestamp)
        {
            Debug.Log($"Client Fired At: {position.ToString("F4")}, {rotation.ToString("F4")}, {timestamp}");
        }
    }
}
*/

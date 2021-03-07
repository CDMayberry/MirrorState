using Mirror;
using MirrorState.Scripts.Generated.Commands;
using UnityEngine;
using UnityEngine.AI;
//using Shapes;

namespace MirrorState.Scripts.Demo
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class TankController : TankControllerBase
    {
        private void OnValidate()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
        }
    
        [Header("Components")]
        public NavMeshAgent agent;
        public Animator animator;
        private Rigidbody _rb;
        [SerializeField]
        private Transform _turret;

        [Header("Movement")]
        public float rotationSpeed = 100;
        public float MaxDistanceBetweenClientAndServerSide = 0.1f;
        //private float angleLimit = 2f;

        [Header("Firing")]
        public KeyCode shootKey = KeyCode.Space;
        public GameObject projectilePrefab;
        public GameObject projectileLinePrefab;
        public Transform projectileMount;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Shoot = Animator.StringToHash("Shoot");
        private uint fireTick = 0;
        private uint refireTickRate = 30;

        // TODO: we want server list for tracking position per tick.

        protected override void Awake()
        {
            base.Awake();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            State.Fire += OnFire;
        }

        private void OnDisable()
        {
            State.Fire -= OnFire;
        }

        void Update()
        {
            UpdateFrameInputs();
        }

        // Projectile weapons should use NetworkTime.rtt to 'catch up' to the player client. 
        // Hit scan weapons would need a rollback script
        void OnFire(uint tick)
        {
            //OnFireProjectile(tick);

            OnFireRaycast(tick);
        }

        void OnFireProjectile(uint tick)
        {
            GameObject projectile = Instantiate(projectilePrefab, projectileMount.position, transform.rotation);
            var script = projectile.GetComponent<NonNetworkProjectile>();
            script.owner = gameObject;
            script.player = netIdentity.netId;

            animator.SetTrigger(Shoot);
            if (!hasAuthority)
            {
                // https://www.gamasutra.com/blogs/NeemaTeymory/20160906/280377/Why_Making_Multiplayer_Games_is_Hard_Lag_Compensating_Weapons_in_MechWarrior_Online.php
                // Naive catch up method. Needs to do more complex logic for ballistic based weapons.
                // NOTE: This would likely be moved into the weapon itself rather than here.
                if (NetworkServer.active)
                {
                    script.Move((TickSystem.Instance.Tick - tick) * Time.fixedDeltaTime);
                }

                // A hit scan weapon would use a hitbox rollback, may want to try that at some point as a 'secondary' fire.
            }
        }

        void OnFireRaycast(uint tick)
        {
            // TODO: I don't think the raycast is rolling back if it *starts* within the bounds of a rollback trigger. Need to verify.
            animator.SetTrigger(Shoot);
            MirrorStatePhysics.Raycast(tick, hasAuthority, projectileMount.position, projectileMount.forward, 1 << LayerMask.NameToLayer("Default"), (size, hits) =>
            {
                // TODO: Remove shapes and use unity's bad line renderer...
                /*var color = State.Predicted.Weapon == 0 ? Color.red : Color.green;
                GameObject projectileLine = Instantiate(projectileLinePrefab);
                var lineScript = projectileLine.GetComponent<Line>();
                lineScript.Color = color;
                lineScript.Start = projectileMount.position;*/
                if (size > 0)
                {
                    var results = new GhostSpawnResults
                    {
                        TurretRotation = _turret.localRotation,
                        HitPosition = hits[0].point,
                        Tick = tick
                    };

                    //Debug.Log("Raycast target hit at " + hits[0].point + " at tick: " + tick);
                    if (hits[0].transform.root.CompareTag("Cube"))
                    {
                        hits[0].transform.root.SendMessage("SpawnGhost", results);
                    }

                    //lineScript.End = hits[0].point;
                }
                else
                {
                    Debug.Log("No raycast target hit");
                    //lineScript.End = projectileMount.position + projectileMount.forward * 100;
                }
            });
        }

        void FireWeapon(Command cmd)
        {
            if (fireTick + refireTickRate <= cmd.CmdTick)
            {
                fireTick = cmd.CmdTick;
                
                Debug.Log("Controller firing at " + cmd.Tick + ", with System at " + TickSystem.Instance.Tick + ": " + cmd.Output);
                State.TriggerFire(cmd.Tick);
            }
        }

        const float MouseSensitivity = 2f;
        private float _vertical;
        private float _horizontal;
        private bool _reload;

        private void UpdateFrameInputs()
        {
            _reload = _reload || Input.GetKeyDown(KeyCode.R);
            // lol, we don't even use a mouse in this test...
            /*_vertical += (Input.GetAxisRaw("Mouse X") * MouseSensitivity);
        _vertical %= 360f;

        _horizontal += (-Input.GetAxisRaw("Mouse Y") * MouseSensitivity);
        _horizontal = Mathf.Clamp(_horizontal, -85f, +85f);*/
        }

        public override bool SimulateAuthority(ref Controls controls)
        {
            controls.Horizontal = Input.GetAxis("Horizontal");
            controls.Vertical = Input.GetAxis("Vertical");
            controls.RotateLeft = Input.GetKey(KeyCode.Q);
            controls.RotateRight = Input.GetKey(KeyCode.E);
            //controls.Reload = _reload;
            controls.Fire = Input.GetKey(shootKey);
            _reload = false;

            return true;
        }

        public override Output ExecuteCommand(Command cmd, bool reset)
        {
            //Debug.Log("Executing at " + cmd.CmdTick + " Reset: " + reset);
            if (reset)
            {
                transform.position = cmd.Output.Position;
                transform.rotation = cmd.Output.Rotation;
                _turret.localPosition = cmd.Output.TurretPosition;
                _turret.localRotation = cmd.Output.TurretRotation;
            }
            else
            {
                // TODO: Generate new State from inputs and return that.
                Vector3 forward = transform.TransformDirection(Vector3.forward);
                Vector3 velocity = forward * (Mathf.Max(cmd.Controls.Vertical, 0) * agent.speed * Time.fixedDeltaTime);

                agent.Move(velocity);
                transform.Rotate(0, cmd.Controls.Horizontal * rotationSpeed * Time.fixedDeltaTime, 0);
                animator.SetBool(Moving, velocity != Vector3.zero);

                if (cmd.Controls.RotateLeft)
                {
                    _turret.Rotate(0, .5f * -rotationSpeed * Time.fixedDeltaTime, 0);
                }
                else if (cmd.Controls.RotateRight)
                {
                    _turret.Rotate(0, .5f * rotationSpeed * Time.fixedDeltaTime, 0);
                }

                cmd.Output.Position = transform.position;
                cmd.Output.Rotation = transform.rotation;
                cmd.Output.TurretPosition = _turret.localPosition;
                cmd.Output.TurretRotation = _turret.localRotation;

                if (cmd.FirstExecute)
                {
                    /*if (cmd.Controls.Reload)
                    {
                        State.Predicted.Weapon += 1;
                        State.Predicted.Weapon %= 2;
                    }*/
                    // Shooting logic

                    if (cmd.Controls.Fire)
                    {
                        FireWeapon(cmd);
                    }
                }
            }

            return cmd.Output;
        }

        /*        
        public override string ToString()
        {
            return $"State({{ CmdTick: {CmdTick}, MechPosition: {TankPosition.ToString("F3")}, MechRotation: {TankRotation.ToString("F3")}, TorsoLocalRotation: {TurretLocalRotation.ToString("F3")}, Forward: {Forward}, Weapon: {Weapon}, Health: {DemoHealth} }})";
        }
        */
    }
}

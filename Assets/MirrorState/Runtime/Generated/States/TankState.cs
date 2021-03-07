using System;
using UnityEngine;
using Mayberry.Reflyn;
using Mirror;
using MirrorState.Mirror;
using MirrorState.Scripts;
using MirrorState.Scripts.Rollback;
using MirrorState.Scripts.Generation;
using RailgunNet;
using RailgunNet.Ticks;
using RailgunNet.Ticks.Interfaces;
using RailgunNet.Ticks.Buffers;
using MirrorState.Scripts.Demo;

namespace MirrorState.Scripts.Generated.States
{
    [DefaultExecutionOrder(10)]
    public class TankState : NetworkBehaviour, ITrackedEntity, IUnitDemo
    {
        public delegate void TankStateEvent(uint tick);
        public event TankStateEvent Fire;
        public event MirrorStateEvent DemoFire;
        public event MirrorStateEvent DemoDeath;
        [SerializableAttribute]
        public struct State : ITick
        {
            public uint CmdTick;
            public Vector3 TankPosition;
            public Quaternion TankRotation;
            public Quaternion TurretLocalRotation;
            public ushort Forward;
            public int Weapon;
            public float DemoHealth;
            public float DemoDamage;
            public uint Tick
            {
                get
                {
                    return this.CmdTick;
                }
            }

            public bool IsNew
            {
                get;
                set;
            }
        }

        public struct SyncState
        {
            public float DemoHealth;
        }

        public struct PredictedState
        {
            public ushort Forward;
            public int Weapon;
            public float DemoDamage;
        }

        [SyncVarAttribute]
        public SyncState Sync;
        public PredictedState Predicted;
        DejitterStructBuffer<State> _stateDejitter = new DejitterStructBuffer<State>(50);
        PriorityQueue<uint, TankEventEnum> _priorityQueue = new PriorityQueue<uint, TankEventEnum>();
        Rigidbody _rb;
        [SerializeField]
        Animator _animator;
        [SyncVarAttribute]
        Vector3 TankPosition;
        [SyncVarAttribute]
        Quaternion TankRotation;
        [SyncVarAttribute]
        Quaternion TurretLocalRotation;
        public Transform TurretLocalTransform;
        public float Forward;
        public int Weapon;
        private static readonly int DemoHealth_Anim = Animator.StringToHash("DemoHealth");
        float _timeElapsed;
        float _timeToReachTarget = 0.1F;
        State _from;
        State _to;
        [RangeAttribute(1, 100)]
        public uint TicksPerUpdate = 1;
        uint _nextTickUpdate;
        int _stateCount = 0;
        [SyncVarAttribute]
        Vector3 LastPosition = Vector3.zero;
        [SyncVarAttribute]
        Quaternion LastRotation = Quaternion.identity;
        public float RotationSmooth = 15F;
        State _savedState;
        public uint TrackedTick
        {
            get
            {
                if (this.HasAnyAuthority())
                {
                    return TickSystem.Instance.Tick;
                }

                if (NetworkServer.active)
                {
                    return TickSystem.Instance.Tick - 1;
                }

                return this._to.CmdTick;
            }
        }

        private ushort _forward
        {
            get
            {
                return Mathf.FloatToHalf(this.Forward);
            }

            set
            {
                this.Forward = Mathf.HalfToFloat(value);
            }
        }

        public float DemoHealth
        {
            get
            {
                return this.Sync.DemoHealth;
            }

            set
            {
                this.Sync.DemoHealth = value;
            }
        }

        public float DemoDamage
        {
            get
            {
                return this.Predicted.DemoDamage;
            }

            set
            {
                this.Predicted.DemoDamage = value;
            }
        }

        void TriggerEvent(uint tick, TankEventEnum evnt)
        {
            if (evnt == TankEventEnum.Fire)
            {
                this.Fire?.Invoke(tick);
                return;
            }

            if (evnt == TankEventEnum.DemoFire)
            {
                this.DemoFire?.Invoke(tick);
                return;
            }

            if (evnt == TankEventEnum.DemoDeath)
            {
                this.DemoDeath?.Invoke(tick);
                return;
            }
        }

        [ClientRpcAttribute(excludeOwner = true)]
        void RpcTriggerEventPredicted(uint tick, TankEventEnum evnt)
        {
            if (this.hasAuthority || NetworkServer.active)
            {
                return;
            }

            if (tick <= TickSystem.Instance.delayTick)
            {
                this.TriggerEvent(tick, evnt);
            }
            else
            {
                this._priorityQueue.Enqueue(tick, evnt);
            }
        }

        [ClientRpcAttribute]
        void RpcTriggerEvent(uint tick, TankEventEnum evnt)
        {
            if (NetworkServer.active)
            {
                return;
            }

            if (tick <= TickSystem.Instance.delayTick)
            {
                this.TriggerEvent(tick, evnt);
            }
            else
            {
                this._priorityQueue.Enqueue(tick, evnt);
            }
        }

        public void TriggerFire(uint tick)
        {
            if (this.hasAuthority == false && NetworkServer.active == false)
            {
                return;
            }

            this._priorityQueue.Enqueue(tick, TankEventEnum.Fire);
            if (NetworkServer.active)
            {
                this.RpcTriggerEventPredicted(tick, TankEventEnum.Fire);
            }
        }

        public void TriggerDemoFire(uint tick)
        {
            if (this.hasAuthority == false && NetworkServer.active == false)
            {
                return;
            }

            this._priorityQueue.Enqueue(tick, TankEventEnum.DemoFire);
            if (NetworkServer.active)
            {
                this.RpcTriggerEventPredicted(tick, TankEventEnum.DemoFire);
            }
        }

        [ServerAttribute]
        public void TriggerDemoDeath(uint tick)
        {
            if (NetworkServer.active == false)
            {
                return;
            }

            this._priorityQueue.Enqueue(tick, TankEventEnum.DemoDeath);
            this.RpcTriggerEvent(tick, TankEventEnum.DemoDeath);
        }

        void OnEnable()
        {
            RollbackSystem.Instance.Register(this);
        }

        void OnDisable()
        {
            RollbackSystem.Instance.Unregister(this);
        }

        void Awake()
        {
            this.syncInterval = 0F;
            this._rb = this.GetComponent<Rigidbody>();
            if (TickSystem.Instance == null)
            {
                this.enabled = false;
            }
        }

        public override void OnStartServer()
        {
            this.TankPosition = this.transform.position;
            this.TankRotation = this.transform.rotation;
        }

        public override void OnStartClient()
        {
            if (this._rb != null && this.HasAnyAuthority() == false)
            {
                this._rb.isKinematic = true;
            }

            this.transform.position = this.TankPosition;
            this.transform.rotation = this.TankRotation;
            this.Predicted.Forward = this._forward;
            this.Predicted.Weapon = this.Weapon;
            this.Predicted.DemoDamage = this.DemoDamage;
        }

        void SetTransitionStates(State from, State to)
        {
            this._from = from;
            this._to = to;
            this._timeElapsed = 0F;
            this._timeToReachTarget = (this._to.CmdTick - this._from.CmdTick) * TickSystem.SecsPerTick;
            this.Predicted.Forward = this._to.Forward;
            this.Predicted.Weapon = this._to.Weapon;
            this.Sync.DemoHealth = this._to.DemoHealth;
            this.Predicted.DemoDamage = this._to.DemoDamage;
        }

        void BufferState(State state)
        {
            this._stateDejitter.Store(state);
            if (this.hasAuthority == false && NetworkServer.active == false && state.CmdTick < TickSystem.Instance.delayTick && state.CmdTick > this._to.CmdTick)
            {
                this.SetTransitionStates(this._to, state);
            }
        }

        [ClientRpcAttribute(excludeOwner = true)]
        void RpcServerState(State state)
        {
            if (this.hasAuthority)
            {
                return;
            }

            this.BufferState(state);
        }

        void FixedUpdate()
        {
            uint usedTick = 0;
            if (this.HasAnyAuthority())
            {
                usedTick = TickSystem.Instance.Tick;
            }
            else
            {
                if (NetworkServer.active)
                {
                    usedTick = TickSystem.Instance.Tick - 1;
                }
                else
                {
                    usedTick = TickSystem.Instance.delayTick;
                }
            }

            if (this.isClient)
            {
                this._animator.SetFloat(TankState.DemoHealth_Anim, this.Sync.DemoHealth);
            }

            if ((NetworkServer.active || this.hasAuthority) && TickSystem.Instance.Tick >= this._nextTickUpdate)
            {
                this._nextTickUpdate = TickSystem.Instance.Tick + this.TicksPerUpdate;
                var newState = this.GetNewState(usedTick);
                this.BufferState(newState);
                if (NetworkServer.active)
                {
                    this.RpcServerState(newState);
                }
            }

            while (this._priorityQueue.Any() && this._priorityQueue.Peek().Key <= usedTick)
            {
                var evnt = this._priorityQueue.Dequeue();
                this.TriggerEvent(evnt.Key, evnt.Value);
            }
        }

        State GetNewState(uint tick)
        {
            State newState = new State();
            newState.CmdTick = tick;
            newState.TankPosition = this.transform.position;
            newState.TankRotation = this.transform.rotation;
            newState.TurretLocalRotation = this.TurretLocalTransform.localRotation;
            newState.Forward = this.Predicted.Forward;
            newState.Weapon = this.Predicted.Weapon;
            newState.DemoHealth = this.Sync.DemoHealth;
            newState.DemoDamage = this.Predicted.DemoDamage;
            return newState;
        }

        public State GetAt(uint tick)
        {
            if (this.hasAuthority)
            {
                return this.GetNewState(tick);
            }

            return this._stateDejitter.GetLatestAt(tick);
        }

        void Update()
        {
            if (NetworkServer.active || this.hasAuthority || this._stateCount == 0)
            {
                return;
            }

            if (this._stateDejitter.Latest.CmdTick > TickSystem.Instance.delayTick)
            {
                var latest = this._stateDejitter.GetLatestAt(TickSystem.Instance.delayTick);
                if (latest.Tick != 0)
                {
                    State current;
                    State next;
                    this._stateDejitter.GetFirstAfter(latest.Tick, out current, out next);
                    this.SetTransitionStates(current, next);
                }
            }

            this._timeElapsed = this._timeElapsed + Time.deltaTime;
            float lerpAmt = this._timeElapsed / this._timeToReachTarget;
            this.transform.position = Vector3.Lerp(this._from.TankPosition, this._to.TankPosition, lerpAmt);
            this.transform.rotation = Quaternion.Slerp(this._from.TankRotation, this._to.TankRotation, Time.deltaTime * this.RotationSmooth);
            this.TurretLocalTransform.localRotation = Quaternion.Slerp(this._from.TurretLocalRotation, this._to.TurretLocalRotation, Time.deltaTime * this.RotationSmooth);
        }

        void SetToState(State state)
        {
            this.transform.position = state.TankPosition;
            this.transform.rotation = state.TankRotation;
            this.TurretLocalTransform.localRotation = state.TurretLocalRotation;
        }

        public void Restore()
        {
            this.SetToState(this._savedState);
        }

        public void Rollback(uint tick)
        {
            State state;
            if (tick > TickSystem.Instance.Tick)
            {
                Debug.LogError("Trying to rollback to a future state.");
                return;
            }

            if (this._stateDejitter.TryGet(tick, out state) == false)
            {
                if (tick < this._stateDejitter.Last.CmdTick)
                {
                    state = this._stateDejitter.Last;
                    Debug.LogWarning("Entity exceeded oldest rollback state, default to last");
                }
                else
                {
                    state = this._stateDejitter.GetLatestAt(tick);
                    Debug.LogError("Unable to find appropriate tick for rollback, using nearest State.");
                }
            }

            this._savedState = this._stateDejitter.Latest;
            this.SetToState(state);
        }
    }

    public enum TankEventEnum : byte
    {
        Fire,
        DemoFire,
        DemoDeath
    }
}

using System;
using UnityEngine;
using Mayberry.Reflyn;
using Mirror;
using MirrorState.Mirror;
using MirrorState.Scripts;
using MirrorState.Scripts.Rollback;
using MirrorState.Scripts.Generation;
using RailgunNet.Ticks.Buffers;

namespace MirrorState.Scripts.Generated.States
{
    public static class TickWrapperTrackedCubeExtension
    {
        public static void WriteTickWrapper(this NetworkWriter writer, TickWrapper<TrackedCubeState.State> dateTime)
        {
            writer.WriteUInt32(dateTime.Tick);
            writer.WriteVector3(dateTime.Data.CubePosition);
            writer.WriteQuaternion(dateTime.Data.CubeRotation);
            //writer.WriteVector3(dateTime.Data.CubeScale);
        }

        public static TickWrapper<TrackedCubeState.State> ReadTickWrapper(this NetworkReader reader)
        {
            var wrapper = new TickWrapper<TrackedCubeState.State>();
            wrapper.Tick = reader.ReadUInt32();
            wrapper.Data.CubePosition = reader.ReadVector3();
            wrapper.Data.CubeRotation = reader.ReadQuaternion();
            //wrapper.Data.CubeScale = reader.ReadVector3();
            return wrapper;
        }
    }

    [DefaultExecutionOrder(10)]
    public class TrackedCubeState : NetworkBehaviour, ITrackedEntity
    {
        public delegate void TrackedCubeStateEvent(uint tick);
        [SerializableAttribute]
        public struct State
        {
            public Vector3 CubePosition;
            public Quaternion CubeRotation;
        }

        public struct SyncState
        {
        }

        public struct PredictedState
        {
        }

        [SyncVarAttribute]
        public SyncState Sync;
        public PredictedState Predicted;
        DejitterUnmanagedBuffer<State> _stateDejitter = new DejitterUnmanagedBuffer<State>(50);
        PriorityQueue<uint, TrackedCubeEventEnum> _priorityQueue = new PriorityQueue<uint, TrackedCubeEventEnum>();
        Rigidbody _rb;
        [SerializeField]
        Animator _animator;
        [SyncVarAttribute]
        Vector3 CubePosition;
        [SyncVarAttribute]
        Quaternion CubeRotation;
        float _timeElapsed;
        float _timeToReachTarget = 0.1F;
        TickWrapper<TrackedCubeState.State> _from;
        TickWrapper<TrackedCubeState.State> _to;
        [RangeAttribute(1, 100)]
        public uint TicksPerUpdate = 1;
        uint _nextTickUpdate;
        int _stateCount = 0;
        [SyncVarAttribute]
        Vector3 LastPosition = Vector3.zero;
        [SyncVarAttribute]
        Quaternion LastRotation = Quaternion.identity;
        public float RotationSmooth = 15F;
        TickWrapper<TrackedCubeState.State> _savedState;
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

                return this._to.Tick;
            }
        }

        void TriggerEvent(uint tick, TrackedCubeEventEnum evnt)
        {
        }

        [ClientRpcAttribute(excludeOwner = true)]
        void RpcTriggerEventPredicted(uint tick, TrackedCubeEventEnum evnt)
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
        void RpcTriggerEvent(uint tick, TrackedCubeEventEnum evnt)
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
            this.CubePosition = this.transform.position;
            this.CubeRotation = this.transform.rotation;
        }

        public override void OnStartClient()
        {
            if (this._rb != null && this.HasAnyAuthority() == false)
            {
                this._rb.isKinematic = true;
            }

            this.transform.position = this.CubePosition;
            this.transform.rotation = this.CubeRotation;
        }

        void SetTransitionStates(TickWrapper<TrackedCubeState.State> from, TickWrapper<TrackedCubeState.State> to)
        {
            this._from = from;
            this._to = to;
            this._timeElapsed = 0F;
            this._timeToReachTarget = (this._to.Tick - this._from.Tick) * TickSystem.SecsPerTick;
        }

        void BufferState(TickWrapper<TrackedCubeState.State> state)
        {
            this._stateDejitter.Store(state);
            if (this.hasAuthority == false && NetworkServer.active == false && state.Tick < TickSystem.Instance.delayTick && state.Tick > this._to.Tick)
            {
                this.SetTransitionStates(this._to, state);
            }
        }

        [ClientRpcAttribute(excludeOwner = true)]
        void RpcServerState(TickWrapper<TrackedCubeState.State> state)
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

        TickWrapper<TrackedCubeState.State> GetNewState(uint tick)
        {
            var newState = new TickWrapper<TrackedCubeState.State>();
            newState.Tick = tick;
            newState.Data.CubePosition = this.transform.position;
            newState.Data.CubeRotation = this.transform.rotation;
            return newState;
        }

        public TickWrapper<TrackedCubeState.State> GetAt(uint tick)
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

            if (this._stateDejitter.Latest.Tick > TickSystem.Instance.delayTick)
            {
                var latest = this._stateDejitter.GetLatestAt(TickSystem.Instance.delayTick);
                if (latest.Tick != default)
                {
                    this._stateDejitter.GetFirstAfter(latest.Tick, out var current, out var next);
                    this.SetTransitionStates(current, next);
                }
            }

            this._timeElapsed = this._timeElapsed + Time.deltaTime;
            float lerpAmt = this._timeElapsed / this._timeToReachTarget;
            this.transform.position = Vector3.Lerp(this._from.Data.CubePosition, this._to.Data.CubePosition, lerpAmt);
            this.transform.rotation = Quaternion.Slerp(this._from.Data.CubeRotation, this._to.Data.CubeRotation, Time.deltaTime * this.RotationSmooth);
        }

        void SetToState(State state)
        {
            this.transform.position = state.CubePosition;
            this.transform.rotation = state.CubeRotation;
        }

        public void Restore()
        {
            this.SetToState(this._savedState.Data);
        }

        public void Rollback(uint tick)
        {
            TickWrapper<TrackedCubeState.State> state;
            if (tick > TickSystem.Instance.Tick)
            {
                Debug.LogError("Trying to rollback to a future state.");
                return;
            }

            if (this._stateDejitter.TryGet(tick, out state) == false)
            {
                if (tick < this._stateDejitter.Last.Tick)
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
            this.SetToState(state.Data);
        }
    }

    public enum TrackedCubeEventEnum : byte
    {
    }
}

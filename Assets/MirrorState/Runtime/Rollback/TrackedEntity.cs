using MirrorState.Scripts.Generation;
using Mirror;
using RailgunNet.Ticks.Buffers;
using RailgunNet.Ticks.Interfaces;
using UnityEngine;

// -10 is TickSystem so we want to be just after that.
namespace MirrorState.Scripts.Rollback
{
    // Tracked Entities is server only...
    [DefaultExecutionOrder(-9)]
    public class TrackedEntity : NetworkBehaviour, ITrackedEntity
    {
        public uint TrackedTick => Latest.Tick;
        private struct State : ITick
        {
            public uint StateTick;

            public Vector3 Position;
            public Quaternion Rotation;
            public uint Tick => StateTick;
            public bool IsNew { get; set; }
        }

        // 50 fixed frames IE 1 second.
        private DejitterStructBuffer<State> _buffer = new DejitterStructBuffer<State>(TickSystem.TicksPerSecInt); 


        void OnEnable()
        {
            // Register with RollbackSystem for use with 'all rollback'
            RollbackSystem.Instance.Register(this);
        }
    
        void OnDisable()
        {
            // Remove itself from RollbackSystem
            RollbackSystem.Instance.Unregister(this);
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            // Add a state to the Array, List, or Ring Buffer?
            _buffer.Store(new State
            {
                IsNew = true, 
                Position = transform.position, 
                Rotation = transform.rotation,
                StateTick = TickSystem.Instance.Tick
            });
        }

        private State Latest;

        public void Rollback(uint tick)
        {
            // Rollback to the tick
            State state;
            if (!_buffer.TryGet(tick, out state))
            {
                Debug.LogWarning(gameObject.name + ": Tried to rollback further than was in the objects history.");
                return;
            }

            Latest = new State
            {
                IsNew = true,
                Position = transform.position,
                Rotation = transform.rotation,
                StateTick = TickSystem.Instance.Tick
            };
            Debug.Log(transform.root.gameObject.name + ": rolling back from " + transform.position.ToString("F4"));
            SetToState(state);
            Debug.Log(transform.root.gameObject.name + ": rolling back to " + transform.position.ToString("F4"));
        }

        // Rollback should always be followed up by Restore, so we can assume Latest is the exact same frame.
        public void Restore()
        {
            // Set state back to the latest state.

            SetToState(Latest);
        }

        private void SetToState(State state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
        }

        private void SetToState()
        {
            // Should be passed in a state object and set the necessary fields to match.
            // For now we're just doing position rotation, if we needed more we should just have the XXXState become a base class
            // And have the user override to determine exactly how it rolls back.
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using Mirror.RemoteCalls;
using MirrorState.Scripts;
using MirrorState.Scripts.Experimental;
using MirrorState.Scripts.Generated.States;
using MirrorState.Scripts.Networking;
using MirrorState.Scripts.Systems;
using UnityEngine;
using Fholm;

namespace MirrorState.Scripts
{
    public delegate void OnTickEvent(uint tick);

    // TODO: Move back to MonoBehaviour, add to the network manager, and use network messages instead of commands.
    [DefaultExecutionOrder(-155)]
    public class TickStateSystem : NetworkBehaviour
    {
        public static TickStateSystem Instance;
        
        private readonly Dictionary<uint, NetworkTickBehaviour> _behaviours = new Dictionary<uint, NetworkTickBehaviour>();
        private OnTickEvent _onTickEvent;
        
        [ReadOnly]
        public uint Tick = 0;
        [ReadOnly]
        public ushort TickDifference = 0; // This will always be 0 on the server, and the State interpolation should never run on the server anyway.
        [ReadOnly]
        public double RTT = 0;
        [ReadOnly]
        public bool Lagging = false;

        // Should use the last server tick. NOTE: Make sure sync rate is 0, which ensures clients only get the initial value, since that's all we really need.
        [SyncVar]
        public uint ServerTick;

        // TODO: We want delay to 

        // NOTE: This delay is for rolling back to a for a client on the server, so 300 is full RTT.
        [Range(0, 1)]
        public float MaxDelaySeconds = .3f;
        
        private float _clientInterpolationTime;
        public  float ClientInterpolationTime => _clientInterpolationTime;
        private float _clientMaxServerTimeReceived;

        private FloatIntegratorEma _clientTimeOffsetAvg;
        private FloatIntegratorEma _clientSnapshotDeliveryDeltaAvg;
        private float? _clientLastSnapshotReceived;

        // Really don't like this, but it get's around Awake not being called on disabled scene objects.
        public TickStateSystem()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Instance already exists, destroying!");
                Destroy(this);
                return;
            }
            Debug.Log("Tick System Setup");

            Instance = this;
        }

        private void OnEnable()
        {
            _clientTimeOffsetAvg = new FloatIntegratorEma();
            _clientTimeOffsetAvg.Initialize(TickUtils.TicksPerSec);

            _clientSnapshotDeliveryDeltaAvg = new FloatIntegratorEma();
            _clientSnapshotDeliveryDeltaAvg.Initialize(TickUtils.TicksPerSec);

            MirrorStateTicker.Instance.OnFixedUpdate += OnCustomUpdate;
            MirrorStateTicker.Instance.StartTick();
            Debug.Log("Tick System Started");
        }

        private void OnDisable()
        {
            MirrorStateTicker.Instance.OnFixedUpdate -= OnCustomUpdate;
        }

        public override void OnStartClient()
        {
            // Probably not exactly accurate, but it won't be the farthest behind either.
            Tick = ServerTick;
        }

        void OnCustomUpdate(double deltaTime)
        {
            if(!NetworkServer.active && !NetworkClient.active)
            {
                return;
            }

            if (NetworkServer.active)
            {
                // TODO: How would events be queued? and how would this call event methods with a specific data?
                //          State object can generate a method with a reader input for converting data into event calls?
                //          so data is Id: int, Event: byte[, variable data]
                //          This gets converted to and calls the appropriate event automatically (tick based obv, will need to figure out how to queue this data).
                /*
                Write (Server):  

                writer.WriteInt(currentTick)
                foreach(entity in entities) {
                    writer.WriteInt(entity.Id)
                    writer.WriteInt(entity.DirtyFlag)
                    entity.Serialize(writer) 
                } 

                Read (Client): 

                tick = reader.ReadInt()
                while(reader.HasData) {
                    id = reader.ReadInt()
                    flag = reader.ReadInt()
                    entity = entities[id]
                    entity.Deserialize(tick, flag, reader)
                }
                */
                
                PooledNetworkWriter writer = NetworkWriterPool.GetWriter();
                writer.WriteUInt32(Tick);
                writer.WriteSingle(Time.time);

                foreach (var entity in _behaviours)
                {
                    writer.WriteUInt32(entity.Key);
                    entity.Value.SerializeState(ref writer);
                }

                SendRPCInternal(typeof(TickStateSystem), "ReceiveServerUpdate", writer, 1, excludeOwner: true);
                ServerTick = Tick;
            }

            Tick++;
            _clientInterpolationTime += (float)MirrorStateTicker.Instance.ScaledDeltaTime;

            Publish(Tick);
        }

        private static void InvokeReceiveServerUpdate(NetworkBehaviour obj, NetworkReader reader, NetworkConnection senderConnection)
        {
            if (!NetworkClient.active)
            {
                Debug.LogError("RPC RpcTriggerEvent called on server.");
                return;
            }

            ((TickStateSystem)obj).ReceiveServerUpdate(reader);
        }

        private void ReceiveServerUpdate(NetworkReader reader)
        {
            // Do thing with recieved data;
            var tick = reader.ReadUInt32();
            var time = reader.ReadSingle();

            if (NetworkServer.active)
            {
                return;
            }

            while (reader.Position < reader.Length)
            {
                uint entityId = reader.ReadUInt32();
                //Debug.Log("Reading ID: " + entityId);
                NetworkTickBehaviour entity = _behaviours[entityId];
                Assert.Check(entity != null);

                // These should probably queue the result.
                entity.DeserializeState(tick, time, ref reader);
            }

            _clientMaxServerTimeReceived = Math.Max(_clientMaxServerTimeReceived, time);
            OnReceived(time);
        }

        private void OnReceived(float time)
        {
            if (_clientLastSnapshotReceived.HasValue)
            {
                _clientSnapshotDeliveryDeltaAvg.Integrate(Time.time - _clientLastSnapshotReceived.Value);
            }
            else
            {
                _clientInterpolationTime = time;
            }

            _clientLastSnapshotReceived = Time.time;

            float diff = _clientMaxServerTimeReceived - _clientInterpolationTime;

            _clientTimeOffsetAvg.Integrate(diff);

            float diffWanted = _clientTimeOffsetAvg.Value;

            if (diffWanted > TickUtils.InterpolationTimeAdjustmentPositiveThreshold)
            {
                MirrorStateTicker.Instance.TimestepSpeed = TickUtils.SecsPerTick * 1.01f;
            }
            else if (diffWanted < TickUtils.InterpolationTimeAdjustmentNegativeThreshold)
            {
                MirrorStateTicker.Instance.TimestepSpeed = TickUtils.SecsPerTick * 0.99f;
            }
            else
            {
                MirrorStateTicker.Instance.TimestepSpeed = TickUtils.SecsPerTick;
            }
            
            //Debug.Log($"diff: {diff:F3}, diffWanted: {diffWanted:F3}, time: {MirrorStateTicker.Instance.TimestepSpeed:F3}, deliveryDeltaAvg: {_clientSnapshotDeliveryDeltaAvg.Value}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Publish(uint tick)
        {
            _onTickEvent?.Invoke(tick);
        }

        public void Register(NetworkTickBehaviour behaviour)
        {
            _behaviours.Add(behaviour.netId, behaviour);
            _onTickEvent += behaviour.SystemTick;
        }

        public void Unregister(NetworkTickBehaviour behaviour)
        {
            _behaviours.Remove(behaviour.netId);
            _onTickEvent -= behaviour.SystemTick;
        }

        static TickStateSystem()
        {
            //RemoteCallHelper.RegisterCommandDelegate(typeof(TickCustomSystem), "CmdProcessCommand", InvokeReceiveServerUpdate, ignoreAuthority: false);
            RemoteCallHelper.RegisterRpcDelegate(typeof(TickStateSystem), "ReceiveServerUpdate", InvokeReceiveServerUpdate);
        }
    }

    public static class TickUtils
    {
        public const uint TicksPerSec = 30;
        public const float SecsPerTick = 1f / TicksPerSec; // Also the "fixedDeltaTime" of ticks
        //public const double SecsPerTickDouble = 1d / TicksPerSec; // Also the "fixedDeltaTime" of ticks
        public const float FixedDeltaTime = SecsPerTick;

        public const int SnapshotOffsetCount = 2;

        public const float InterpolationOffset = SecsPerTick * SnapshotOffsetCount;
        public static uint InterpolationOffsetTicks = SecondsToTicks(SecsPerTick * SnapshotOffsetCount);

        public const float InterpolationTimeAdjustmentNegativeThreshold = SecsPerTick * -0.5f;
        public const float InterpolationTimeAdjustmentPositiveThreshold = SecsPerTick * 2f;
        public static float InterpolationTimeAdjustmentNegativeThresholdTicks = SecondsToTicks(InterpolationTimeAdjustmentNegativeThreshold);
        public static float InterpolationTimeAdjustmentPositiveThresholdTicks = SecondsToTicks(InterpolationTimeAdjustmentPositiveThreshold);

        public static int ActionsPerMinuteToTicks(float apm)
        {
            if (apm <= 0)
            {
                return 0;
            }
            float aps = apm / 60f;
            return ActionsPerSecondToTicks(aps);
        }

        public static int ActionsPerSecondToTicks(float aps)
        {
            if (aps <= 0)
            {
                return 0;
            }
            float secsPerAction = 1f / aps;
            float ticksPerSec = secsPerAction / SecsPerTick;
            return Mathf.RoundToInt(ticksPerSec);
        }

        public static uint SecondsToTicks(float secs)
        {
            float ticksPerSec = secs / SecsPerTick;
            return (uint)Mathf.RoundToInt(ticksPerSec);
        }

        public static uint SecondsToTicks(double secs)
        {
            float ticksPerSec = (float)(secs / SecsPerTick);
            return (uint)Mathf.RoundToInt(ticksPerSec);
        }

    }
}
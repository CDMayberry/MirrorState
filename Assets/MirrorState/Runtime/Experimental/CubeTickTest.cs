using System;
using System.Collections.Generic;
using Mirror;
using MirrorState.Scripts.Networking;
using RailgunNet.Ticks.Buffers;
using RailgunNet.Ticks.Interfaces;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MirrorState.Scripts.Experimental
{
    public class CubeTickTest : NetworkTickBehaviour
    {
        [Serializable]
        struct CubeSnapshot : ITick
        {
            public Vector3 Position;
            public float Time;
            public uint Tick { get; set; }
            public bool IsNew { get; set; }
        }

        public GameObject Server;
        public GameObject Client;
        public bool Simulated;

        [SerializeField]
        List<CubeSnapshot> _clientCubeSnapshots = new List<CubeSnapshot>();

        private DejitterStructBuffer<CubeSnapshot> _cubeBuffer = new DejitterStructBuffer<CubeSnapshot>(60);

        public override void OnStartServer()
        {
            if (!Simulated)
            {
                Client.SetActive(false);
            }
        }

        public override void OnStartClient()
        {
            if (!Simulated && !NetworkServer.active)
            {
                Server.SetActive(false);
            }
        }

        public override void OnInterpolate(float delta)
        {
            if (!NetworkServer.active)
            {
                ClientRenderLatestPosition();
            }
        }

        public override void OnTick()
        {
            if (NetworkServer.active)
            {
                ServerMovement();
            }
        }

        public override void SerializeState(ref PooledNetworkWriter writer)
        {
            writer.WriteVector3(Server.transform.position);
        }

        public override void DeserializeState(uint tick, float time, ref NetworkReader reader)
        {
            var snapshot = new CubeSnapshot()
            {
                Position = reader.ReadVector3(),
                Tick = tick,
                Time = time
            };

            _cubeBuffer.Store(snapshot);
        }

        void ServerMovement()
        {
            Vector3 pos = default;
            pos.x = Mathf.PingPong(Time.time * 4, 10f) - 5f;

            Server.transform.position = pos;
        }

        // TODO: use Tick queue w/ MirrorStateTicker.Instance.InterpTime
        void ClientRenderLatestPosition()
        {
            //float interpTime = (float) MirrorStateTicker.Instance.InterpTime;
            // TODO: this interpTime doesn't work exactly, cause we need the time relative to the tick...?
            float clientInterpTime = (float)TickStateSystem.Instance.ClientInterpolationTime;
            var interpFrom = default(Vector3);
            var interpTo = default(Vector3);
            var interpAlpha = default(float);

            _cubeBuffer.GetFirstAfter(TickStateSystem.Instance.Tick, out var current, out var next);

            interpFrom = current.Position;
            interpTo = next.Position;

            /*for (int i = 0; i < _clientCubeSnapshots.Count; ++i)
            {

                if (i + 1 == _clientCubeSnapshots.Count)
                {
                    if (_clientCubeSnapshots[0].Time > clientInterpTime)
                    {
                        interpFrom = interpTo = _clientCubeSnapshots[0].Position;
                        interpAlpha = 0;
                    }
                    else
                    {
                        interpFrom = interpTo = _clientCubeSnapshots[i].Position;
                        interpAlpha = 0;
                    }
                }
                else
                {
                    //                v----- client interp time
                    // [0][1][2][3][4] [5][6][7][8][9]
                    //              F
                    //                 T

                    // F = 101.4 seconds
                    // INTERP TIME = 101.467
                    // T = 101.5 seconds

                    int f = i;
                    int t = i + 1;

                    if (_clientCubeSnapshots[f].Time <= clientInterpTime && _clientCubeSnapshots[t].Time >= clientInterpTime)
                    {

                        interpFrom = _clientCubeSnapshots[f].Position;
                        interpTo = _clientCubeSnapshots[t].Position;

                        float range = _clientCubeSnapshots[t].Time - _clientCubeSnapshots[f].Time;
                        float current = clientInterpTime - _clientCubeSnapshots[f].Time;

                        interpAlpha = Mathf.Clamp01(current / range);

                        break;
                    }
                }

            }*/

            Client.transform.position = Vector3.Lerp(interpFrom, interpTo, clientInterpTime);
        }
    }

    public struct FloatIntegratorEma
    {
        private float _ratio;
        private float _average;

        private bool _first;

        public bool Initialized => _ratio != 0f;
        public float Value => _average;

        public void Initialize(int count)
        {
            this = default;
            _ratio = 2.0f / (count + 1);
            _first = true;
        }

        public void Initialize(uint count)
        {
            this = default;
            _ratio = 2.0f / (count + 1);
            _first = true;
        }

        public void Integrate(float value)
        {
            if (_first)
            {
                _average = value;
                _first = false;
            }
            else
            {
                _average += _ratio * (value - _average);
            }
        }
    }
}

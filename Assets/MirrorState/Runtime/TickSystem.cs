using System;
using System.Collections.Generic;
using MirrorState.Scripts.Systems;
using Mirror;
using UnityEngine;

namespace MirrorState.Scripts
{
    // TODO: Move back to MonoBehaviour, add to the network manager, and use network messages instead of commands.
    [DefaultExecutionOrder(-155)]
    public class TickSystem : NetworkBehaviour
    {
        public static TickSystem Instance;
        

        // Client's timer starts the moment the scene starts...
        // Tge estimated tick of the server.
        [ReadOnly]
        public uint Tick = 0;
        // sbyte doesn't work exactly like a unsigned byte I think...
        [ReadOnly]
        public ushort TickDifference = 0; // This will always be 0 on the server, and the State interpolation should never run on the server anyway.
        [ReadOnly]
        public double RTT = 0;
        [ReadOnly]
        public bool Lagging = false;

        // TODO: This probably shouldn't be properties.
        //          IE if a client switches to a longer tick rate IE 2 -> 6, we don't want to go back states,
        //          instead we'd just want to wait until the Tick  + difference matches then passes the current ClientTick
        //          then continue (Ideally this doesn't happen in huge jumps, but I think we need to go further to handle that case)
        // The tick the client should be 'simulating'

        // The tick the remote states should be interpolating to.
        // (this should max out and just show them latest if the user goes over a certain latency limit)


        // Should use the last server tick. NOTE: Make sure sync rate is 0, which ensures clients only get the initial value, since that's all we really need.
        [SyncVar]
        public uint ServerTick;

        // TODO: We want delay to 

        // NOTE: This delay is for rolling back to a for a client on the server, so 300 is full RTT.
        [Range(0, 1)]
        public float MaxDelaySeconds = .3f;
        // These fields should be readonly in inspector
        [ReadOnly]
        public uint Delay = 0;
        [ReadOnly]
        public uint delayTick;

        // Matching fixed delta time atm. If we can find a way to have our own fixed loop and guarantee it will always run (Not sure if Unity can do that well) then we can separate the two.
        public static float SecsPerTick => Time.fixedDeltaTime;
        public static float TicksPerSec => 1f / SecsPerTick;
        public static int TicksPerSecInt => Mathf.RoundToInt(TicksPerSec);

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
            return (uint) Mathf.RoundToInt(ticksPerSec);
        }

        public static uint SecondsToTicks(double secs)
        {
            float ticksPerSec = (float) (secs / SecsPerTick);
            return (uint)Mathf.RoundToInt(ticksPerSec);
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.Log("Instance already exists, destroying!");
                Destroy(this);
                return;
            }

            Instance = this;
            Delay = SecondsToTicks(MaxDelaySeconds);
        }

        public override void OnStartClient()
        {
            // Probably not exactly accurate, but it won't be the farthest behind either.
            Tick = ServerTick;
        }


        // GOT SOMETHING: On rare occasion this is jumping ahead a tick, but the client's controller *isn't* also jumping a tick,
        //          The client can go 1773 -> 1774 -> (RTT recalc) -> 1776,
        //          so the client's now just treating 1775 as 1776 instead of doing it twice.
        // Solution: Keep track of 'lastTick', make fixed a while loop for clientOnly:
        //              -> while (lastTick < TickSystem.Tick) { ...; lastTick++ }
        // Alternative: This is probably also a bonus of Overwatch's 'Send All Since Last Server Response', as it'll just fill in.
        //              Consider building the logic to send all commands since last acknowledgement from server.
        [ClientRpc(channel = 1)]
        private void RpcUpdateTick(uint tick)
        {
            if (NetworkServer.active)
            {
                return;
            }

            RTT = NetworkTime.rtt;

            TickDifference = (ushort)(SecondsToTicks(RTT) + 1);

            /*if (TickDifference > Delay)
            {
                Lagging = true;
            }*/

            // ECS Talk 25:40, we want the client to be *ahead* of the server by half rtt, not matching.
            //  So does that mean we simply want RTT / SecPerTick? By the time we get a tick, it's been (RTT / 2) since then.
            //  so to get ahead, we need half RTT to catch up, and half RTT to get ahead...
            Tick = tick + TickDifference; // 1 is buffer time, for now hard-coded.

            // New question then: What is DelayTick used for? I believe it's state only, but now I need to dig into it.
        }
        
        // TODO: Though we're on fixed, we still need a way to speed up time. See FHolms stream for state interpolation, but we may still want a float timer per tick...?
        private void FixedUpdate()
        {
            Tick++;
            if (Tick >= Delay)
            {
                delayTick = Tick - Delay;
            }

            if (NetworkServer.active)
            {
                ServerTick = Tick;
                RpcUpdateTick(Tick);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}

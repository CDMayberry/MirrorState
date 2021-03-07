using UnityEngine;
using System.Collections;
using Mirror;
using MirrorState.Mirror;
using MirrorState.Scripts;

namespace Assets.MirrorState.Mirror
{
    // TODO: We also need to see if it always comes after a fixed update or not..., if it's inconsistent where it lands that's a problem. Though, if it's set lower than FixedUpdate maybe it won't be an issue.
    public class DebugTickBehaviour : TickBehaviour
    {
        [SyncVar]
        public int Test;

        protected override void TickSync(uint diff)
        {
            Debug.Log("Tick Sync'd at " + SyncTick + ", TickSystem at: " + TickSystem.Instance.Tick + ". Difference passed: " + diff + ". Set To " + Test);
        }


        void OnGUI()
        {
            if (!this.HasAnyAuthority())
            {
                GUILayout.Label("Server Set To: " + Test);
                return;
            }

            GUILayout.BeginArea(new Rect(10, 40 + 100, 215, 9999));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1"))
            {
                Test = 1;
            }
            else if (GUILayout.Button("2"))
            {
                Test = 2;
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
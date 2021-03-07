using System.Collections;
using MirrorState.Scripts;
using MirrorState.Scripts.Generation;
using UnityEngine;

namespace MirrorState.Scripts.Demo
{
    // TODO: Perhaps we can infer Predicted from whether it has a setter as well as a getter?
    // TODO: Move to Demo folder
    public interface IUnitDemo : IStateBase
    {
        // Disabled while I decide if these should have Transforms, and if so how to handle them being properties? IE Transforms are not referenced externally, they're just for state tracking and interpolation, they are private only essentially.
        /*[StateTransform(true, true, false)]
    Transform Root { get; set; }
    [StateTransform(false, true, false, Child = true)]
    Transform Child { get; set; }*/
        [StateAnim]
        float DemoHealth { get; set; }
        [StatePredicted]
        float DemoDamage { get; set; }

        [StatePredicted]
        event MirrorStateEvent DemoFire;
        event MirrorStateEvent DemoDeath;
    }

    // TODO: How would we do arguments? This needs to be generically passed to a RPC?
    public delegate void CustomMirrorStateEvent(int value);
    

    public class UnitDemoTest : IUnitDemo
    {
        public Transform Root { get; set; }
        public Transform Child { get; set; }
        public float DemoHealth { get; set; }
        public float DemoDamage { get; set; }
        public event MirrorStateEvent DemoFire;
        public event MirrorStateEvent DemoDeath;

        public void TriggerDemoFire()
        {
            DemoFire?.Invoke(0);
        }
    }
}
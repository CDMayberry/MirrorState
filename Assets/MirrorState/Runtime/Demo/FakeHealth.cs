using MirrorState.Scripts.Generation;
using UnityEngine;

namespace MirrorState.Scripts.Demo
{
    public class FakeHealth : MonoBehaviour, IUnitDemo
    {
        public float DemoHealth { get; set; }
        public float DemoDamage { get; set; }
        public event MirrorStateEvent DemoFire;
        public event MirrorStateEvent DemoDeath;
    }
}

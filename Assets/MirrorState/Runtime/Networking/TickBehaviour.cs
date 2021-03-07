using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using MirrorState.Scripts;
using UnityEngine;

namespace Assets.MirrorState.Scripts.Networking
{
    /*public abstract class TickBehaviour : MonoBehaviour, ISubscriber<uint>
    {
        public uint TicksPerUpdate = 1;

        private void OnEnable()
        {
            TickCustomSystem.Instance.Subscribe(this);
        }

        private void OnDisable()
        {
            TickCustomSystem.Instance.Unsubscribe(this);
        }

        public abstract void OnTick();

        private uint _tickCount;

        public void SystemTick(uint tick)
        {
            _tickCount += 1;
            if (_tickCount >= TicksPerUpdate)
            {
                OnTick();
                _tickCount = 0;
            }
        }
    }*/
}

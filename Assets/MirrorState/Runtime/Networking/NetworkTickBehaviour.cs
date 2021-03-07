using Mirror;
using MirrorState.Scripts.Experimental;

namespace MirrorState.Scripts.Networking
{
    public abstract class NetworkTickBehaviour : NetworkBehaviour
    {
        public uint TicksPerUpdate = 1;

        public override void OnStartNetwork()
        {
            TickStateSystem.Instance.Register(this);
        }

        public override void OnStopNetwork()
        {
            TickStateSystem.Instance.Unregister(this);
        }

        /*private void OnEnable()
        {
            TickStateSystem.Instance.Register(this);
        }

        private void OnDisable()
        {
            TickStateSystem.Instance.Unregister(this);
        }*/

        // Probably want something better than this?
        private void LateUpdate()
        {
            OnInterpolate((float)MirrorStateTicker.Instance.InterpTime);
        }

        public abstract void OnInterpolate(float delta);
        public abstract void OnTick();

        private uint _tickCount;

        public abstract void SerializeState(ref PooledNetworkWriter writer);
        public abstract void DeserializeState(uint tick, float time, ref NetworkReader reader);

        public void SystemTick(uint tick)
        {
            _tickCount += 1;
            if (_tickCount >= TicksPerUpdate)
            {
                OnTick();
                _tickCount = 0;
            }
        }
    }
}

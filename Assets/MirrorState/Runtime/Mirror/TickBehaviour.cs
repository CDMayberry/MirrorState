using UnityEngine;
using System.Collections;
using Mirror;
using MirrorState.Scripts;

namespace Assets.MirrorState.Mirror
{

    public abstract class TickBehaviour : NetworkBehaviour
    {
        private uint _lastTick;
        protected uint LastTick => _lastTick;

        [SyncVar]
        private uint _syncTick;
        protected uint SyncTick => _syncTick;

        protected virtual void Awake()
        {
            // I Don't like this, but TickSystem and syncInterval aren't both using fixed update, so it can double up Sync's here.
            /*if (this.hasAuthority && !this.isServer)
            {
                this.syncInterval = 0;
            }
            else
            {
                this.syncInterval = TickSystem.SecsPerTick / 2f;
            }*/
            this.syncInterval = TickSystem.SecsPerTick / 2f;
        }

        protected abstract void TickSync(uint diff);

        protected virtual void FixedUpdate()
        {
            if (this.isServer)
            {
                _syncTick = TickSystem.Instance.Tick;
            }
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            //writer.WriteBytesAndSize();

            // Issue: the dirty flags are unchecked after OnSerialize, via ClearDirtyComponentsDirtyBits in NetworkIdentity. For now this will be fine, sending it slightly split up isn't a big issue atm, though cutting down on messages definitely could be important.
            /*if (!initialState && _lastTick == _syncTick)
            {
                writer.WriteBoolean(false);
                return false;
            }

            writer.WriteBoolean(true);*/

            //Debug.Log("Serialize");
            var result = base.OnSerialize(writer, initialState);
            if (this.isServer && _lastTick != _syncTick)
            {
                TickSync(_syncTick - _lastTick);
                _lastTick = _syncTick;
            }
            return result;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            //Debug.Log("Deserialize");
            /*var changed = reader.ReadBoolean();
            if (!changed)
            {
                return;
            }*/

            base.OnDeserialize(reader, initialState);
            // TODO: Does this run on the server as well?
            if (!this.isServer && _lastTick != _syncTick)
            {
                TickSync(_syncTick - _lastTick);
                _lastTick = _syncTick;
            }
        }
    }
}
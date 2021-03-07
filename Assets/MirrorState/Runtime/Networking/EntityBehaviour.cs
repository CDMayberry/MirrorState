using System;
using System.Collections.Generic;
using Mirror;

namespace Mayberry.Scripts.Networking
{
    /*public abstract class EntityBehaviour<T> : NetworkBehaviour where T : struct
    {
        private Queue<T> _inputs = new Queue<T>();

        private void LateUpdate()
        {
            if (hasAuthority)
            {
                var input = ClientUpdate();
                _inputs.Enqueue(input);
            }

            if (NetworkServer.active)
            {

            }
        }

        public abstract T ClientUpdate();

        public abstract void ServerUpdate();

        public abstract void RunCommand();
    }*/
}

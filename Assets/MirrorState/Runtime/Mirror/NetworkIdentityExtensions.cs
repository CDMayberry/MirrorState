using Mirror;

namespace MirrorState.Mirror
{
    public static class NetworkIdentityExtensions
    {
        /// <summary>
        /// If the server is running and the identity doesn't have a connection to a client, that means it is owned by the server.
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        public static bool HasServerAuthority(this NetworkIdentity identity)
        {
            return identity.isServer && identity.connectionToClient == null;
        }
        /// <summary>
        /// If the server is running and the identity doesn't have a connection to a client, that means it is owned by the server.
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        public static bool HasAnyAuthority(this NetworkIdentity identity)
        {
            return identity.hasAuthority || identity.isServer && identity.connectionToClient == null;
        }

        /// <summary>
        /// Checks if client has authority or the server owns the object. Meant to handle case where server controls objects, like AI.
        /// </summary>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        public static bool HasAnyAuthority(this NetworkBehaviour behaviour)
        {
            return behaviour.hasAuthority || behaviour.netIdentity.HasServerAuthority();
        }
    }
}

using System;
using MirrorState.Scripts.Generation;
using Mirror;
using MirrorState.Scripts.Rollback;
using UnityEngine;

namespace MirrorState.Scripts
{
    public static class MirrorStatePhysics
    {
        /*private static void RaycastRollback(uint tick, Vector3 origin, Vector3 direction, Action action)
        {
            // GOAL: Let's aim to have this be a complete call, IE -> Rollback Raycast -> Rollback -> Regular Raycast -> Restore -> Return Hit(s).

            // TODO: This should do a raycast and hit the Rollback layer. Any entities hit should be passed back?
            // Or do we want to roll this into the Rollback as well?

            // IE Do raycast for rollback, then call the action?
            // Would we want to then give them control? Or would we immediately do a second raycast in with both gameObjects and rewound objects?
            // Better question: Should Raycast be considered the entire process or just the basic rollback raycast?

            RaycastHit[] hits = { };
            var size = Physics.RaycastNonAlloc(origin, direction, hits, 100, LayerMask.NameToLayer("Rollback"));

            if (size > 0)
            {
                var entities = RollbackSystem.Instance.ToTrackedEntities(size, hits);
                RollbackSystem.Instance.RollbackAndRestore(tick, entities, action);
            }
            else
            {
                action();
            }
        }*/

        private static readonly Collider[] RollbackColliderHits = new Collider[20];
        private static readonly RaycastHit[] RollbackHits = new RaycastHit[20];
        private static readonly RaycastHit[] RegularHits = new RaycastHit[20];
        private static readonly int _rollbackLayer = 1 << LayerMask.NameToLayer("Rollback");
        private static readonly int _hitLayer = 1 << LayerMask.NameToLayer("Unit");

        /// <summary>
        /// Raycast handles the rollback and restore process for non-authority calls on the server (IE not host object) and acts like a regular raycast otherwise.
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="hasAuthority">If true, will skip the rollback since the server is the authority.</param>
        /// <param name="origin"></param>
        /// <param name="radius"></param>
        /// <param name="actionOnHit">Will be called after the raycast completes, passing in any contacts. arguments are number of targets hit and the pre-allocated hits array</param>
        public static void OverlapSphere(uint tick, bool hasAuthority, Vector3 origin, float radius, Action<int, Collider[]> actionOnHit)
        {
            // TODO: The inbound ticks needs to incorporate the TickDifference
            // For Host objects we don't actually need to rollback.
            if (NetworkServer.active && !hasAuthority)
            {
                ServerOverlapSphere(tick - TickSystem.Instance.Delay, origin, radius, actionOnHit);
            }
            else
            {
                ClientOverlapSphere(origin, radius, actionOnHit);
            }
        }


        private static void ServerOverlapSphere(uint tick, Vector3 origin, float radius, Action<int, Collider[]> actionOnHit)
        {
            int size = Physics.OverlapSphereNonAlloc(origin, radius, RollbackColliderHits, _rollbackLayer, QueryTriggerInteraction.Ignore);

            ITrackedEntity[] entities = null;
            if (size > 0)
            {
                entities = RollbackSystem.Instance.ToTrackedEntities(size, RollbackHits);
                RollbackSystem.Instance.Rollback(tick, entities);
                // TODO: probably move this into the RollbackSystem.
                Physics.SyncTransforms();
            }

            ClientOverlapSphere(origin, radius, actionOnHit);

            if (size > 0)
            {
                RollbackSystem.Instance.Restore(entities);
                Physics.SyncTransforms();
            }
        }

        private static void ClientOverlapSphere(Vector3 origin, float radius, Action<int, Collider[]> actionOnHit)
        {
            int regularSize = Physics.OverlapSphereNonAlloc(origin, radius, RollbackColliderHits, _hitLayer, QueryTriggerInteraction.Ignore);

            actionOnHit(regularSize, RollbackColliderHits);
        }

        /// <summary>
        /// Raycast handles the rollback and restore process for non-authority calls on the server (IE not host object) and acts like a regular raycast otherwise.
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="hasAuthority">If true, will skip the rollback since the server is the authority.</param>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="layerMask"></param>
        /// <param name="actionOnHit">Will be called after the raycast completes, passing in any contacts. arguments are number of targets hit and the pre-allocated hits array</param>
        public static void Raycast(uint tick, bool hasAuthority, Vector3 origin, Vector3 direction, int layerMask, Action<int, RaycastHit[]> actionOnHit)
        {
            // TODO: The inbound ticks needs to incorporate the TickDifference
            // For Host objects we don't actually need to rollback.
            /*if (NetworkServer.active && !hasAuthority)
            {
                NonAuthoritativeRaycast(tick - TickSystem.Instance.Delay, origin, direction, layerMask, actionOnHit);
            }
            else
            {
                AuthoritativeRaycast(origin, direction, layerMask, actionOnHit);
            }*/

            if (hasAuthority)
            {
                ClientRaycast(origin, direction, layerMask, actionOnHit);
            }
            else if (NetworkServer.active)
            {
                ServerRaycast(tick - TickSystem.Instance.Delay, origin, direction, layerMask, actionOnHit);
            }
            else
            {
                ClientRaycast(origin, direction, layerMask, actionOnHit);
                // This assumes State is passing delayTick which already accounts for difference.
                //ServerRaycast(tick, origin, direction, layerMask, actionOnHit);
            }
        }

        /// <summary>
        /// Rollback run for non-authoritative objects on the server (IE not host object)
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="layerMask"></param>
        /// <param name="actionOnHit"></param>
        private static void ServerRaycast(uint tick, Vector3 origin, Vector3 direction, int layerMask, Action<int, RaycastHit[]> actionOnHit)
        {
            int size = Physics.RaycastNonAlloc(origin, direction, RollbackHits, 100, _rollbackLayer);

            ITrackedEntity[] entities = null;
            if (size > 0)
            {
                entities = RollbackSystem.Instance.ToTrackedEntities(size, RollbackHits);
                RollbackSystem.Instance.Rollback(tick, entities);
                // TODO: probably move this into the RollbackSystem.
                Physics.SyncTransforms();
            }

            ClientRaycast(origin, direction, layerMask, actionOnHit);

            if (size > 0)
            {
                RollbackSystem.Instance.Restore(entities);
                Physics.SyncTransforms();
            }
        }

        private static void ClientRaycast(Vector3 origin, Vector3 direction, int layerMask, Action<int, RaycastHit[]> actionOnHit)
        {
            int regularSize = Physics.RaycastNonAlloc(origin, direction, RegularHits, 100, layerMask);

            actionOnHit(regularSize, RegularHits);
        }

        /// <summary>
        /// Raycast handles the rollback and restore process for non-authority calls on the server (IE not host object).
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="hasAuthority">If true, will skip the rollback since the server is the authority.</param>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="actionDuringRollback">Will be called after the raycast rollback completes.</param>
        public static void RollbackRaycast(uint tick, bool hasAuthority, Vector3 origin, Vector3 direction, Action actionDuringRollback)
        {
            // TODO: The inbound ticks needs to incorporate the TickDifference
            // For Host objects we don't actually need to rollback.
            if (NetworkServer.active && !hasAuthority)
            {
                ServerRollbackRaycast(tick - TickSystem.Instance.Delay, origin, direction, actionDuringRollback);
            }
            else
            {
                actionDuringRollback();
            }
            // TODO: probably need 3rd case for 'Observer' functionality.
        }

        /// <summary>
        /// Rollback run for non-authoritative objects on the server (IE not host object)
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="actionDuringRollback"></param>
        private static void ServerRollbackRaycast(uint tick, Vector3 origin, Vector3 direction, Action actionDuringRollback)
        {
            int size = Physics.RaycastNonAlloc(origin, direction, RollbackHits, 100, _rollbackLayer);

            ITrackedEntity[] entities = null;
            if (size > 0)
            {
                entities = RollbackSystem.Instance.ToTrackedEntities(size, RollbackHits);
                RollbackSystem.Instance.Rollback(tick, entities);
                // TODO: probably move this into the RollbackSystem.
                Physics.SyncTransforms();
            }

            actionDuringRollback();

            if (size > 0)
            {
                RollbackSystem.Instance.Restore(entities);
                Physics.SyncTransforms();
            }
        }

        /// <summary>
        /// Meant to be used with projectiles.
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="entity"></param>
        /// <param name="actionOnRollback"></param>
        public static void RollbackSelf(uint tick, ITrackedEntity entity, Action actionOnRollback)
        {
            //
            if (!entity.hasAuthority && !NetworkServer.active)
            {
                entity.Rollback(tick);
            }

            try
            {
                actionOnRollback();
            }
            finally
            {
                if (!entity.hasAuthority && !NetworkServer.active)
                {
                    entity.Restore();
                }
            }

        }

        // What about for projectiles? That requires a little more complicated logic (see the MWO link)
        // https://www.gamasutra.com/blogs/NeemaTeymory/20160906/280377/Why_Making_Multiplayer_Games_is_Hard_Lag_Compensating_Weapons_in_MechWarrior_Online.php
        // TODO: Need a RollbackAdvanced for use with projectiles?
        // How To:  Use speed and time since tick to determine how far it needs to raycast. Use RollbackHits to decide which entities to interpolate.
        //      Rollback hitboxes are a fixed size, later on they should be based on the state history, but as a naive approach this works.

    }
}

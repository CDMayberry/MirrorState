using Mirror;
using UnityEngine;

// TODO: What if the tracked entity knew it's tick? 
//          If predicted, this should be the latest tick
//          If server non-authoritative, Tick - 1 
//          If non-authoritative, would point to the latest tick that it's interpolating to. (ignoring queue'd states)
// GOAL: The TrackedTick of a entity should always be the amount something should roll back to if need be...?
namespace MirrorState.Scripts.Generation
{
    public interface ITrackedEntity
    {
        uint TrackedTick { get; }
        bool hasAuthority { get; }
        GameObject gameObject { get; }
        NetworkIdentity netIdentity { get; }
        uint netId { get; }
        void Rollback(uint tick);
        void Restore();
    }
}
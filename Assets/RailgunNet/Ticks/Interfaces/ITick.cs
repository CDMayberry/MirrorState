using UnityEngine;

namespace RailgunNet.Ticks.Interfaces
{

    public interface ITick
    {
        uint Tick { get; }
        bool IsNew { get; set; }
    }

    public interface ITickable
    {
        void SendTick();
    }
}

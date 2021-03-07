using RailgunNet.Ticks.Interfaces;

namespace RailgunNet.Ticks.Samples
{

    public class TickMessage : ITick, ITickPoolable<TickMessage>
    {
        // Assume 0 is bad...
        public uint CurrentTick;
        public int Value;
        public uint Tick => CurrentTick;
        public bool IsNew { get; set; }

        public void Reset()
        {
            CurrentTick = 0;
            Value = 0;
        }
    }

}

using System.Collections.Generic;
using System.Linq;
using RailgunNet.Ticks.Interfaces;
using Random = System.Random;


namespace RailgunNet.Ticks.Samples
{
    public class TickClient : ITickable
    {
        public int Id { get; }
        private readonly RemoteClient _remote;
        private readonly System.Random _random;
        public bool FailTick = false;
        public readonly ClientClock _ticker;
        public readonly Queue<TickMessage> _outbound;
        public uint LastAck = TickConstants.BadTick;

        public TickClient(RemoteClient remote, int id)
        {
            Id = id;
            _remote = remote;
            _outbound = new Queue<TickMessage>();
            _random = new Random();
            _ticker = new ClientClock(this);
        }

        // This should always being queuing it's changes and ticking.
        public void Update()
        {
            var message = TickPool<TickMessage>.Allocate();
            message.CurrentTick = _ticker.TimerTick;
            message.Value = _random.Next(1, 100);

            _outbound.Enqueue(message);

            _ticker.Tick();
        }

        public void CommandAck(uint ackTick)
        {
            LastAck = ackTick;

            while (_outbound.Count > 0)
            {
                TickMessage command = _outbound.Peek();
                // Go until you reach a tick that is later than the ackTick.
                if (command.Tick > ackTick)
                {
                    break;
                }

                TickMessage message = _outbound.Dequeue();
                TickPool<TickMessage>.Deallocate(message);
            }
        }

        public void SendTick()
        {
            if (FailTick && _random.Next(1, 100) > 70)
            {
                //Console.WriteLine("Dropped Tick");
                return;
            }

            _remote.ReceiveCommands(_outbound.ToList());
        }
    }
}



using System.Collections.Generic;
using System.Linq;
using RailgunNet.Ticks.Buffers;
using RailgunNet.Ticks.Interfaces;


namespace RailgunNet.Ticks.Samples
{
    // Server's representation of the player client.
    public class RemoteClient : ITickable
    {
        public RemoteClock Clock;
        public TickClient Client;
        public int Id { get; }
        // Shouldn't be a queue but a buffer...
        private readonly DejitterBuffer<TickMessage> _inbound = new DejitterBuffer<TickMessage>(60);

        public RemoteClient(int id)
        {
            Id = id;
            Clock = new RemoteClock(this);
        }

        public void Update()
        {
            // Needs to be clearing out _inbound at a certain rate.
            Clock.Update(_inbound.Latest?.Tick ?? 0);
        }

        public void SendTick()
        {
            Client.CommandAck(Clock.EstimatedRemote);
            // Send _inbound to other clients...?
        }

        public void ReceiveCommands(List<TickMessage> commands)
        {
            var results = commands.Select(x =>
            {
                // This would be done when client sends to server but we don't have that barrier here.
                var dup = TickPool<TickMessage>.Allocate();
                dup.CurrentTick = x.CurrentTick;
                dup.Value = x.Value;
                return dup;
            }).ToList();

            foreach (var command in results)
            {
                ReceiveCommand(command);
            }
        }

        private void ReceiveCommand(TickMessage command)
        {
            if (_inbound.Store(command))
            {
                //IsNewCommand was never being used...
                command.IsNew = true;
            }
            else
            {
                TickPool<TickMessage>.Deallocate(command);
            }
        }
    }
}

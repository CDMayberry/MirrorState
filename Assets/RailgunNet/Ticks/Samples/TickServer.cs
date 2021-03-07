using System.Collections.Generic;
using RailgunNet.Ticks.Interfaces;

namespace RailgunNet.Ticks.Samples
{
    public class TickServer : ITickable
    {
        public readonly ClientClock _ticker;
        private readonly List<RemoteClient> _remotes = new List<RemoteClient>();
        public IReadOnlyList<RemoteClient> Remotes => _remotes;

        public TickServer()
        {
            _ticker = new ClientClock(this);
        }

        public void Update()
        {
            _ticker.Tick();
        }

        public void SendTick()
        {
            foreach (RemoteClient remoteClient in _remotes)
            {
                remoteClient.Update();
            }
        }

        public List<TickClient> AddClients(int numClients = 1)
        {
            _remotes.Clear();
            var clients = new List<TickClient>();

            for (int i = 0; i < numClients; i++)
            {
                var remote = new RemoteClient(i + 1);
                var client = new TickClient(remote, i + 1);
                remote.Client = client;

                _remotes.Add(remote);
                clients.Add(client);
            }

            return clients;
        }
    }
}

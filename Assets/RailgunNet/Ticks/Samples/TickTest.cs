using System;
using System.Threading;

namespace RailgunNet.Ticks.Samples
{
    public class TickTest
    {
        private TickServer _server;

        public TickTest()
        {
            _server = new TickServer();
        }

        public void Run(int numClients = 1)
        {
            var clients = _server.AddClients(numClients);

            Console.WriteLine("Press any key to exit...");
            const int consoleServerTop = 1;
            while (true)
            {
                for (int i = 0; i < clients.Count; i++)
                {
                    TickClient client = clients[i];
                    client.Update();
                    Console.SetCursorPosition(0, consoleServerTop + 1 + (client.Id - 1) * 3);
                    Console.WriteLine("Client " + client.Id + ": " + client._ticker.TimerTick);
                    Console.WriteLine("Server Estimate: " + _server.Remotes[i].Clock.EstimatedRemote);
                    Console.WriteLine("Last ACK: " + client.LastAck);
                    Console.WriteLine("Outbound: " + client._outbound.Count.ToString().PadLeft(2, '0'));
                }

                _server.Update();
                Console.SetCursorPosition(0, consoleServerTop);
                Console.WriteLine("Server: " + _server._ticker.TimerTick);

                if (Console.KeyAvailable)
                {
                    break;
                }
                Thread.Sleep(16); // 60 fps baby
            }

            //Console.WriteLine("Exiting");
        }
    }
}
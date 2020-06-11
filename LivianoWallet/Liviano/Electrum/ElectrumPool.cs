//
// ElectrumPool.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2020 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Linq;
using System.Threading.Tasks;
using Liviano.Models;

namespace Liviano.Electrum
{
    public class ElectrumPool
    {
        public const int MIN_NUMBER_OF_CONNECTED_SERVERS = 2;

        public bool Connected { get; private set; }

        Server currentServer;
        public Server CurrentServer
        {
            get
            {
                return currentServer;
            }

            set
            {
                if (currentServer is null && !Connected)
                {
                    Connected = true;

                    OnConnectedEvent?.Invoke(this, currentServer);
                }

                if (value is null && !(currentServer is null))
                {
                    Connected = false;

                    OnDisconnectedEvent?.Invoke(this, currentServer);
                }

                currentServer = value;
                ElectrumClient = currentServer.ElectrumClient;

                OnCurrentServerChangedEvent?.Invoke(this, CurrentServer);
            }
        }

        public event EventHandler<Server> OnCurrentServerChangedEvent;

        public event EventHandler<Server> OnConnectedEvent;

        public event EventHandler<Server> OnDisconnectedEvent;

        public Server[] AllServers { get; set; }

        public Server[] ConnectedServers { get; set; }

        public ElectrumClient ElectrumClient { get; private set; }

        public ElectrumPool(Server[] servers)
        {
            AllServers = ShuffleServers(servers);

            // TODO This should be replaced by something way smarter
            CurrentServer = AllServers[0];
        }

        public async Task FindConnectedServers()
        {

        }

        Server[] ShuffleServers(Server[] servers)
        {
            Random rnd = new Random();

            return servers.OrderBy(n => rnd.Next()).ToArray();
        }
    }

    public class CurrentServerChangedEventArgs
    {
        public CurrentServerChangedEventArgs(Server server) { Server = server; }
        public Server Server { get; } // readonly
    }

}

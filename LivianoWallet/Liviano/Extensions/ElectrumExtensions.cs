//
// ElectrumExtensions.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 
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
using System.Collections.Generic;
using System.Linq;

using Liviano.Models;

namespace Liviano.Extensions
{
    public static class ElectrumExtensions
    {
        /// <summary>
        /// Filter the list out of onion domains since we don't support them yet
        /// </summary>
        /// <param name="servers"></param>
        /// <returns></returns>
        public static List<Server> CompatibleServers(this List<Server> servers)
        {
            return servers.Where(server =>
                !server.Domain.EndsWith(".onion", StringComparison.CurrentCulture) &&
                server.PrivatePort != null
            ).ToList();
        }

        public static void RemoveServer(this List<Server> servers, Server server)
        {
            var i = servers.FindIndex(s => s.Domain == server.Domain && s.PrivatePort == server.PrivatePort);

            servers.RemoveAt(i);
        }

        public static bool ContainsServer(this List<Server> servers, Server server)
        {
            return servers.Any(s =>
                s.Domain != null &&
                s.Domain == server.Domain &&
                s.PrivatePort == server.PrivatePort
            );
        }

        public static bool ContainsServer(this Server[] servers, Server server)
        {
            return (new List<Server>(servers)).ContainsServer(server);
        }

        public static Server[] Shuffle(this Server[] servers)
        {
            Random rnd = new Random();

            return servers.OrderBy(n => rnd.Next()).ToArray();
        }
    }
}

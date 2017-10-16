using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets
{
    public static class SocketsExtensions
    {
        public static async Task ConnectAsync(this Socket me, EndPoint remoteEP, CancellationToken cancel)
        {
            await me.ConnectAsync(remoteEP).WithCancellation(cancel);
        }
    }
}

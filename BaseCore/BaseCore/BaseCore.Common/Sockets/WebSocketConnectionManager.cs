using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BaseCore.Common.Sockets
{
    public class WebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

        public WebSocket GetSocketById(string socketId)
        {
            return this._sockets.FirstOrDefault(x => x.Key == socketId).Value;
        }

        public ConcurrentDictionary<string, WebSocket> GetAllSockets()
        {
            return this._sockets;
        }

        public string GetSocketId(WebSocket socket)
        {
            return this._sockets.FirstOrDefault(x => x.Value == socket).Key;
        }

        public void AddSocket(WebSocket socket)
        {
            this._sockets.TryAdd(this.GenerateConnectionId(), socket);
        }

        private string GenerateConnectionId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public async Task RemoveSocket(string socketId)
        {
            this._sockets.TryRemove(socketId, out var socket);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed.", CancellationToken.None);
        }
    }
}

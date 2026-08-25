using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BaseCore.Common.Sockets
{
    public abstract class WebSocketHandler
    {
        protected WebSocketConnectionManager WebSocketConnectionManager { get; set; }

        public WebSocketHandler(WebSocketConnectionManager socketManager)
        {
            this.WebSocketConnectionManager = socketManager;
        }

        public virtual async Task OnConnected(WebSocket socket)
        {
            this.WebSocketConnectionManager.AddSocket(socket);
        }

        public virtual async Task OnDisconnected(WebSocket socket)
        {
            var id = this.WebSocketConnectionManager.GetSocketId(socket);
            await this.WebSocketConnectionManager.RemoveSocket(id);
        }

        public async Task SendMessageAsync(WebSocket socket, string message)
        {
            Debug.Print(message);
            if (socket.State != WebSocketState.Open) { return; }
            var bytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task SendMessageAsync(string socketId, string message)
        {
            var socket = this.WebSocketConnectionManager.GetSocketById(socketId);
            await SendMessageAsync(socket, message);
        }

        public async Task SendMessageToAllAsync(string message)
        {
            foreach (var socket in WebSocketConnectionManager.GetAllSockets())
            {
                if (socket.Value.State == WebSocketState.Open)
                {
                    await SendMessageAsync(socket.Value, message);
                }
            }
        }

        public abstract Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, byte[] buffer);
    }
}

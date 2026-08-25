using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BaseCore.Common.Sockets
{
    public class WebSocketManagerMiddleware
    {
        private readonly RequestDelegate _next;
        private WebSocketHandler _webSocketHandler { get; set; }
        public WebSocketManagerMiddleware(RequestDelegate next, WebSocketHandler webSocketHandler)
        {
            this._next = next;
            this._webSocketHandler = webSocketHandler;
        }

        public async Task Invoke(HttpContext context)
        {
            // If the request is not a WebSocket request, it just exits the middleware.
            if (!context.WebSockets.IsWebSocketRequest) { return; }

            // If it is a WebSockets request,
            // then it accepts the connection and passes the socket to the OnConnected method from the WebSocketHandler.
            var socket = await context.WebSockets.AcceptWebSocketAsync();

            // while the socket is in the Open state, it awaits for the receival of new data.
            await this._webSocketHandler.OnConnected(socket);

            // When it receives the data, it decides wether to pass the context to the ReceiveAsync method of WebSocketHandler
            // (that's why you need to pass an actual implementation of the abstract WebSocketHandler class)
            // or to the OnDisconnected method (if the message type is Close).
            await Receive(socket, async (result, buffer) =>
            {
                var str = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                Debug.Print(str);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    await this._webSocketHandler.ReceiveAsync(socket, result, buffer);
                    return;
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // at this time, we won't deal this part
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await this._webSocketHandler.OnDisconnected(socket);
                    return;
                }
            });
        }

        private async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
        {
            const int BUFFER_LENGTG = 4096; // 4 * 1024;
            var buffer = new byte[BUFFER_LENGTG];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                handleMessage(result, buffer);
            }
        }
    }
}

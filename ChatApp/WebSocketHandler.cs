using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatApp
{
    public abstract class WebSocketHandler
    {
        public WebSocketHandler(ConnectionManager connectionManager)
        {
            ConnectionManager = connectionManager;
        }

        protected ConnectionManager ConnectionManager { get; set; }

        public virtual async Task OnConnected(WebSocket socket, string username)
        {

            var connectionError = ValidateUsername(username);

            if (!string.IsNullOrEmpty(connectionError))
            {
                await ConnectionManager.RemoveSocket(socket, connectionError);
            }
            else
            {
                ConnectionManager.AddSocket(socket);
                ConnectionManager.AddUser(socket, username);

                var connectMessage = new ServerMessage(username, false, GetAllUsers());
                await BroadcastMessage(JsonSerializer.Serialize(connectMessage));
            }
        }

        public string ValidateUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return "Username must not be empty";

            if (ConnectionManager.UsernameAlreadyExists(username)) return $"User {username} already exists";

            return null;
        }

        public virtual async Task<string> OnDisconnected(WebSocket socket)
        {
            var socketId = ConnectionManager.GetId(socket);
            await ConnectionManager.RemoveSocket(socket);

            var username = ConnectionManager.GetUsernameBySocketId(socketId);
            ConnectionManager.RemoveUser(username);

            return username;
        }

        public async Task SendMessageAsync(WebSocket socket, string message)
        {
            if (socket.State != WebSocketState.Open)
                return;

            await socket.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(message),
                    0,
                    message.Length),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        public async Task SendMessageAsync(string socketId, string message)
        {
            await SendMessageAsync(ConnectionManager.GetSocketById(socketId), message);
        }

        public async Task SendMessageToAllAsync(string message)
        {
            
            foreach (var pair in ConnectionManager.GetAllSockets())
                if (pair.Value.State == WebSocketState.Open)
                    await SendMessageAsync(pair.Value, message);
        }

        public async Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, byte[] buffer)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

            await SendMessageToAllAsync(message);
        }

        public string ReceiveString(WebSocketReceiveResult result, byte[] buffer)
        {
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }

        public async Task BroadcastMessage(string message)
        {
            await SendMessageToAllAsync(message);
        }

        public List<string> GetAllUsers()
        {
            return ConnectionManager.GetAllUsernames();
        }

        public string GetUsernameBySocket(WebSocket socket)
        {
            return ConnectionManager.GetUsernameBySocket(socket);
        }
    }
}
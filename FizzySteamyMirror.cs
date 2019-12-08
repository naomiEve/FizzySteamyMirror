using UnityEngine;
using Steamworks;
using System;
using System.Collections.Generic;

namespace Mirror.FizzySteam
{
    [HelpURL("https://vis2k.github.io/Mirror/Transports/Fizzy")]
    public class FizzySteamyMirror : Transport
    {
        public int steamAppId;

        protected FizzySteam.Client client = new FizzySteam.Client();
        protected FizzySteam.Server server = new FizzySteam.Server();
        public float messageUpdateRate = 0.03333f;
        public P2PSend[] channels = new P2PSend[2] { P2PSend.Reliable, P2PSend.Unreliable };        

        private void Start()
        {
            // Set the Steam AppId for Facepunch.Steamworks initialization
            client.steamAppId = steamAppId;
            server.steamAppId = steamAppId;

            Common.secondsBetweenPolls = messageUpdateRate;
            if (channels == null) {
                channels = new P2PSend[2] { P2PSend.Reliable, P2PSend.Unreliable };
            }
            channels[0] = P2PSend.Reliable;
            Common.channels = channels;
        }

        public FizzySteamyMirror()
        {
            // Set up server events
            server.OnConnected += (id) => OnServerConnected?.Invoke(id);
            server.OnDisconnected += (id) => OnServerDisconnected?.Invoke(id);
            server.OnReceivedData += (id, data, channel) => OnServerDataReceived?.Invoke(id, new ArraySegment<byte>(data), channel);
            server.OnReceivedError += (id, exception) => OnServerError?.Invoke(id, exception);

            // Set up client events
            client.OnConnected += () => OnClientConnected?.Invoke();
            client.OnDisconnected += () => OnClientDisconnected?.Invoke();
            client.OnReceivedData += (data, channel) => OnClientDataReceived?.Invoke(new ArraySegment<byte>(data), channel);
            client.OnReceivedError += (exception) => OnClientError?.Invoke(exception);

            Debug.Log("[FizzySteamyMirror] Init complete!");
        }

        // Client functions
        public override bool ClientConnected() { return client.Connected; }
        public override void ClientConnect(string address) { client.Connect(address); }
        public override bool ClientSend(int channelId, ArraySegment<byte> segment) { return client.Send(segment.Array, channelId); }
        public override void ClientDisconnect() { client.Disconnect(); }

        // Server functions
        public override bool ServerActive() { return server.Active; }
        public override void ServerStart()
        {
            server.Listen();
        }

        public virtual void ServerStartWebsockets(string address, int port, int maxConnections)
        {
            Debug.LogError("[FizzySteamyMirror] Starting websockets isn't permitted.");
        }

        public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment) { return server.Send(connectionIds, segment.Array, channelId); }

        public override bool ServerDisconnect(int connectionId)
        {
            return server.Disconnect(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId) { return server.ServerGetClientAddress(connectionId); }
        public override void ServerStop() { server.Stop(); }

        // Common functions
        public override void Shutdown()
        {
            client.Disconnect();
            server.Stop();
        }

        public override int GetMaxPacketSize(int channelId) {
            if (channelId >= channels.Length) {
                channelId = 0;
            }
            P2PSend sendMethod = channels[channelId];
            switch (sendMethod) {
                case P2PSend.Unreliable:
                    return 1200; // UDP like - MTU size.
                case P2PSend.UnreliableNoDelay:
                    return 1200; // UDP like - MTU size.
                case P2PSend.Reliable:
                    return 1048576; // Reliable channel. Can send up to 1MB of data in a single message.
                case P2PSend.ReliableWithBuffering:
                    return 1048576; // Reliable channel. Can send up to 1MB of data in a single message.
                default:
                    return 1200; // UDP like - MTU size.
            }
        }

        public override bool Available()
        {
            try
            {
                return Steamworks.SteamClient.IsValid;
            }
            catch
            {
                return false;
            }
        }
  }
}

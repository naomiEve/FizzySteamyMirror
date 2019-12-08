using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;

namespace Mirror.FizzySteam
{
    internal class SteamClient
    {
        public enum ConnectionState
        {
            CONNECTED,
            DISCONNECTING,
        }

        public SteamId steamID;
        public ConnectionState state;
        public int connectionID;
        public float timeIdle = 0;

        public SteamClient(ConnectionState state, SteamId steamID, int connectionID)
        {
            this.state = state;
            this.steamID = steamID;
            this.connectionID = connectionID;
            this.timeIdle = 0;
        }
    }
    internal class SteamConnectionMap : IEnumerable<KeyValuePair<int, SteamClient>>
    {
        public readonly Dictionary<SteamId, SteamClient> fromSteamID = new Dictionary<SteamId, SteamClient>();
        public readonly Dictionary<int, SteamClient> fromConnectionID = new Dictionary<int, SteamClient>();

        public SteamConnectionMap()
        {
        }

        public int Count
        {
            get { return fromSteamID.Count; }
        }

        public SteamClient Add(SteamId steamID, int connectionID, SteamClient.ConnectionState state)
        {
            var newClient = new SteamClient(state, steamID, connectionID);
            fromSteamID.Add(steamID, newClient);
            fromConnectionID.Add(connectionID, newClient);

            return newClient;
        }

        public void Remove(SteamClient steamClient)
        {
            fromSteamID.Remove(steamClient.steamID);
            fromConnectionID.Remove(steamClient.connectionID);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<int, SteamClient>> GetEnumerator()
        {
            return fromConnectionID.GetEnumerator();
        }
    }

    public class Server : Common
    {

        enum ConnectionState : byte
        {
            OFFLINE,
            LISTENING
        }

        public event Action<int> OnConnected;
        public event Action<int, byte[], int> OnReceivedData;
        public event Action<int> OnDisconnected;
        public event Action<int, Exception> OnReceivedError;

        private ConnectionState state = ConnectionState.OFFLINE;
        private SteamConnectionMap steamConnectionMap;
        private int nextConnectionID;
        private int maxConnections;

        public int steamAppId;

        public bool Listening { get { return state == ConnectionState.LISTENING; } private set { if (value) state = ConnectionState.LISTENING; } }
        public bool Offline
        {
            get { return state == ConnectionState.OFFLINE; }
            private set
            {
                if (value)
                {
                    state = ConnectionState.OFFLINE;
                    Shutdown();
                }
            }
        }
        public async void Listen(int maxConnections = int.MaxValue)
        {
            Debug.Log("[FizzySteamyMirror] Started listening");
            //todo check we are not already listening ?

            Init(steamAppId);
            Listening = true;
            this.maxConnections = maxConnections;

            InternalReceiveLoop();

            await ReceiveLoop();

            Debug.Log("[FizzySteamyMirror] Stopped listening");
        }

        protected override void OnNewConnectionInternal(SteamId id)
        {
            Debug.Log("[FizzySteamyMirror] OnNewConnectionInternal (Server)");

            SteamNetworking.AcceptP2PSessionWithUser(id);
        }

        //start a async loop checking for internal messages and processing them. This includes internal connect negotiation and disconnect requests so runs outside "connected"
        private async void InternalReceiveLoop()
        {
            Debug.Log("[FizzySteamyMirror] InternalReceiveLoop Start");

            uint readPacketSize;
            SteamId clientSteamID;

            try
            {
                while (!Offline)
                {
                    while (ReceiveInternal(out readPacketSize, out clientSteamID))
                    {
                        Debug.Log("[FizzySteamyMirror] InternalReceiveLoop - Received Data");
                        if (readPacketSize != 1)
                        {
                            continue;
                        }
                        Debug.Log("[FizzySteamyMirror] InternalReceiveLoop - received " + receiveBufferInternal[0]);
                        switch (receiveBufferInternal[0])
                        {
                            //requesting to connect to us
                            case (byte)InternalMessages.CONNECT:
                                if(steamConnectionMap.Count >= maxConnections)
                                {
                                    SendInternal(clientSteamID, disconnectMsgBuffer);
                                    continue;
                                    //too many connections, reject
                                }
                                SendInternal(clientSteamID, acceptConnectMsgBuffer);

                                int connectionId = nextConnectionID++;
                                steamConnectionMap.Add(clientSteamID, connectionId, SteamClient.ConnectionState.CONNECTED);
                                OnConnected?.Invoke(connectionId);
                                break;

                            //asking us to disconnect
                            case (byte)InternalMessages.DISCONNECT:
                                try
                                {
                                    SteamClient steamClient = steamConnectionMap.fromSteamID[clientSteamID];
                                    steamConnectionMap.Remove(steamClient);
                                    OnDisconnected?.Invoke(steamClient.connectionID);
                                    CloseP2PSessionWithUser(steamClient.steamID);
                                }
                                catch (KeyNotFoundException)
                                {
                                    //we have no idea who this connection is
                                    Debug.LogError("[FizzySteamyMirror] Trying to disconnect a client that we don't know " + clientSteamID);
                                    OnReceivedError?.Invoke(-1, new Exception("ERROR Unknown SteamID"));
                                }

                                break;
                        }
                    }

                    //not got a message - wait a bit more
                    await Task.Delay(TimeSpan.FromSeconds(secondsBetweenPolls));
                }
            }
            catch (ObjectDisposedException) { }

            Debug.Log("[FizzySteamyMirror] InternalReceiveLoop Stop");
        }

        private async Task ReceiveLoop()
        {
            Debug.Log("[FizzySteamyMirror] ReceiveLoop Start");

            uint readPacketSize;
            SteamId clientSteamID;

            try
            {
                byte[] receiveBuffer;
                while (!Offline)
                {
                    for (int i = 0; i < channels.Length; i++) {
                        while (Receive(out readPacketSize, out clientSteamID, out receiveBuffer, i)) {
                            if (readPacketSize == 0) {
                                continue;
                            }

                            try {
                                int connectionId = steamConnectionMap.fromSteamID[clientSteamID].connectionID;
                                // we received some data,  raise event
                                OnReceivedData?.Invoke(connectionId, receiveBuffer,i);
                            } catch (KeyNotFoundException) {
                                CloseP2PSessionWithUser(clientSteamID);
                                //we have no idea who this connection is
                                Debug.LogError("[FizzySteamyMirror] Data received from steam client that we don't know " + clientSteamID);
                                OnReceivedError?.Invoke(-1, new Exception("ERROR Unknown SteamID"));
                            }
                        }
                    }
                    //not got a message - wait a bit more
                    await Task.Delay(TimeSpan.FromSeconds(secondsBetweenPolls));
                }
            }
            catch (ObjectDisposedException) { }

            Debug.Log("[FizzySteamyMirror] ReceiveLoop Stop");
        }

        // check if the server is running
        public bool Active
        {
            get { return Listening; }
        }

        public void Stop()
        {
            Debug.LogWarning("[FizzySteamyMirror] Server Stop");
            if (!Active) return;

            Offline = true;

            Shutdown();
            Debug.Log("[FizzySteamyMirror] Server Stop Finished");
        }

        // disconnect (kick) a client
        public bool Disconnect(int connectionId)
        {
            try
            {
                SteamClient steamClient = steamConnectionMap.fromConnectionID[connectionId];
                Disconnect(steamClient);
                return true;
            }
            catch (KeyNotFoundException)
            {
                //we have no idea who this connection is
                Debug.LogWarning("[FizzySteamyMirror] Trying to disconnect somebody we don't know " + connectionId);
            }
            return false;
        }

        private async void Disconnect(SteamClient steamClient)
        {
            if(steamClient.state != SteamClient.ConnectionState.CONNECTED)
            {
                return;
            }

            SendInternal(steamClient.steamID, disconnectMsgBuffer);
            steamClient.state = SteamClient.ConnectionState.DISCONNECTING;

            //Wait a short time before calling steams disconnect function so the message has time to go out
            await Task.Delay(100);
            steamConnectionMap.Remove(steamClient);
            OnDisconnected?.Invoke(steamClient.connectionID);
            CloseP2PSessionWithUser(steamClient.steamID);
        }

        public bool Send(List<int> connectionIds, byte[] data, int channelId = 0) {
            for (int i = 0; i < connectionIds.Count; i++) {
                try {
                    SteamClient steamClient = steamConnectionMap.fromConnectionID[connectionIds[i]];
                    //will default to reliable at channel 0 if sent on an unknown channel
                    Send(steamClient.steamID, data, channelToSendType(channelId), channelId >= channels.Length ? 0 : channelId);
                } catch (KeyNotFoundException) {
                    //we have no idea who this connection is
                    Debug.LogError("[FizzySteamyMirror] Trying to send on a connection that we don't have " + connectionIds[i]);
                    OnReceivedError?.Invoke(connectionIds[i], new Exception("ERROR Unknown Connection"));
                }
            }
            return true;
        }

        public string ServerGetClientAddress(int connectionId)
        {
            try
            {
                SteamClient steamClient = steamConnectionMap.fromConnectionID[connectionId];
                return steamClient.steamID.ToString();
            }
            catch (KeyNotFoundException)
            {
                //we have no idea who this connection is
                Debug.LogError("[FizzySteamyMirror] Trying to get info of an unknown connection " + connectionId);
                OnReceivedError?.Invoke(connectionId, new Exception("ERROR Unknown Connection"));
            }

            return null;
        }

        protected override void Init(int appid)
        {
            base.Init(appid);

            nextConnectionID = 1;
            steamConnectionMap = new SteamConnectionMap();
        }
    }
}

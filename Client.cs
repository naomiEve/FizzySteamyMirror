using UnityEngine;
using System;
using Steamworks;
using System.Threading.Tasks;

namespace Mirror.FizzySteam
{
    public class Client : Common
    {
        enum ConnectionState : byte {
            DISCONNECTED,
            CONNECTING,
            CONNECTED
        }

        public event Action<Exception> OnReceivedError;
        public event Action<byte[], int> OnReceivedData;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public static int clientConnectTimeoutMS = 25000;

        private ConnectionState state = ConnectionState.DISCONNECTED;
        private SteamId hostSteamID = new SteamId();
        public int steamAppId;

        public bool Connecting { get { return state == ConnectionState.CONNECTING; } private set { if( value ) state = ConnectionState.CONNECTING; } }
        public bool Connected {
            get { return state == ConnectionState.CONNECTED; }
            private set {
                if (value)
                {
                    bool wasConnecting = Connecting;
                    state = ConnectionState.CONNECTED;
                    if (wasConnecting)
                    {
                        OnConnected?.Invoke();
                    }

                }
            }
        }
        public bool Disconnected {
            get { return state == ConnectionState.DISCONNECTED; }
            private set {
                if (value)
                {
                    bool wasntDisconnected = !Disconnected;
                    state = ConnectionState.DISCONNECTED;
                    if (wasntDisconnected)
                    {
                        OnDisconnected?.Invoke();
                    }

                    Shutdown();
                }
            }
        }

        // Used internally while connecting. Subscribes to OnConnect and signals this task
        TaskCompletionSource<Task> connectedComplete;
        private void SetConnectedComplete()
        {
            connectedComplete.SetResult(connectedComplete.Task);
        }

        System.Threading.CancellationTokenSource cancelToken;
        public async void Connect(string host)
        {
            cancelToken = new System.Threading.CancellationTokenSource();
            // Don't start if we're connected somewhere else
            if (!Disconnected)
            {
                Debug.LogError("[FizzySteamyMirror] Client already connected or connecting");
                OnReceivedError?.Invoke(new Exception("Client already connected"));
                return;
            }

            Connecting = true;

            Init(steamAppId);

            try
            {
                hostSteamID = Convert.ToUInt64(host);

                InternalReceiveLoop();

                connectedComplete = new TaskCompletionSource<Task>();
                
                OnConnected += SetConnectedComplete;
                CloseP2PSessionWithUser(hostSteamID);

                // Send a connect message to the steam client
                SendInternal(hostSteamID, connectMsgBuffer);

                Task connectedCompleteTask = connectedComplete.Task;

                if (await Task.WhenAny(connectedCompleteTask, Task.Delay(clientConnectTimeoutMS, cancelToken.Token)) != connectedCompleteTask)
                {
                    // Timed out while waiting for connection to complete
                    OnConnected -= SetConnectedComplete;

                    Exception e = new Exception("Timed out while connecting");
                    OnReceivedError?.Invoke(e);
                    throw e;
                }

                OnConnected -= SetConnectedComplete;

                await ReceiveLoop();
            }
            catch (FormatException)
            {
                Debug.LogError("[FizzySteamyMirror] Failed to connect - Error while parsing the Steam ID");
                OnReceivedError?.Invoke(new Exception("ERROR passing steam ID address"));
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError("[FizzySteamyMirror] Failed to connect -> " + ex);
                OnReceivedError?.Invoke(ex);
            }
            finally
            {
                Disconnect();
            }

        }

        public async void Disconnect()
        {
            if (!Disconnected)
            {
                SendInternal(hostSteamID, disconnectMsgBuffer);
                Disconnected = true;
                cancelToken.Cancel();

                // Wait before closing connection with Steam
                await Task.Delay(100);
                CloseP2PSessionWithUser(hostSteamID);
            }
        }

        private async Task ReceiveLoop()
        {
            Debug.Log("[FizzySteamyMirror] ReceiveLoop Start");

            uint readPacketSize;
            SteamId clientSteamID;

            try
            {
                byte[] receiveBuffer;

                while (Connected)
                {
                    for (int i = 0; i < channels.Length; i++) {
                        while (Receive(out readPacketSize, out clientSteamID, out receiveBuffer, i)) {
                            if (readPacketSize == 0) {
                                continue;
                            }
                            if (clientSteamID != hostSteamID) {
                                Debug.LogError("[FizzySteamyMirror] Received a message from an unknown steam user");
                                continue;
                            }
                            // Received data, invoke the proper event
                            OnReceivedData?.Invoke(receiveBuffer, i);
                        }
                    }
                    // No messages, delay until next poll
                    await Task.Delay(TimeSpan.FromSeconds(secondsBetweenPolls));
                }
            }
            catch (ObjectDisposedException) { }

            Debug.Log("[FizzySteamyMirror] ReceiveLoop Stop");
        }

        protected override void OnNewConnectionInternal(SteamId id)
        {
            Debug.Log("[FizzySteamyMirror] OnNewConnectionInternal (Client)");

            if (hostSteamID == id)
            {
                SteamNetworking.AcceptP2PSessionWithUser(id);
            } else
            {
                Debug.LogError("");
            }
        }

        // Starts an async loop checking for internal messages
        private async void InternalReceiveLoop()
        {
            Debug.Log("[FizzySteamyMirror] InternalReceiveLoop Start");

            uint readPacketSize;
            SteamId clientSteamID;

            try
            {
                while (!Disconnected)
                {
                    while (ReceiveInternal(out readPacketSize, out clientSteamID))
                    {
                        if (readPacketSize != 1)
                            continue;
                        if (clientSteamID != hostSteamID)
                        {
                            Debug.LogError("[FizzySteamyMirror] Received an internal message from an unknown steam user");
                            continue;
                        }
                        switch (receiveBufferInternal[0])
                        {
                            case (byte)InternalMessages.ACCEPT_CONNECT:
                                Connected = true;
                                break;
                            case (byte)InternalMessages.DISCONNECT:
                                Disconnected = true;
                                break;
                        }
                    }
                    // No messages, delay until the next poll
                    await Task.Delay(TimeSpan.FromSeconds(secondsBetweenPolls));
                }
            }
            catch (ObjectDisposedException) { }

            Debug.Log("[FizzySteamyMirror] InternalReceiveLoop Stop");
        }

        // Send the data or throw an exception
        public bool Send(byte[] data, int channelId)
        {
            if (Connected)
            {
                Send(hostSteamID, data, channelToSendType(channelId), channelId);
                return true;
            }
            else
            {
                throw new Exception("Not Connected");
            }
        }

    }
}

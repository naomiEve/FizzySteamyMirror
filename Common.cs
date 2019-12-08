using UnityEngine;
using System;
using Steamworks;

namespace Mirror.FizzySteam
{
    public class Common
    {
        protected enum SteamChannels : int
        {
            SEND_DATA,
            SEND_INTERNAL = 100
        }

        protected enum InternalMessages : byte
        {
            CONNECT,
            ACCEPT_CONNECT,
            DISCONNECT
        }

        public static float secondsBetweenPolls = 0.03333f;

        readonly static protected byte[] connectMsgBuffer = new byte[] { (byte)InternalMessages.CONNECT };
        readonly static protected byte[] acceptConnectMsgBuffer = new byte[] { (byte)InternalMessages.ACCEPT_CONNECT };
        readonly static protected byte[] disconnectMsgBuffer = new byte[] { (byte)InternalMessages.DISCONNECT };
        public static P2PSend[] channels;

        readonly static protected uint maxPacketSize = 1048576;
        protected byte[] receiveBufferInternal = new byte[1];


        protected void Shutdown()
        {
            SteamNetworking.OnP2PSessionRequest -= OnNewConnection;
            SteamNetworking.OnP2PConnectionFailed -= OnConnectFail;
        }

        protected virtual void Init(int appid)
        {
            Debug.Log("[FizzySteamyMirror] Initializing");
            
            // Init the steam client if it hasn't been initialized before. Shouldn't usually happen, since games usually initialize Steamworks on launch.
            try
            {
                if (!Steamworks.SteamClient.IsValid)
                    Steamworks.SteamClient.Init((uint)appid);
            } catch(Exception e)
            {
                Debug.Log(e.Message);
            }

            if (Steamworks.SteamClient.IsValid)
            {
                SteamNetworking.OnP2PSessionRequest += OnNewConnection;
                SteamNetworking.OnP2PConnectionFailed += OnConnectFail;
            }
            else
            {
                Debug.LogError("[FizzySteamyMirror] Steam not initialized!");
                return;
            }
        }

        protected void OnNewConnection(SteamId id)
        {
            Debug.Log("[FizzySteamyMirror] OnNewConnection");
            OnNewConnectionInternal(id);
        }

        protected virtual void OnNewConnectionInternal(SteamId id) {}

        protected virtual void OnConnectFail(SteamId id, P2PSessionError error)
        {
            Debug.Log("[FizzySteamyMirror] OnConnectFail -> " + error);
            throw new Exception("Failed to connect");
        }

        protected void SendInternal(SteamId host, byte[] msgBuffer)
        {
            if (!Steamworks.SteamClient.IsValid)
            {
                throw new ObjectDisposedException("Steamworks");
            }
            SteamNetworking.SendP2PPacket(host, msgBuffer, (int)msgBuffer.Length, (int)SteamChannels.SEND_INTERNAL, P2PSend.Reliable);
        }

        protected bool ReceiveInternal(out uint readPacketSize, out SteamId clientSteamID)
        {
            if (!Steamworks.SteamClient.IsValid)
            {
                throw new ObjectDisposedException("Steamworks");
            }

            Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket((int)SteamChannels.SEND_INTERNAL);
            if (packet.HasValue)
            {
                receiveBufferInternal = packet.Value.Data;
                clientSteamID = packet.Value.SteamId;
                readPacketSize = (uint)packet.Value.Data.Length;
                return true;
            }

            clientSteamID = new SteamId();
            readPacketSize = 0;
            return false;
        }

        protected void Send(SteamId host, byte[] msgBuffer, P2PSend sendType, int channel)
        {
            if (!Steamworks.SteamClient.IsValid)
            {
                throw new ObjectDisposedException("Steamworks");
            }

            if (channel >= channels.Length) {
                channel = 0;
            }

            SteamNetworking.SendP2PPacket(host, msgBuffer, msgBuffer.Length, channel, sendType);
        }

        protected bool Receive(out uint readPacketSize, out SteamId clientSteamID, out byte[] receiveBuffer, int channel)
        {
            if (!Steamworks.SteamClient.IsValid)
            {
                throw new ObjectDisposedException("Steamworks");
            }

            if (SteamNetworking.IsP2PPacketAvailable(channel))
            {
                Steamworks.Data.P2Packet? packet = SteamNetworking.ReadP2PPacket(channel);
                if (packet.HasValue)
                {
                    clientSteamID = packet.Value.SteamId;
                    receiveBuffer = packet.Value.Data;
                    readPacketSize = (uint)packet.Value.Data.Length;
                    return true;
                }
            }

            receiveBuffer = null;
            readPacketSize = 0;
            clientSteamID = new SteamId();
            return false;
        }

        protected void CloseP2PSessionWithUser(SteamId clientSteamID)
        {
            if (!Steamworks.SteamClient.IsValid)
            {
                throw new ObjectDisposedException("Steamworks");
            }
            SteamNetworking.CloseP2PSessionWithUser(clientSteamID);
        }

        public uint GetMaxPacketSize(P2PSend sendType)
        {
            switch (sendType)
            {
                case P2PSend.Unreliable:
                case P2PSend.UnreliableNoDelay:
                    return 1200; // UDP like - MTU size.

                case P2PSend.Reliable:
                case P2PSend.ReliableWithBuffering:
                    return maxPacketSize; // Reliable channel. Can send up to 1MB of data in a single message.

                default:
                    Debug.LogError("[FizzySteamyMirror] Cannot get the packet size of an unknown type");
                    return 0;
            }

        }

        protected P2PSend channelToSendType(int channelId)
        {
            if (channelId >= channels.Length) {
                Debug.LogError("[FizzySteamyMirror] Unknown channel id, please set it up in the component, will now send via the reliable channel");
                return P2PSend.Reliable;
            }
            return channels[channelId];
        }

    }
}

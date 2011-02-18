using System;
using System.Collections.Generic;
using System.Net;

namespace UDPX
{
    /// <summary>
    /// Represents a client, which handles sending and receiving packets to and from a specific IP end point.
    /// </summary>
    public interface IUDPXClient
    {
        /// <summary>
        /// Sends data to the target of this client. Packets sent with this method are protected from packet loss
        /// and corruption.
        /// </summary>
        void Send(byte[] Data);

        /// <summary>
        /// Sends data to the target of this client. These packets have slightly less overhead than checked packets, but may
        /// be lost or corrupt.
        /// </summary>
        void SendUnchecked(byte[] Data);

        /// <summary>
        /// Stops sending and receiving data from the IP end point.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Gets (or sets, in case of a relocation) the IP end point packets are sent to and received from.
        /// </summary>
        IPEndPoint EndPoint { get; set; }

        /// <summary>
        /// Called when a packet is received for this client.
        /// </summary>
        event ReceivePacketHandler ReceivePacket;
    }

    /// <summary>
    /// Called when a packet is received. The data for the packet is supplied.
    /// </summary>
    public delegate void ReceivePacketHandler(byte[] Data);

    /// <summary>
    /// Called when a packet from a previously unknown IP end point is received. The given client can be used to
    /// respond and listen to the newly discovered IP end point.
    /// </summary>
    public delegate void ConnectHandler(IUDPXClient Client);
    
    /// <summary>
    /// Contains methods related to UDPX.
    /// </summary>
    public static class UDPX
    {
        /// <summary>
        /// Begins listening for connection on the specified port.
        /// </summary>
        public static void BeginListen(int Port, ConnectHandler OnConnect)
        {

        }

        /// <summary>
        /// Stops listening for new connections on the specified port. Connections that are currently active
        /// will still remain.
        /// </summary>
        public static void EndListen(int Port)
        {

        }
    }
}
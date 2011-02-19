using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Represents a client, which handles sending and receiving packets to and from a specific IP end point.
/// </summary>
public interface IUDPXClient : IDisposable
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
    /// Gets (or sets, in case of a relocation) the IP end point packets are sent to and received from.
    /// </summary>
    IPEndPoint EndPoint { get; set; }

    /// <summary>
    /// Called when a packet is received for this client. Using this method, packets will not be received in the order
    /// they are sent.
    /// </summary>
    event ReceivePacketHandler ReceivePacket;

    /// <summary>
    /// Called when a packet is received for this client and all previous packets have been. The packets will
    /// be received in their original order.
    /// </summary>
    event ReceivePacketHandler ReceivePacketOrdered;
}

/// <summary>
/// Called when a packet is received. The data for the packet is supplied.
/// </summary>
public delegate void ReceivePacketHandler(byte[] Data);

/// <summary>
/// Called when a UDPX client is connected.
/// </summary>
public delegate void ConnectHandler(IUDPXClient Client);

/// <summary>
/// Contains methods related to UDPX.
/// </summary>
public static class UDPX
{
    /// <summary>
    /// Begins listening for new connections on the specified port. Note that the OnConnect method will
    /// only be called for new connections from previously unknown IPEndPoint's. If stored, the returned listener
    /// can eventually be used to end listening.
    /// </summary>
    public static Listener BeginListen(int Port, ConnectHandler OnConnect)
    {
        return new Listener(Port, OnConnect);
    }

    /// <summary>
    /// Tries, asynchronously, to connect a UDPX client to the given endpoint. The connect handler will be called with
    /// null if no connection can be established.
    /// </summary>
    public static void Connect(IPEndPoint EndPoint, ConnectHandler OnConnect)
    {
        UdpClient cli = new UdpClient();
        cli.Send(new byte[300], 300, EndPoint);
    }

    /// <summary>
    /// Listens for an incoming connection on a port.
    /// </summary>
    public class Listener : IDisposable
    {
        public Listener(int Port, ConnectHandler OnConnect)
        {
            this._Client = new UdpClient(Port);
            this._OnConnect = OnConnect;
            this._BeginListen();
        }

        /// <summary>
        /// Gets the maximum size packet data can be.
        /// </summary>
        public const int MaxPacketSize = 65536 - _PacketHeaderSize;

        /// <summary>
        /// Gets the size a header for a normal packet.
        /// </summary>
        private const int _PacketHeaderSize = 1 + 4 + 4;

        /// <summary>
        /// Forces listening for incoming connections to stop on this listener.
        /// </summary>
        public void End()
        {
            this._Client.Close();
            this._Client = null;
        }

        public void Dispose()
        {
            this.End();
        }

        private void _BeginListen()
        {
            this._Client.BeginReceive(this._ReceiveCallback, null);
        }

        private void _ReceiveCallback(IAsyncResult AR)
        {
            IPEndPoint end = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = this._Client.EndReceive(AR, ref end);
            this._BeginListen();
        }

        private ConnectHandler _OnConnect;
        private UdpClient _Client;
    }

    /// <summary>
    /// Gets the type of a packet from its data.
    /// </summary>
    private static PacketType _GetType(byte[] Data)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Indicates the type of a packet.
    /// </summary>
    public enum PacketType
    {
        Sequenced,
        Unsequenced,
        Request,
        Handshake
    }
}
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Timers;

/// <summary>
/// Represents a connection, which handles sending and receiving packets to and from a specific IP end point.
/// </summary>
public interface IUDPXConnection : IDisposable
{
    /// <summary>
    /// Sends data to the target of this connection. Packets sent with this method are protected from packet loss
    /// and corruption.
    /// </summary>
    void Send(byte[] Data);

    /// <summary>
    /// Sends data to the target of this connection. These packets have slightly less overhead than checked packets, but may
    /// be lost or corrupt.
    /// </summary>
    void SendUnchecked(byte[] Data);

    /// <summary>
    /// Gets the IP end point packets are sent to and received from.
    /// </summary>
    IPEndPoint EndPoint { get; }

    /// <summary>
    /// Called when a packet is received for this connection. Using this method, packets will not be received in the order
    /// they are sent.
    /// </summary>
    event ReceivePacketHandler ReceivePacket;

    /// <summary>
    /// Called when a packet is received for this connection and all previous packets have been. The packets will
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
public delegate void ConnectHandler(IUDPXConnection Client);

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
    public static Listener Listen(int Port, ConnectHandler OnConnect)
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
        byte[] handshake = new byte[] { (byte)PacketType.Handshake };

        Send(cli, EndPoint, handshake);

        ReceiveRawPacketHandler receivepacket = null;
        Receive(cli, receivepacket = delegate(IPEndPoint From, byte[] Data)
        {
            if (OnConnect != null)
            {
                if (From.Equals(EndPoint) && Data.Length == 1 && Data[0] == (byte)PacketType.HandshakeAck)
                {
                    OnConnect(new _ClientConnection(cli, EndPoint));
                    OnConnect = null;
                }
                else
                {
                    // Not the packet we were looking for, try again...
                    Receive(cli, receivepacket);
                }
            }
        });
    }

    /// <summary>
    /// Sends a packet with the given end point.
    /// </summary>
    public static void Send(UdpClient Client, IPEndPoint To, byte[] Data)
    {
        while (true)
        {
            try
            {
                Client.Send(Data, Data.Length, To);
                return;
            }
            catch (SocketException se)
            {
                if (!_CanIgnore(se))
                {
                    throw se;
                }
            }
        }
    }

    /// <summary>
    /// Sends a single packet to the specified end point.
    /// </summary>
    public static void Send(IPEndPoint To, byte[] Data)
    {
        using (UdpClient cli = new UdpClient())
        {
            Send(cli, To, Data);
        }
    }

    /// <summary>
    /// Asynchronously listens on the given client for a single packet.
    /// </summary>
    public static void Receive(UdpClient Client, ReceiveRawPacketHandler OnReceive)
    {
        // Good job Microsoft, for making this so easy O_O
        while (true)
        {
            try
            {
                Client.BeginReceive(delegate(IAsyncResult ar)
                {
                    IPEndPoint end = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data;
                    while (true)
                    {
                        try
                        {
                            data = Client.EndReceive(ar, ref end);
                            break;
                        }
                        catch (SocketException se)
                        {
                            if (!_CanIgnore(se))
                            {
                                throw se;
                            }
                        }
                    }
                    OnReceive(end, data);
                }, null);
                return;
            }
            catch (SocketException se)
            {
                if (!_CanIgnore(se))
                {
                    throw se;
                }
            }
        }
    }

    /// <summary>
    /// Asynchronously listens on the given port for a single packet.
    /// </summary>
    public static void Receive(int Port, ReceiveRawPacketHandler OnReceive)
    {
        using (UdpClient cli = new UdpClient(Port))
        {
            Receive(cli, OnReceive);
        }
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
    /// Listens for an incoming connection on a port.
    /// </summary>
    public class Listener : IDisposable
    {
        public Listener(int Port, ConnectHandler OnConnect)
        {
            this._Client = new UdpClient(Port);
            this._Connections = new Dictionary<IPEndPoint, _LConnection>();
            this._OnConnect = OnConnect;
            this._BeginListen();
        }

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

        /// <summary>
        /// Disconnects the connection with the specified endpoint.
        /// </summary>
        internal void _Disconnect(IPEndPoint EndPoint)
        {
            this._Connections.Remove(EndPoint);
        }

        private void _BeginListen()
        {
            Receive(this._Client, _ReceiveCallback);
        }

        private void _ReceiveCallback(IPEndPoint From, byte[] Data)
        {
            _LConnection conn;
            if (this._Connections.TryGetValue(From, out conn))
            {
                conn.Receive(Data);
            }
            else
            {
                if (Data.Length == 1 && Data[0] == (byte)PacketType.Handshake)
                {
                    Send(this._Client, From, new byte[] { (byte)PacketType.HandshakeAck });
                    _LConnection connection = new _LConnection(From, this, this._Client);
                    this._Connections[From] = connection;
                    this._OnConnect(connection);
                }
            }
            this._BeginListen();
        }

        
        private class _LConnection : _Connection
        {
            public _LConnection(IPEndPoint EndPoint, Listener Listener, UdpClient SendClient)
            {
                this._EndPoint = EndPoint;
                this._SendClient = SendClient;
                this._Listener = Listener;
            }

            public override IPEndPoint EndPoint
            {
                get
                {
                    return this._EndPoint;
                }
            }

            public override void SendUnchecked(byte[] Data)
            {
                UDPX.Send(this._SendClient, this._EndPoint, Data);
            }

            public override void Disconnect()
            {
                this._Listener._Disconnect(this._EndPoint);
            }

            private IPEndPoint _EndPoint;
            private Listener _Listener;
            private UdpClient _SendClient;
        }

        private Dictionary<IPEndPoint, _LConnection> _Connections;
        private ConnectHandler _OnConnect;
        private UdpClient _Client;
    }

    /// <summary>
    /// Gets if the specified exception can safely be ignored.
    /// </summary>
    private static bool _CanIgnore(SocketException Exception)
    {
        return Exception.SocketErrorCode == SocketError.ConnectionReset;
    }

    /// <summary>
    /// An abstract connection that handles packets.
    /// </summary>
    private abstract class _Connection : IUDPXConnection
    {
        /// <summary>
        /// Called when this connection is disconnected.
        /// </summary>
        public virtual void Disconnect()
        {

        }

        public void Dispose()
        {
            this.Disconnect();
        }

        public void Send(byte[] Data)
        {
            this.SendUnchecked(Data);
        }

        /// <summary>
        /// Called when this connection receives a raw packet.
        /// </summary>
        public void Receive(byte[] Data)
        {
            this.ReceivePacket(Data);
        }

        public abstract void SendUnchecked(byte[] Data);

        public abstract IPEndPoint EndPoint { get; }

        public event ReceivePacketHandler ReceivePacket;
        public event ReceivePacketHandler ReceivePacketOrdered;

        private int _ReceiveSequence;
        private int _SendSequence;
    }

    /// <summary>
    /// Connection for a client.
    /// </summary>
    private class _ClientConnection : _Connection
    {
        public _ClientConnection(UdpClient Client, IPEndPoint EndPoint)
        {
            this._Client = Client;
            this._EndPoint = EndPoint;
            this._BeginListen();
        }

        public override void SendUnchecked(byte[] Data)
        {
            UDPX.Send(this._Client, this._EndPoint, Data);
        }

        public override IPEndPoint EndPoint
        {
            get
            {
                return this._EndPoint;
            }
        }

        private void _BeginListen()
        {
            UDPX.Receive(this._Client, delegate(IPEndPoint From, byte[] Data)
            {
                if (From.Equals(this._EndPoint))
                {
                    this.Receive(Data);
                }
                this._BeginListen();
            });
        }

        private UdpClient _Client;
        private IPEndPoint _EndPoint;
    }

    /// <summary>
    /// Called when a raw (unprocessed) packet is received.
    /// </summary>
    public delegate void ReceiveRawPacketHandler(IPEndPoint From, byte[] Data);

    /// <summary>
    /// Indicates the type of a packet.
    /// </summary>
    public enum PacketType : byte
    {
        Sequenced,
        Unsequenced,
        Request,
        Handshake,
        HandshakeAck
    }
}
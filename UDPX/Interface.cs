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

        int attempts = 5;
        double attemptinterval = 0.5;
        double timeout = 1.0;

        // Set up request timer
        Timer timer = new Timer(attemptinterval * 1000.0);
        timer.Elapsed += delegate
        {
            lock (cli)
            {
                if (OnConnect != null)
                {
                    if (attempts > 0)
                    {
                        Send(cli, EndPoint, handshake);
                        attempts--;
                    }
                    else
                    {
                        timer.Dispose();
                        timer = new Timer(timeout * 1000.0);
                        timer.AutoReset = false;
                        timer.Elapsed += delegate
                        {
                            lock (cli)
                            {
                                if (OnConnect != null)
                                {
                                    // Timeout
                                    OnConnect(null);
                                    OnConnect = null;
                                    ((IDisposable)cli).Dispose();
                                }
                                timer.Dispose();
                            }
                        };
                        timer.Start();
                    }
                }
                else
                {
                    timer.Dispose();
                }
            }
        };
        timer.AutoReset = true;

        // Send initial attempt to open receiving port.
        Send(cli, EndPoint, handshake);
        attempts--;

        // Create receive callback
        ReceiveRawPacketHandler receivepacket = null;
        List<byte[]> queue = new List<byte[]>();
        Receive(cli, receivepacket = delegate(IPEndPoint From, byte[] Data)
        {
            lock (cli)
            {
                if (OnConnect != null)
                {
                    if (From.Equals(EndPoint))
                    {
                        if (Data.Length == 1 && Data[0] == (byte)PacketType.HandshakeAck)
                        {
                            _ClientConnection cc = new _ClientConnection(cli, EndPoint);
                            OnConnect(cc);

                            // Give the new connection the messages intended for it
                            foreach (byte[] qdata in queue)
                            {
                                cc.ReceiveRaw(qdata);
                            }

                            OnConnect = null;
                        }
                        else
                        {
                            // Add data to packet queue and try again
                            queue.Add(Data);
                            Receive(cli, receivepacket);
                        }
                    }
                    else
                    {
                        // Not the packet we were looking for, try again...
                        Receive(cli, receivepacket);
                    }
                }
            }
        });

        // Begin sending more attempts
        timer.Start();
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
            catch (ObjectDisposedException)
            {
                return;
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
                    lock (Client)
                    {
                        IPEndPoint end = new IPEndPoint(IPAddress.Any, 0);
                        byte[] data;
                        try
                        {
                            data = Client.EndReceive(ar, ref end);
                            OnReceive(end, data);
                        }
                        catch (SocketException se)
                        {
                            if (se.SocketErrorCode == SocketError.Shutdown)
                            {
                                return;
                            }
                            if (_CanIgnore(se))
                            {
                                Receive(Client, OnReceive);
                            }
                            else
                            {
                                throw se;
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            return;
                        }
                    }
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
            catch (ObjectDisposedException)
            {
                return;
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
                conn.ReceiveRaw(Data);
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

            public override void SendRaw(byte[] Data)
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
        public _Connection()
        {
            this._Received = new Dictionary<int, byte[]>();
            this._Sent = new Dictionary<int, byte[]>();
        }

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
            this._SendWithSequence(this._SendSequence, Data);
            this._Sent[this._SendSequence] = Data;
            this._SendSequence++;
        }

        /// <summary>
        /// Sends a sequenced packet with the specified sequence number.
        /// </summary>
        private void _SendWithSequence(int Sequence, byte[] Data)
        {
            int rc = IPAddress.HostToNetworkOrder(this._ReceiveSequence);
            int sc = IPAddress.HostToNetworkOrder(Sequence);
            byte[] pdata = new byte[Data.Length + _PacketHeaderSize];
            pdata[0] = (byte)PacketType.Sequenced;
            pdata[1] = (byte)sc;
            pdata[2] = (byte)(sc >> 8);
            pdata[3] = (byte)(sc >> 16);
            pdata[4] = (byte)(sc >> 24);
            pdata[5] = (byte)rc;
            pdata[6] = (byte)(rc >> 8);
            pdata[7] = (byte)(rc >> 16);
            pdata[8] = (byte)(rc >> 24);
            for (int t = 0; t < Data.Length; t++)
            {
                pdata[t + _PacketHeaderSize] = Data[t];
            }
            this.SendRaw(pdata);
        }

        /// <summary>
        /// Sends a request for the packet with the specified sequence number.
        /// </summary>
        private void _SendRequest(int Sequence)
        {
            int sc = IPAddress.HostToNetworkOrder(Sequence);
            byte[] pdata = new byte[5];
            pdata[0] = (byte)PacketType.Request;
            pdata[1] = (byte)sc;
            pdata[2] = (byte)(sc >> 8);
            pdata[3] = (byte)(sc >> 16);
            pdata[4] = (byte)(sc >> 24);
            this.SendRaw(pdata);
        }

        public void SendUnchecked(byte[] Data)
        {
            byte[] pdata = new byte[Data.Length + 1];
            pdata[0] = (byte)PacketType.Unsequenced;
            for (int t = 0; t < Data.Length; t++)
            {
                pdata[t + 1] = Data[t];
            }
            this.SendRaw(pdata);
        }

        /// <summary>
        /// Sends a raw packet.
        /// </summary>
        public abstract void SendRaw(byte[] Data);

        /// <summary>
        /// Called when this connection receives a raw packet.
        /// </summary>
        public void ReceiveRaw(byte[] Data)
        {
            // Get packet type
            byte[] pdata;
            PacketType type = (PacketType)Data[0];
            switch (type)
            {
                case PacketType.Handshake:
                    this._ReceiveSequence = 0;
                    this.SendRaw(new byte[] { (byte)PacketType.HandshakeAck });
                    break;
                case PacketType.HandshakeAck:
                    break;
                case PacketType.Unsequenced:
                    pdata = new byte[Data.Length - 1];
                    for (int t = 0; t < pdata.Length; t++)
                    {
                        pdata[t] = Data[t + 1];
                    }
                    if (this.ReceivePacket != null)
                    {
                        this.ReceivePacket(pdata);
                    }
                    break;
                case PacketType.Sequenced:
                    // Get actual packet data
                    pdata = new byte[Data.Length - _PacketHeaderSize];
                    for (int t = 0; t < pdata.Length; t++)
                    {
                        pdata[t] = Data[t + _PacketHeaderSize];
                    }

                    // Decode sequence and receive numbers
                    int sc = (int)Data[1] + ((int)Data[2] << 8) + ((int)Data[3] << 16) + ((int)Data[4] << 24);
                    int rc = (int)Data[5] + ((int)Data[6] << 8) + ((int)Data[7] << 16) + ((int)Data[8] << 24);
                    sc = IPAddress.NetworkToHostOrder(sc);
                    rc = IPAddress.NetworkToHostOrder(rc);

                    // Remove all sent items before the receive number (they should not need to be requested)
                    while (this._Sent.Remove(--rc)) ;

                    // See if this packet is actually needed
                    if (sc >= this._ReceiveSequence && !this._Received.ContainsKey(sc))
                    {
                        if (sc > this._LastReceiveSequence)
                        {
                            this._LastReceiveSequence = sc;
                        }

                        // Give receive callback
                        if (this.ReceivePacket != null)
                        {
                            this.ReceivePacket(pdata);
                        }

                        
                        if (sc == this._ReceiveSequence)
                        {
                            // Give ordered receive packet callback (and update receive numbers).
                            while (true)
                            {
                                this._ReceiveSequence++;
                                sc++;
                                if (this.ReceivePacketOrdered != null)
                                {
                                    this.ReceivePacketOrdered(pdata);
                                }

                                if (this._Received.TryGetValue(sc, out pdata))
                                {
                                    this._Received.Remove(sc);
                                }
                                else
                                {
                                    // Don't have the next packet,
                                    // have to stop here.
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Store the data (if needed).
                            if (this.ReceivePacketOrdered != null)
                            {
                                this._Received[sc] = pdata;
                            }
                            else
                            {
                                this._Received[sc] = null;
                            }
                        }

                        // Request all previous packets we need
                        for (int i = this._ReceiveSequence; i < this._LastReceiveSequence; i++)
                        {
                            if (!this._Received.ContainsKey(i))
                            {
                                this._SendRequest(i);
                            }
                        }
                    }
                    
                    break;
                case PacketType.Request:
                    sc = (int)Data[1] + ((int)Data[2] << 8) + ((int)Data[3] << 16) + ((int)Data[4] << 24);
                    sc = IPAddress.NetworkToHostOrder(sc);

                    // Send out requested packet
                    byte[] tosend;
                    if (this._Sent.TryGetValue(sc, out tosend))
                    {
                        this._SendWithSequence(sc, tosend);
                    }
                    break;
            }
        }

        public abstract IPEndPoint EndPoint { get; }

        public event ReceivePacketHandler ReceivePacket;
        public event ReceivePacketHandler ReceivePacketOrdered;

        /// <summary>
        /// Data for packets on or after _ReceiveSequence.
        /// </summary>
        private Dictionary<int, byte[]> _Received;

        /// <summary>
        /// The sequence number of the last packet received.
        /// </summary>
        private int _LastReceiveSequence;

        /// <summary>
        /// The highest sequence number for which all previous packets are accounted for. This is also the next packet
        /// that ReceivePacketOrdered needs.
        /// </summary>
        private int _ReceiveSequence;

        /// <summary>
        /// Data for recently sent packets.
        /// </summary>
        private Dictionary<int, byte[]> _Sent;

        /// <summary>
        /// The next sequence number to use for sending.
        /// </summary>
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

        public override void Disconnect()
        {
            ((IDisposable)this._Client).Dispose();
        }

        public override void SendRaw(byte[] Data)
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
                    this.ReceiveRaw(Data);
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
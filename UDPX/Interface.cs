﻿using System;
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
    /// Gets or sets after how many seconds of connection inactivity a keep alive packet should be sent. Keep alive packets insure
    /// that the connection remains open and also help detect missing packets. If this is set to null, no keep alive packets
    /// are sent.
    /// </summary>
    double? KeepAlive { get; set; }

    /// <summary>
    /// Gets or sets after how many seconds of connection inactivity, the connection should be regarded as disconnected implicitly.
    /// </summary>
    double? Timeout { get; set; }

    /// <summary>
    /// Forces the connection to disconnect explicitly.
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Called if this connection is disconnected externally.
    /// </summary>
    event DisconnectHandler Disconnected;

    /// <summary>
    /// Called when a packet is received for this connection. Using this method, packets will not be received in the order
    /// they are sent.
    /// </summary>
    event ReceivePacketHandler ReceivedPacket;

    /// <summary>
    /// Called when a packet is received for this connection and all previous packets have been. The packets will
    /// be received in their original order.
    /// </summary>
    event ReceivePacketHandler ReceivedPacketOrdered;
}

/// <summary>
/// Called when a packet is received. The data for the packet is supplied.
/// </summary>
/// <param name="Checked">True if the received packet was checked.</param>
public delegate void ReceivePacketHandler(bool Checked, byte[] Data);

/// <summary>
/// Called when a UDPX connection is made.
/// </summary>
public delegate void ConnectHandler(IUDPXConnection Client);

/// <summary>
/// Called when a UDPX connection is broken.
/// </summary>
/// <param name="Timeout">True if this disconnect is explicitly made for the connection, false otherwise (including timeouts).</param>
public delegate void DisconnectHandler(bool Explicit);

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
        // Create sequence number
        int seq = _CreateInitialSequence();
        byte[] handshake = new byte[5];
        handshake[0] = (byte)PacketType.Handshake;
        _WriteInt(seq, handshake, 1);


        UdpClient cli = new UdpClient();

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
                        if (Data.Length == 5 && Data[0] == (byte)PacketType.HandshakeAck)
                        {
                            int recvseq = _ReadInt(Data, 1);
                            _ClientConnection cc = new _ClientConnection(seq, recvseq, cli, EndPoint);
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
    /// Random number generator for intial sequence numbers.
    /// </summary>
    private static Random _Random = new Random();

    /// <summary>
    /// Creates a good, random, initial sequence number.
    /// </summary>
    private static int _CreateInitialSequence()
    {
        return _Random.Next(int.MinValue, 0);
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
                if (Data.Length == 5 && Data[0] == (byte)PacketType.Handshake)
                {
                    int seq = _CreateInitialSequence();
                    int recvseq = _ReadInt(Data, 1);
                    byte[] handshakeack = new byte[5];
                    handshakeack[0] = (byte)PacketType.HandshakeAck;
                    _WriteInt(seq, handshakeack, 1);

                    Send(this._Client, From, handshakeack);
                    _LConnection connection = new _LConnection(seq, recvseq, From, this, this._Client);
                    this._Connections[From] = connection;
                    this._OnConnect(connection);
                }
            }
            this._BeginListen();
        }

        
        private class _LConnection : _Connection
        {
            public _LConnection(int InitialSequence, int InitialReceiveSequence, IPEndPoint EndPoint, Listener Listener, UdpClient SendClient)
                : base(InitialSequence, InitialReceiveSequence)
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

            public override void OnDispose()
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
        public _Connection(int InitialSequence, int InitialReceiveSequence)
        {
            this._Received = new Dictionary<int, byte[]>();
            this._Sent = new Dictionary<int, byte[]>();

            this._ReceiveSequence = this._LastReceiveSequence = InitialReceiveSequence;
            this._SendSequence = this._InitialSequence = InitialSequence;
        }

        /// <summary>
        /// Gets how many sequence numbers off an incoming packet can be before it is disregarded.
        /// </summary>
        private const int _SequenceWindow = 128;

        public void Disconnect()
        {
            byte[] pdata = new byte[ _PacketHeaderSize];
            pdata[0] = (byte)PacketType.Disconnect;
            _WriteInt(this._SendSequence, pdata, 1);
            _WriteInt(this._ReceiveSequence, pdata, 5);
            this.SendRaw(pdata);
            this.Dispose();
        }

        /// <summary>
        /// Called when this connection is disposed.
        /// </summary>
        public virtual void OnDispose()
        {

        }

        public void Dispose()
        {
            this.KeepAlive = null;
            this.Timeout = null;
            this.OnDispose();
        }

        public void Send(byte[] Data)
        {
            this._SendWithSequence(this._SendSequence, Data);
            this._Sent[this._SendSequence] = Data;
            this._SendSequence++;
        }

        public double? KeepAlive
        {
            get
            {
                if (this._KeepAliveTimer == null)
                {
                    return null;
                }
                return this._KeepAliveTimer.Interval / 1000.0;
            }
            set
            {
                if (value != null)
                {
                    if (this._KeepAliveTimer == null)
                    {
                        this._KeepAliveTimer = new Timer(value.Value * 1000.0);
                        this._KeepAliveTimer.AutoReset = false;
                        this._KeepAliveTimer.Elapsed += delegate { this._SendKeepAlive(); };
                        this._KeepAliveTimer.Start();
                    }
                }
                else
                {
                    if (this._KeepAliveTimer != null)
                    {
                        this._KeepAliveTimer.Dispose();
                        this._KeepAliveTimer = null;
                    }
                }
            }
        }

        public double? Timeout
        {
            get
            {
                if (this._TimeoutTimer == null)
                {
                    return null;
                }
                return this._TimeoutTimer.Interval / 1000.0;
            }
            set
            {
                if (value != null)
                {
                    if (this._TimeoutTimer == null)
                    {
                        this._TimeoutTimer = new Timer(value.Value * 1000.0);
                        this._TimeoutTimer.AutoReset = false;
                        this._TimeoutTimer.Elapsed += delegate 
                        {
                            if (this.Disconnected != null)
                            {
                                this.Disconnected(false);
                            }
                            this.Dispose(); 
                        };
                        this._TimeoutTimer.Start();
                    }
                }
                else
                {
                    if (this._TimeoutTimer != null)
                    {
                        this._TimeoutTimer.Dispose();
                        this._TimeoutTimer = null;
                    }
                }
            }
        }

        /// <summary>
        /// Sends a sequenced packet with the specified sequence number.
        /// </summary>
        private void _SendWithSequence(int Sequence, byte[] Data)
        {
            byte[] pdata = new byte[Data.Length + _PacketHeaderSize];
            pdata[0] = (byte)PacketType.Sequenced;
            _WriteInt(Sequence, pdata, 1);
            _WriteInt(this._ReceiveSequence, pdata, 5);
            for (int t = 0; t < Data.Length; t++)
            {
                pdata[t + _PacketHeaderSize] = Data[t];
            }
            this._ResetKeepAlive();
            this.SendRaw(pdata);
        }

        /// <summary>
        /// Sends a keep alive packet.
        /// </summary>
        private void _SendKeepAlive()
        {
            byte[] pdata = new byte[_PacketHeaderSize];
            pdata[0] = (byte)PacketType.KeepAlive;
            _WriteInt(this._SendSequence - 1, pdata, 1);
            _WriteInt(this._ReceiveSequence, pdata, 5);
            this._ResetKeepAlive();
            this.SendRaw(pdata);
        }

        /// <summary>
        /// Sends a request for the packet with the specified sequence number.
        /// </summary>
        private void _SendRequest(int Sequence)
        {
            byte[] pdata = new byte[5];
            pdata[0] = (byte)PacketType.Request;
            _WriteInt(Sequence, pdata, 1);
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
            this._ResetKeepAlive();
            this.SendRaw(pdata);
        }

        private void _ResetKeepAlive()
        {
            if (this._KeepAliveTimer != null)
            {
                this._KeepAliveTimer.Interval = this._KeepAliveTimer.Interval;
            }
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
            if (Data.Length < 1)
            {
                return;
            }

            // Get packet type
            byte[] pdata;
            PacketType type = (PacketType)Data[0];
            switch (type)
            {
                case PacketType.Handshake:
                    byte[] handshakeack = new byte[5];
                    handshakeack[0] = (byte)PacketType.HandshakeAck;
                    _WriteInt(this._InitialSequence, handshakeack, 1);
                    this.SendRaw(handshakeack);
                    break;

                case PacketType.HandshakeAck:
                    break;

                case PacketType.Unsequenced:
                    pdata = new byte[Data.Length - 1];
                    for (int t = 0; t < pdata.Length; t++)
                    {
                        pdata[t] = Data[t + 1];
                    }
                    if (this.ReceivedPacket != null)
                    {
                        this.ReceivedPacket(false, pdata);
                    }
                    break;

                case PacketType.Sequenced:
                    if (Data.Length < _PacketHeaderSize)
                    {
                        break;
                    }

                    // Get actual packet data
                    pdata = new byte[Data.Length - _PacketHeaderSize];
                    for (int t = 0; t < pdata.Length; t++)
                    {
                        pdata[t] = Data[t + _PacketHeaderSize];
                    }

                    // Decode sequence and receive numbers
                    int sc = _ReadInt(Data, 1);
                    int rc = _ReadInt(Data, 5);
                    if (this._ValidPacket(sc, rc))
                    {
                        this._ProcessReceiveNumber(rc);

                        // See if this packet is actually needed
                        if (!this._Received.ContainsKey(sc))
                        {
                            if (sc > this._LastReceiveSequence)
                            {
                                this._LastReceiveSequence = sc;
                            }

                            // Give receive callback
                            if (this.ReceivedPacket != null)
                            {
                                this.ReceivedPacket(true, pdata);
                            }


                            if (sc == this._ReceiveSequence)
                            {
                                // Give ordered receive packet callback (and update receive numbers).
                                while (true)
                                {
                                    this._ReceiveSequence++;
                                    sc++;
                                    if (this.ReceivedPacketOrdered != null)
                                    {
                                        this.ReceivedPacketOrdered(true, pdata);
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
                                if (this.ReceivedPacketOrdered != null)
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
                    }
                    break;

                case PacketType.KeepAlive:
                    if (Data.Length < _PacketHeaderSize)
                    {
                        break;
                    }

                    // Decode sequence and receive numbers
                    sc = _ReadInt(Data, 1); // Contains the last sent sequence number
                    rc = _ReadInt(Data, 5);

                    if (this._ValidPacket(sc, rc))
                    {
                        this._ProcessReceiveNumber(rc);

                        // Request previous packets that are needed
                        for (int i = this._ReceiveSequence; i <= sc; i++)
                        {
                            if (!this._Received.ContainsKey(i))
                            {
                                this._SendRequest(i);
                            }
                        }
                    }
                    break;

                case PacketType.Request:
                    if (Data.Length < 5)
                    {
                        break;
                    }

                    sc = _ReadInt(Data, 1);

                    // Send out requested packet
                    byte[] tosend;
                    if (this._Sent.TryGetValue(sc, out tosend))
                    {
                        this._SendWithSequence(sc, tosend);
                    }
                    break;

                case PacketType.Disconnect:
                    if (Data.Length < _PacketHeaderSize)
                    {
                        break;
                    }

                    // Decode sequence and receive numbers (to prove this is a valid disconnect).
                    sc = _ReadInt(Data, 1);
                    rc = _ReadInt(Data, 5);

                    if (this._ValidPacket(sc, rc))
                    {
                        if (this.Disconnected != null)
                        {
                            this.Disconnected(true);
                        }
                        this.Dispose();
                    }
                    break;
            }

            // Reset timeout
            if (this._TimeoutTimer != null)
            {
                this._TimeoutTimer.Interval = this._TimeoutTimer.Interval;
            }
        }

        /// <summary>
        /// Gets if a packet is likely to be valid (not spoofed) based on the Sequence and ReceiveSequence it gives. This function may also return false if
        /// the packet is guranteed to be useless.
        /// </summary>
        private bool _ValidPacket(int SC, int RC)
        {
            return SC >= this._ReceiveSequence && SC < this._LastReceiveSequence + _SequenceWindow && RC <= this._SendSequence && RC > this._SendSequence - _SequenceWindow;
        }

        /// <summary>
        /// Updates the state of the connection given a receive number from a packet.
        /// </summary>
        /// <param name="RC"></param>
        private void _ProcessReceiveNumber(int RC)
        {
            // Remove all sent items before the receive number (they should not need to be requested)
            while (this._Sent.Remove(--RC)) ;
        }

        public abstract IPEndPoint EndPoint { get; }

        public event DisconnectHandler Disconnected;
        public event ReceivePacketHandler ReceivedPacket;
        public event ReceivePacketHandler ReceivedPacketOrdered;

        private Timer _TimeoutTimer;
        private Timer _KeepAliveTimer;

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

        /// <summary>
        /// The first sequence number used for sending.
        /// </summary>
        private int _InitialSequence;
    }

    private static void _WriteInt(int Int, byte[] Data, int Offset)
    {
        Int = IPAddress.HostToNetworkOrder(Int);
        Data[Offset + 0] = (byte)Int;
        Data[Offset + 1] = (byte)(Int >> 8);
        Data[Offset + 2] = (byte)(Int >> 16);
        Data[Offset + 3] = (byte)(Int >> 24);
    }

    private static int _ReadInt(byte[] Data, int Offset)
    {
        int Int = (int)Data[Offset + 0] + ((int)Data[Offset + 1] << 8) + ((int)Data[Offset + 2] << 16) + ((int)Data[Offset + 3] << 24);
        return IPAddress.NetworkToHostOrder(Int);
    }

    /// <summary>
    /// Connection for a client.
    /// </summary>
    private class _ClientConnection : _Connection
    {
        public _ClientConnection(int InitialSequence, int InitialReceiveSequence, UdpClient Client, IPEndPoint EndPoint)
            : base(InitialSequence, InitialReceiveSequence)
        {
            this._Client = Client;
            this._EndPoint = EndPoint;
            this._BeginListen();
        }

        public override void OnDispose()
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
        HandshakeAck,
        KeepAlive,
        Disconnect
    }
}
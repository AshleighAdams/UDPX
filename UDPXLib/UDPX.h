#ifndef UDPX_H
#define UDPX_H

#include "windows.h"

#define UDPX_PACKETHEADERSIZE (1 + 4 + 4)
#define UDPX_MAXPACKETSIZE (65536 - UDPX_PACKETHEADERSIZE)

namespace UDPX
{
	enum PacketType : BYTE
    {
        Sequenced,
        Unsequenced,
        Request,
        Handshake,
        HandshakeAck,
        KeepAlive,
        Disconnect
    };

	bool InitSockets();
	void UninitSockets();

	class UDPXAddress
	{
	public:
		UDPXAddress();
		UDPXAddress( unsigned int a, unsigned int b, unsigned int c, unsigned int d, unsigned short Port );
		UDPXAddress( unsigned int Address, unsigned short Port );
		unsigned int GetAddress() const;
		unsigned short GetPort() const;
	private:
		unsigned int m_Address;
		unsigned short m_Port;
	};

	typedef void (__stdcall *DisconnectedFn)(bool Explict);
	typedef void (__stdcall *ReceivedPacketFn)(bool Checked, BYTE* Data, int Length);
	
	class Socket
	{
	public:
		Socket();
		bool Open(unsigned short port);
		void Close();
		bool Send(UDPXAddress* destination, const char* data, int size);
		int Receive(UDPXAddress* sender, void* data, int size);
	private:
		SOCKET handle;
	};

	void Send(Socket* s, UDPXAddress* address, BYTE data, int length)
	{
	}
	
	class UDPXConnection
	{
	public:
		UDPXConnection();
		UDPXConnection(UDPXAddress Address);
		UDPXConnection(UDPXAddress* Address);
		void				Send(BYTE Data);
		void				SendUnchecked(BYTE Data);
		void				Disconnect(void);
		void				SetKeepAlive(double Time);
		void				SetTimeout(double Time);
		void				SetDisconnectEvent(DisconnectedFn fp);
		void				SetReceivedPacketEvent(ReceivedPacketFn fp);
		UDPXAddress*		GetAddress();
		void				ReciveRaw(BYTE* Data, int Length);
	private:
		DisconnectedFn		m_pDisconnected;
		ReceivedPacketFn	m_ReceivedPacket;
		double				m_KeepAlive;
		double				m_Timeout;
		UDPXAddress*		m_pAddress;
		Socket*				m_pSocket;
		int					m_ReciveSequence;
		int					m_SendSequence;
	};


	typedef void (__stdcall *ConnectionHandelerFn)(UDPXConnection Connection);
	void Listen(int port, ConnectionHandelerFn connection);
	void Connect(UDPXAddress* Address, ConnectionHandelerFn connection);
}

#endif // UDPX_H

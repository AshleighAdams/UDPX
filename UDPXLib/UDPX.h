#ifndef UDPX_H
#define UDPX_H

#pragma comment(lib, "ws2_32.lib")

#include "winsock2.h"
#include "windows.h"
#include <map>

using std::map;

#define UDPX_PACKETHEADERSIZE (1 + 4 + 4)
#define UDPX_MAXPACKETSIZE (65536 - UDPX_PACKETHEADERSIZE)
#define UDPX_SEQUENCEWINDOW (100)
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

	void Send(Socket* s, UDPXAddress* address, BYTE* data, int length);

	typedef map<int,BYTE*> StoredPacketType;

	class UDPXConnection
	{
	public:
		friend DWORD (WINAPI ConnectThread)(void*); // This is just so we can access private members from some threads (the connect thread that is not a part of the object
		friend DWORD (WINAPI IncomingPacketThread)(void*); // and neither is this one)
		UDPXConnection();
		UDPXConnection(UDPXAddress Address);
		UDPXConnection(UDPXAddress* Address);
		~UDPXConnection();
		void				Send(BYTE* Data);
		void				SendUnchecked(BYTE* Data);
		void				Disconnect(void);
		void				SetKeepAlive(double Time);
		void				SetTimeout(double Time);
		void				SetDisconnectEvent(DisconnectedFn fp);
		void				SetReceivedPacketEvent(ReceivedPacketFn fp);
		void				SetReceivedPacketOrderdEvent(ReceivedPacketFn fp);
		UDPXAddress*		GetAddress(void);
	private:
		HANDLE				m_IncomingPacketThreadHandle;
		void				Init();
		void				ReciveRaw(BYTE* Data, int Length);
		bool				ValidPacket(int RS, int SS);
		void				SendRequest(int Sequence);
		void				SendRaw(BYTE* Data, int Length);
		void				SendWithSequence(int Sequence, BYTE* Data, int Length);
		void				ResetKeepAlive(void);
		DisconnectedFn		m_pDisconnected;
		ReceivedPacketFn	m_ReceivedPacket;
		ReceivedPacketFn	m_ReceivedPacketOrderd;
		double				m_KeepAlive;
		double				m_Timeout;
		UDPXAddress*		m_pAddress;
		Socket*				m_pSocket;
		int					m_InitialSequence;
		int					m_ReciveSequence;
		int					m_SendSequence;
		int					m_LastReceiveSequence;
		void				ProcessReciveNumber(int RS);
		StoredPacketType	m_SentPackets;
		StoredPacketType	m_RecivedPackets;
	};
	
	DWORD WINAPI ConnectThread(void* arg);
	DWORD WINAPI IncomingPacketThread(void* arg);

	typedef void (__stdcall *ConnectionHandelerFn)(UDPXConnection* Connection);
	void Listen(int port, ConnectionHandelerFn connection);
	void Connect(UDPXAddress* Address, ConnectionHandelerFn connection);
}

#endif // UDPX_H

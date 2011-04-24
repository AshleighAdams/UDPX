/*
 *	Created by C0BRA (Mitchel Collins) and DrSchnz (Dimity Z)
 *	Copyright 2011
 *	Many thanks to http://realdev.co.za/code/c-network-communication-using-udp for his socket class
 */

#define PLATFORM_WIN 0
#define PLATFORM PLATFORM_WIN

#define WSAErr() do{cerr << "WSAError: " << WSAGetLastError() << endl;}while(false)
#define PORT 27015

#include <winsock2.h>
#include "UDPX.h"
#include <iostream>
#include <map>
#include <time.h>
#include <limits.h>
#include "windows.h"

using std::cout;
using std::cerr;
using std::cin;
using std::endl;
using std::map;
using namespace UDPX;

namespace UDPX
{
	// Private
	void _WriteInt(int Val, BYTE* Data, int Offset)
    {
        Val = htonl(Val);
        Data[Offset + 0] = (byte)Val;
        Data[Offset + 1] = (byte)(Val >> 8);
        Data[Offset + 2] = (byte)(Val >> 16);
        Data[Offset + 3] = (byte)(Val >> 24);
    }

	int _ReadInt(BYTE* Data, int Offset)
    {
        int Int = (int)Data[Offset + 0] + ((int)Data[Offset + 1] << 8) + ((int)Data[Offset + 2] << 16) + ((int)Data[Offset + 3] << 24);
        return ntohl(Int);
    }
	
	// Public
	bool InitSockets()
	{
		WSADATA WsaData;
		return WSAStartup(MAKEWORD(2,2), &WsaData) == NO_ERROR;	 
	}	 
	void UninitSockets()
	{
		WSACleanup();
	}

	
	UDPXAddress::UDPXAddress()
	{
		m_Address = 0;
		m_Port = 0;
	}
	UDPXAddress::UDPXAddress( unsigned int a, unsigned int b, unsigned int c, unsigned int d, unsigned short Port )
	{
		m_Address = (a << 24) | (b << 16) | (c << 8) | d; // this is not network byte order
		m_Port = Port;
	}
	UDPXAddress::UDPXAddress( unsigned int Address, unsigned short Port )
	{
		m_Address = Address;
		m_Port = Port;
	}
	unsigned int UDPXAddress::GetAddress() const//technically an IP is an unsigned long
	{
		return m_Address;
	}
	unsigned short UDPXAddress::GetPort() const
	{
		return m_Port;
	}



	Socket::Socket()
	{
		this->handle = socket( AF_INET, SOCK_DGRAM, IPPROTO_UDP );
		if (this->handle == INVALID_SOCKET)
			WSAErr();
	}

	bool Socket::Open(unsigned short port)
	{
		//set our ports etc
		sockaddr_in address;
		address.sin_family = AF_INET;
		address.sin_addr.s_addr = INADDR_ANY;
		address.sin_port = htons(port);
		int result = bind(this->handle,(const sockaddr*) &address,sizeof(sockaddr_in));
		if(result == SOCKET_ERROR)//incase another value below zero gets reserved to mean something other than 'érror'
			WSAErr();

		DWORD nonblocking = 1;
		if (ioctlsocket( this->handle,FIONBIO,&nonblocking) !=0)
		{
			cout<<"SOCKET FAILED TO SET NON-BLOCKING\n";
			WSAErr();
		}
		return 0;
	}
	void Socket::Close()
	{
	  closesocket(this->handle);
	}

	bool Socket::Send(UDPXAddress* destination, const char* data, int size)
	{
		unsigned int dest_addr = destination->GetAddress();
		unsigned short dest_port = destination->GetPort();

		sockaddr_in address;
		address.sin_family = AF_INET;
		address.sin_addr.s_addr = htonl(dest_addr);
		address.sin_port = htons(dest_port);

		int sent_bytes = sendto( this->handle, data, size, 0, (sockaddr*)&address, sizeof(address) );
		if ( sent_bytes < size || this->handle == INVALID_SOCKET)//why would the socket explode after?
		{
			WSAErr();
			return false;
		}

		return true;
	}

	int Socket::Receive(UDPXAddress* sender, void* data, int size)
	{
		typedef int socklen_t;
		socklen_t fromLength = sizeof(sockaddr_in);
		sockaddr_in pAddr;
		cout << "recieving data..." << endl;
		int received_bytes = recvfrom( this->handle, (char*)data, size, 0,  (sockaddr*)&pAddr, &fromLength);//this passed sender for the sockaddr...(the dangers of c-casts arise!)
		
		if(received_bytes == SOCKET_ERROR)
		{
			WSAErr();
			return -1;
		}
		sender = new UDPXAddress(pAddr.sin_addr.s_addr,pAddr.sin_port);//seeing as there are no set methods...
		return received_bytes;
	}
	
	DWORD WINAPI IncomingPacketThread(void* arg)
	{
		UDPXConnection* _this = (UDPXConnection*)arg;
		int Recived;
		while(true)
		{
			UDPXAddress* Sender;
			BYTE Data[UDPX_MAXPACKETSIZE + UDPX_PACKETHEADERSIZE];
			Recived = _this->m_pSocket->Receive(Sender, Data, UDPX_MAXPACKETSIZE + UDPX_PACKETHEADERSIZE);
			if(Recived > 0)
				_this->ReciveRaw(Data, Recived);
			Sleep(1);
		}
	}
	void UDPXConnection::Init()
	{
		// TODO create thread for reciving
		this->m_pSocket = new Socket();
		this->m_IncomingPacketThreadHandle = CreateThread(NULL, NULL, IncomingPacketThread, this, NULL, NULL);
	}
	UDPXConnection::UDPXConnection()
	{
		this->Init();
	}
	UDPXConnection::~UDPXConnection()
	{
		TerminateThread(this->m_IncomingPacketThreadHandle, 0);
		delete this->m_pAddress;
		delete this->m_pSocket;
	}
	UDPXConnection::UDPXConnection(UDPXAddress Address)
	{
		this->Init();
		this->m_pAddress = &Address;
	}
	UDPXConnection::UDPXConnection(UDPXAddress* Address)
	{
		this->Init();
		this->m_pAddress = Address;
	}
	void UDPXConnection::Send(BYTE* Data)
	{
		BYTE* data = new BYTE[sizeof(Data)]; // copy it so it can be disposed
		for(int i = 0; i < sizeof(Data); i++)
			data[i] = Data[i];
		this->SendWithSequence(this->m_SendSequence, data, sizeof(data));
		this->m_SentPackets[this->m_SendSequence] = data;
		this->m_SendSequence++;
	}
	void UDPXConnection::SendUnchecked(BYTE* Data)
	{
		int Length = sizeof(Data);
		BYTE* pdata = new BYTE[Length + 1];
		pdata[0] = PacketType::Unsequenced;
		for (int t = 0; t < Length; t++)
			pdata[t + 1] = Data[t];
		this->ResetKeepAlive();
		this->SendRaw(pdata, Length+1);
	}
	void UDPXConnection::Disconnect(void)
	{
		BYTE* pdata = new byte[UDPX_PACKETHEADERSIZE];
		pdata[0] = PacketType::Disconnect;
		_WriteInt(this->m_SendSequence, pdata, 1);
		_WriteInt(this->m_ReciveSequence, pdata, 5);
		this->SendRaw(pdata, 5);
		delete this;
	}
	void UDPXConnection::SetKeepAlive(double Time)
	{
		this->m_KeepAlive = Time;
	}
	void UDPXConnection::SetTimeout(double Time)
	{
		this->m_Timeout = Time;
	}
	void UDPXConnection::SetDisconnectEvent(DisconnectedFn fp)
	{
		this->m_pDisconnected = fp;
	}
	void UDPXConnection::SetReceivedPacketEvent(ReceivedPacketFn fp)
	{
		this->m_ReceivedPacket = fp;
	}
	void UDPXConnection::SetReceivedPacketOrderdEvent(ReceivedPacketFn fp)
	{
		this->m_ReceivedPacketOrderd = fp;
	}
	UDPXAddress* UDPXConnection::GetAddress()
	{
		return this->m_pAddress;
	}
	bool UDPXConnection::ValidPacket(int RS, int SS)
	{
		return SS >= this->m_ReciveSequence && SS < this->m_LastReceiveSequence + UDPX_SEQUENCEWINDOW && RS <= this->m_SendSequence && RS > this->m_SendSequence - UDPX_SEQUENCEWINDOW;
	}
	void UDPXConnection::SendRaw(BYTE* Data, int Length)
	{
		
		this->m_pSocket->Send(this->m_pAddress, (const char*)Data, Length);
	}
	void UDPXConnection::SendRequest(int Sequence)
	{
		BYTE pdata[5];
		pdata[0] = PacketType::Request;
        _WriteInt(Sequence, pdata, 1);
        this->SendRaw(pdata, 5);
		delete pdata;
	}
	void UDPXConnection::SendWithSequence(int Sequence, BYTE* Data, int Length)
	{
		BYTE* pdata = new BYTE[Length + UDPX_PACKETHEADERSIZE];
		pdata[0] = PacketType::Sequenced;
        _WriteInt(Sequence, pdata, 1);
        _WriteInt(this->m_ReciveSequence, pdata, 5);
        for (int t = 0; t < Length; t++)
            pdata[t + UDPX_PACKETHEADERSIZE] = Data[t];
        this->ResetKeepAlive();
        this->SendRaw(pdata, Length + UDPX_PACKETHEADERSIZE);
		delete pdata;
	}
	void UDPXConnection::ResetKeepAlive()
	{
		
	}
	void UDPXConnection::ProcessReciveNumber(int RS)
	{
		while (this->m_SentPackets.count(--RS) > 0)
			this->m_SentPackets.erase(RS);
	}
	void UDPXConnection::ReciveRaw(BYTE *Data, int Length)
	{
		if(Length < 1) return;
		BYTE type = Data[0];
		BYTE* pdata;
		switch(type)
		{
			case PacketType::Handshake:
				BYTE handshakeack[5];
				handshakeack[0] = PacketType::HandshakeAck;
				_WriteInt(this->m_InitialSequence, handshakeack, 1);
				this->SendRaw(handshakeack,5);
				delete handshakeack;
				break;

			case PacketType::HandshakeAck:
				break;

			case PacketType::Unsequenced:
				pdata = new BYTE[Length-1];
				for (int t = 0; t < Length; t++)
					pdata[t] = Data[t + 1];
				if(this->m_ReceivedPacket)
					this->m_ReceivedPacket(false, pdata, Length -1);
				break;

			case PacketType::Sequenced:
			{
				if (Length < UDPX_PACKETHEADERSIZE)
					break;
				
				// get packet data
				pdata = new BYTE[Length - UDPX_PACKETHEADERSIZE];
				for (int t = 0; t < (Length - UDPX_PACKETHEADERSIZE); t++)
					pdata[t] = Data[t + UDPX_PACKETHEADERSIZE];
				
				int sc = _ReadInt(Data, 1);
				int rc = _ReadInt(Data, 5);
				if (this->ValidPacket(sc, rc))
				{
					this->ProcessReciveNumber(rc);

					/// This code needs to be C++ified
					// See if this packet is actually needed
					if (!(this->m_RecivedPackets.count(sc) > 0))
					{
						if (sc > this->m_LastReceiveSequence)
							this->m_LastReceiveSequence = sc;
	                    
						// Give receive callback
						if (this->m_ReceivedPacket)
							this->m_ReceivedPacket(true, pdata, (Length - UDPX_PACKETHEADERSIZE));
	                    
						if (sc == this->m_ReciveSequence)
						{
							// Give ordered receive packet callback (and update receive numbers).
							while (true)
							{
								this->m_ReciveSequence++;
								sc++;
								if (this->m_ReceivedPacketOrderd)
									this->m_ReceivedPacketOrderd(true, pdata, sizeof(pdata));
	                            
								if(this->m_RecivedPackets.count(sc) > 0)
								{
									pdata = this->m_RecivedPackets.find(sc)->second;
									this->m_RecivedPackets.erase(sc);
								}
								else break; // Don't have the next packet, lets stop here.
							}
						}
						else
						{
							// Store the data (if needed).
							if (this->m_ReceivedPacketOrderd)
							{
								BYTE* packettostore = new BYTE[sizeof(pdata)]; // We need to copy it, it gets deleted after
								for(int i = 0; i < sizeof(pdata); i++)
									packettostore[i] = pdata[i];
								this->m_RecivedPackets[sc] = packettostore;
							}
							else
								this->m_RecivedPackets[sc] = NULL;
	                        
						}

						// Request all previous packets we need
						for (int i = this->m_ReciveSequence; i < this->m_LastReceiveSequence; i++)
							if (!(this->m_RecivedPackets.count(sc) > 0))
								this->SendRequest(i);
					}

					/// End C++ifide
				}
			}break;

			case PacketType::KeepAlive:
			{
				if (Length < UDPX_PACKETHEADERSIZE)
					break;

				// Decode sequence and receive numbers
				int sc = _ReadInt(Data, 1); // Contains the last sent sequence number
				int rc = _ReadInt(Data, 5);

				if (this->ValidPacket(sc, rc))
				{
					this->ProcessReciveNumber(rc);

					// Request previous packets that are needed
					for (int i = this->m_ReciveSequence; i <= sc; i++)
					{
						if (!(this->m_RecivedPackets.count(i) > 0))
							this->SendRequest(i);
					}
				}
			}break;

			case PacketType::Request:
			{
				if (Length < 5)
					break;
	            
				int sc = _ReadInt(Data, 1);

				// Send out requested packet
				if (this->m_SentPackets.count(sc) > 0)// this._Sent.TryGetValue(sc, out tosend))
				{
					BYTE* tosend = this->m_SentPackets.find(sc)->second;
					this->SendWithSequence(sc, tosend, sizeof(tosend));
				}
			}break;

			case PacketType::Disconnect:
			{
				if (Length < UDPX_PACKETHEADERSIZE)
					break;

				// Decode sequence and receive numbers (to prove this is a valid disconnect).
				int sc = _ReadInt(Data, 1);
				int rc = _ReadInt(Data, 5);

				if (this->ValidPacket(sc, rc))
				{
					if (this->m_pDisconnected)
						this->m_pDisconnected(true);
					delete this; // We don't need ourself anymore
				}
				break;
			}break;
		}
		delete pdata;
	}

	void Listen(int Port, ConnectionHandelerFn connection)
	{
		
	}
	
	struct ConnectThreadArugments
	{
		UDPXAddress* Address;
		ConnectionHandelerFn ConnectionHandeler;
	};
	typedef struct PacketQueue
	{
		BYTE* Data;
		int Length;
		PacketQueue* Next;
	};

	
	DWORD WINAPI ConnectThread(void* arg)
	{
		ConnectThreadArugments* args = (ConnectThreadArugments*)arg;
		UDPXAddress* Address = args->Address;
		ConnectionHandelerFn OnConnect = args->ConnectionHandeler;
		free(args); // we don't need you anymore

		srand(time(NULL));
		int startsequence = INT_MIN + rand();
		
		BYTE pdata[5];
		pdata[0] = PacketType::Handshake;
		_WriteInt(startsequence, pdata, 1);
		
		Socket s;
		int Attempts = 5;
		double Timeout = 1.0;

		PacketQueue* FirstNode = NULL;
		PacketQueue* LastestNode = NULL;
		
		while(Attempts >= 0)
		{
			s.Send(Address, (const char*)pdata, 5);
			--Attempts;
			
			while(true)
			{
				BYTE packet[UDPX_MAXPACKETSIZE];
				UDPXAddress* Sender;
				int recived = s.Receive(Sender, packet, UDPX_MAXPACKETSIZE);
				if(recived == -1) break;
				if(Sender->GetAddress() == Address->GetAddress() && Sender->GetPort() == Address->GetPort()) // make sure it's from the correct person.
				{
					if(recived == 5 && packet[0] == PacketType::HandshakeAck)
					{
						int recsequence = _ReadInt(packet, 1);
						UDPXConnection* connection = new UDPXConnection(Sender);
						connection->m_ReciveSequence = recsequence;
						OnConnect(connection);
						PacketQueue* Node = FirstNode;
						while(Node)
						{
							connection->ReciveRaw(Node->Data, Node->Length);

							PacketQueue* LastNode = Node;
							Node = Node->Next;
							free(LastNode);
						}
						return 0;
					}
					else
					{
						PacketQueue* Node = new PacketQueue();
						Node->Data = packet;
						Node->Length = recived;
						Node->Next = NULL;
						if(!FirstNode)
						{
							FirstNode = Node;
							LastestNode = Node;
						}
						else
						{
							LastestNode->Next = Node;
							LastestNode = Node;
						}
					}
				}
			}
			Sleep(Timeout * 1000);
		}
		if(FirstNode) // Lets free any data there may have been
		{
			PacketQueue* Node = FirstNode;
			while(Node)
			{
				PacketQueue* LastNode = Node;
				Node = Node->Next;
				free(LastNode);
			}
		}
		OnConnect(NULL);
		return 0;
	}

	void Connect(UDPXAddress* Address, ConnectionHandelerFn connection)
	{
		ConnectThreadArugments arg;
		arg.Address = Address;
		arg.ConnectionHandeler = connection;

		CreateThread(NULL,NULL,ConnectThread,&arg,NULL,NULL);
	}
}


/*////




	int client(Socket s)
	{
		 cout<<"CLIENT\n";
	 
		 const char d[] = "hello world!";
		 Address a = Address(127,0,0,1,PORT);//127.0.0.1 is localhost (127.0.0.0 is the windows system gateway)
		 s.Send(a, d, sizeof(d));
		 cin.get();
		 return 0;
	}
	 
	int server(Socket s)
	{
		cout<<"SERVER\n";
	 
		while (true)
		{
			Address sender;
			unsigned char buffer[1024];//this was an array of 1024 char pointers...
			int br = s.Receive(sender, buffer, sizeof(buffer));//this passed a pointer to the array, instead or a pointer the the first element...
			if (br > 0)
			{
				cout << "Data Recived..." << endl;//debugging messages ftw!
				for (int i = 0; i < br; i++)
				{
					cout << buffer[i];
				}
				cout<<"\n";
			}
			else
				WSAErr();//errors wheren't checked, which resulted in error 10014 being unnoticed...
		}
		return 0;
	}
	 
	int mainnope(int argc, char *argv[])
	{
	 
		if (!InitSock())
			return 1; //if there is an error return 1!
		Socket s;
		 //socket opened on port g_port;

		if (argc > 1)
		{
			if (strcmp(argv[1],"-s") == 0)
			{
				  //this was in the wrong place, causing double+ binding of the same port, again a WSAError will tell you this ;)
				int res = s.Open(PORT);
				if (res < 0)
				{
					WSAErr();
					cin.get();
					return 1;
				}
 				server(s);
			 }
			 else
			 {
				client(s);
			 }
		 }
		 else
		 {
			  client(s);
		 }
	 
		 s.Close();
	 
		 StopSock();
		 return 0;
	}


	///*/
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
#include <time.h>
#include <limits.h>
#include "windows.h"

using std::cout;
using std::cerr;
using std::cin;
using std::endl;
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




	
	UDPXConnection::UDPXConnection()
	{
	}
	UDPXConnection::UDPXConnection(UDPXAddress Address)
	{
		this->m_pAddress = &Address;
	}
	UDPXConnection::UDPXConnection(UDPXAddress* Address)
	{
		this->m_pAddress = Address;
	}
	void UDPXConnection::Send(BYTE Data)
	{
	}
	void UDPXConnection::SendUnchecked(BYTE Data)
	{
	}
	void UDPXConnection::Disconnect(void)
	{
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
	UDPXAddress* UDPXConnection::GetAddress()
	{
		return this->m_pAddress;
	}


	void UDPXConnection::ReciveRaw(BYTE *Data, int Length)
	{
		if(Length < 1) return;
		PacketType type = Data[0];
		switch(type)
		{
		case PacketType::Handshake:
			break;

		case PacketType::HandshakeAck:
			break;

		case PacketType::Unsequenced:
			BYTE pdata[Length-1];
            for (int t = 0; t < Length; t++)
                pdata[t] = Data[t + 1];
			if(this->m_ReceivedPacket)
				this->m_ReceivedPacket(false, pdata, Length -1);
			break;

		case PacketType::Sequenced:
			if (Length < UDPX_PACKETHEADERSIZE)
                break;

            // Get actual packet data
            pdatas = new byte[Data.Length - _PacketHeaderSize];
            for (int t = 0; t < pdata.Length; t++)
            {
                pdata[t] = Data[t + _PacketHeaderSize];
            }
			break;

		case PacketType::KeepAlive:
			break;

		case PacketType::Request:
			break;

		case PacketType::Disconnect:
			break;

		}
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
				if(Sender->GetAddress() == Address->GetAddress() && Sender->GetPort() == Address->GetPort()) // make sure it's from the correct person.
				{
					if(recived == -1) break;
					if(recived == 5 && packet[0] == PacketType::HandshakeAck)
					{
						int recsequence = _ReadInt(packet, 1);
						UDPXConnection* connection = new UDPXConnection(Sender);
						
						PacketQueue* Node = FirstNode;
						while(Node)
						{
							//Simulate the packet once we connect
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
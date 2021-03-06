/*
	UDPXLib Test Project
*/


#include "stdafx.h"
#include <iostream>
#include "../UDPXLib/UDPX.h"

#pragma comment(lib, "../UDPXLib/bin/Debug/UDPXLib.lib")

using namespace UDPX;
using namespace std;

bool Exit = false;

void __stdcall Disconnected(UDPXConnection* Connection, bool Explict)
{
	std::cout<<"Disconnected\n";
	Exit = true;
}

void __stdcall RecivedPacket(UDPXConnection* Connection, bool Checked, BYTE* Data, int Length)
{
	for(int i = 0; i < Length; i++)
		std::cout<<((char)Data[0]);
	std::cout<<"\n";
}

void __stdcall ConnectionHandeler(UDPXConnection* Connection)
{
	if(!Connection)
	{
		std::cout<<"The connection could not be made!\n";
		Exit = true;
		return;
	}
	std::cout<<"Connected to localhost\n";
	
	Connection->SetTimeout(10.0);
	Connection->SetKeepAlive(3.0);
	
	Connection->SetReceivedPacketEvent(&RecivedPacket);
	Connection->SetDisconnectEvent(&Disconnected);
	//Connection->Disconnect();
	//std::cout<<"Dissconnected from localhost\n";
	//Exit = true;
}

int _tmain(int argc, _TCHAR* argv[])
{
	UDPX::InitSockets();
	std::cout<<"Connectiong to localhost...\n";
	UDPXAddress* addr = new UDPXAddress(127,0,0,1,(unsigned short)100);
	addr->Port = 100;
	printf("%i - %i\n", addr->Address, addr->Port);
	std::cout<<addr->Address<<" - "<<addr->Port<<"\n";
	Connect(addr, &ConnectionHandeler);
	
	while(!Exit) Sleep(100);
	UDPX::UninitSockets();
	return 0;
}


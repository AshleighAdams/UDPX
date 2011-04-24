/*
	UDPXLib Test Project
*/


#include "stdafx.h"
#include <iostream>
#include "../UDPXLib/UDPX.h"

#pragma comment(lib, "../UDPXLib/bin/Release/UDPXLib.lib")

using namespace UDPX;
using namespace std;

bool Exit = false;

void __stdcall ConnectionHandeler(UDPXConnection* Connection)
{
	if(!Connection)
	{
		std::cout<<"The connection could not be made!\n";
		Exit = true;
		return;
	}
	std::cout<<"Connected to localhost\n";
	Connection->Disconnect();
	std::cout<<"Dissconnected from localhost\n";
	Exit = false;
}

int _tmain(int argc, _TCHAR* argv[])
{
	UDPX::InitSockets();
	std::cout<<"Connectiong to localhost...\n";
	Connect(new UDPXAddress(127,0,0,1,100), &ConnectionHandeler);
	
	while(!Exit);
	UDPX::UninitSockets();
	return 0;
}


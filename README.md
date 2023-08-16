# Scpi.NET

Welcome to Scpi.NET, a pure .NET library for controlling SCPI devices. If you ever tried to control
an oscilloscope or digital multimeter from your computer, you probably came across the
[SCPI commands](https://en.wikipedia.org/wiki/Standard_Commands_for_Programmable_Instruments)
which allow to control such devices.

Although this standard looks like a simple text communication, the real use is not so easy because these
text commands have to be wrapped into a lower-level communication protocol like TCP/IP or USBTMC which the device
understands. The most of manufacturers provide their own libraries like Tektronix's
[TekVISA](https://www.tek.com/en/support/software/driver/tekvisa-connectivity-software-v411)
or National Instruments' [NI-VISA](https://www.ni.com/en/support/downloads/drivers/download.ni-visa.html#484351).
Unfortunately, in my opinion these libraries are too heavy, create unwanted software dependencies,
make installation packages larger, usually focus on one manufacturer hardware only and in the end they
only provide a "pipe" which is able to transfer the SCPI commands. **The goal of this project is to create a simple, lightweight and manufacturer-independent library which can talk to any SCPI device over USB or Ethernet.**

## SCPI over TCP/IP

This is the easy part of the library and I have implemented it only to give the same control interface
to both USB and Ethernet devices. The devices listen the TCP port `4000` and the SCPI commands are sent
directly in the payload without any headers.

## SCPI over USB

This is the hard part of the library. The devices implement the
[USB Test & Measurement Class (TMC)](https://www.usb.org/document-library/test-measurement-class-specification)
which requires quite low-level USB communication. Both read and write requests have their own headers which
create additional level of complexity. Although I read several documents about the TMC, I still had to
reverse-engineer the communication of my oscilloscope to get it working. Another useful bits of code
were found in the Linux kernel drivers. Although there are still some places where I am not sure if the
implementation is correct, the library works well with several Tektronix oscilloscopes and digital multimeter
from Keysight.

### USB drivers

My USB TMC driver relies on Windows API calls to the `kernel32.dll` and `SetupApi.dll` libraries which are
the integral part of the Windows operating system, so that no additional software dependencies are needed.

In order to make USB TMC device working, you need to install the correct USB driver. It's quite tricky to get
the driver because it is usually part of the VISA libraries and cannot be downloaded separately. I cannot share
the driver I use because of the license restrictions, but as a starting point, I can recommend the link below
where exactly the same driver is used and the connectivity checklist is perfectly valid for all USB TMC devices: 

https://www.siglenteu.com/operating-tip/usb-connectivity-checklist/

# Usage

```csharp
using ScpiNet;
...

// List available USB devices. We will get back USB identifiers which can be used to open the device.
List<string> devices = UsbScpiConnection.GetUsbDeviceList();

// In order to get instrument name from the USB identifier, we need to open the device and ask it for the name.
// The connection implements IDisposable, so it's recommended to use it in the using block.
using (IScpiConnection connection = new UsbScpiConnection(devices[0]))
{
	// Create the connection:
	await connection.Open();

	// Get the instrument name:
	string id = await connection.GetId();

	// Now we can send commands and receive responses using extension methods defined in the `ScpiConnectionExtensions` class:
	string response = await connection.Query("My very special command?");
	...
}

// Connecting the TCP/IP device is even easier because there is always one device listening on the port 4000:
using (IScpiConnection connection = TcpScpiConnection("192.168.1.100", 4000))
{
	// The rest is same as with the USB connection:
	await connection.Open();
	string id = await connection.GetId();
	...
}
```

# Instrument driver considerations

This library focuses only the the transmission of the SCPI commands and responses. It does not provide
any higher-level functionality like instrument drivers. The reason is that implementing of such drivers
is a very complex task:

- Every programmer needs to control different functions of the measurement instruments. If we wanted to
  address all possible requests, it would result in really complex and bloated API. Therefore I decided
  to create my private drivers which are tailored to the needs of my applications only.
- I don't know why, but the SCPI devices I encountered offered almost no means of reflection: You cannot
  ask the oscilloscope how many channels it has or what time bases are available. This means that you
  have to hard-code all these things into your driver. Therefore it is almost impossible to crete a driver
  without physically having the device in your hands.
# Scpi.NET

Welcome to Scpi.NET, a pure .NET library for controlling SCPI devices. If you ever tried to control
an oscilloscope or digital multimeter from your computer, you probably came across the
[SCPI commands](https://en.wikipedia.org/wiki/Standard_Commands_for_Programmable_Instruments)
which allow to control such devices.

Although this standard looks like a simple text communication, the real use is not so easy, because these
text commands have to be wrapped into a lower-level communication protocol like TCP/IP or USBTMC which the device
understands. The most of manufacturers provide their own libraries like Tektronix's
[TekVISA](https://www.tek.com/en/support/software/driver/tekvisa-connectivity-software-v411)
or National Instruments' [NI-VISA](https://www.ni.com/en/support/downloads/drivers/download.ni-visa.html#484351).
Unfortunately, in my opinion these libraries are too heavy, create unwanted software dependencies,
make installation packages larger, usually focus on one manufacturer hardware only and in the end they
only provide a "pipe" which is able to transfer the SCPI commands. **The goal of this project is to create a simple,
lightweight and manufacturer-independent library which can talk to any SCPI device over USB or Ethernet.**

## SCPI over TCP/IP

This is the easy part of the library and I have implemented it only to give the same control interface
to both USB and Ethernet devices. The devices listen to the TCP port `4000` and the SCPI commands are sent
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

[USB Connectivity Checklist](https://www.siglenteu.com/operating-tip/usb-connectivity-checklist/)

# Usage

Just install the [NuGet package](https://www.nuget.org/packages/ScpiNet) or clone the repository and add
reference to that. Then you can start with the examples below.


```csharp
using ScpiNet;
...

// List available USB devices. We will get back USB identifiers which can be used to open the device.
List<string> devices = UsbScpiConnection.GetUsbDeviceList();

// In order to get instrument name from the USB identifier, we need to open the device and ask it for
// the name. The connection implements IDisposable, so it's recommended to use it in the using block.
using (IScpiConnection connection = new UsbScpiConnection(devices[0]))
{
	// Create the connection:
	await connection.Open();

	// Get the instrument name:
	string id = await connection.GetId();

	// Send some SCPI command:
	await Connection.WriteString("My special SCPI command", true);

	// Read the response:
	string response = await Connection.ReadString();
	...
}

// Connecting the TCP/IP device is even easier because there is always one device listening
// on the port 4000:
using (IScpiConnection connection = TcpScpiConnection("192.168.1.100", 4000))
{
	// The rest is same as with the USB connection:
	await connection.Open();
	string id = await connection.GetId();
	...
}
```

Reading of instrument ID is fine, but you will probably want to send more SCPI commands to the device.
In order to keep the application architecture clean, you should create a separate class for the instrument
you are controlling. This can be done by inheriting from the `ScpiDevice` class which already provides some useful
methods such as `Query()`. Please see the `SampleApp` directory for more details.


# Instrument driver considerations

This library focuses only on the transmission of the SCPI commands and responses. It does not provide
any higher-level functionality like instrument drivers. The reason is that implementing of such drivers
is a very complex task:

- Every programmer needs to control different functions of the measurement instruments. If we wanted to
  address all possible requests, it would result in really complex and bloated API. Therefore I decided
  to create my private drivers which are tailored to the needs of my applications only.
- I don't know why, but the SCPI devices I encountered offered almost no means of reflection: You cannot
  ask the oscilloscope how many channels it has or what time bases are available. This means that you
  have to hard-code all these things into your driver. Therefore it is almost impossible to create a driver
  without physically having the device in your hands.
- Some high-level functions are already provided by the VISA suites provided by the device manufacturers.
  I don't want to compete with them.


# Troubleshooting

- If you receive communication timeout, it usually means that the device did not understand to your command
  and ignored it.
- Some devices are slow and need some time to process the command. Therefore, the library automatically ensures,
  there is at least 1 ms gap between two subsequent requests. Currently, this delay is not configurable, because
  i found it working well with all devices I tested. If you have a device which needs different delay, please let me know.
- USB TMC protocol uses a field *Tag* in the header of the request and response in order to check that the response
  corresponds to the request. Some devices ignore this and return zero *Tag* in the response. In order to work around
  this problem, the `UsbScpiConnection` class provides a property `TagCheckEnabled` which is `true` by default.
  If you receive tag mismatch errors, you can try to set this property to `false`.
- Some devices are sensitive to the size of reading buffer, even though this should not affect the communication.
  If I passed too large buffer to these devices, the request simply timed out. Therefore, the `IScpiConnection`
  interface contains a property called `DefaultBufferSize` which allows to fine tune the buffer size. The default
  value is only 128 bytes, which is safely small. You can increase it in your device driver/application if you
  want to read larger responses in a single step.


# Support and contributions

Currently, I am the only developer of this library and I am not able to test it with all possible devices. Also,
I am doing this project in my free time, and I am quite busy. I will try solve all issues and answer all questions
as quickly as possible, but please be patient.

If you are going to create an issue, please provide as much information as possible. If you can provide a code
snippet which reproduces the problem, it would be perfect. Some issues are hard to reproduce.

If you fixed a bug or implemented a new feature, please consider creating a pull request.  It will help this project
to grow.

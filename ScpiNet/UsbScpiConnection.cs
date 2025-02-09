using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ScpiNet
{
	/// <summary>
	/// The UsbConnection class represents a USB TMC connection to a TMC device.
	/// This class uses directly Windows API as its back-end.
	/// </summary>
	public class UsbScpiConnection : IScpiConnection
	{
		/// <summary>
		/// Creates or opens a file or I/O device. The most commonly used I/O devices are as follows: file, file stream, directory, physical disk, volume,
		/// console buffer, tape drive, communications resource, mail slot, and pipe. The function returns a handle that can be used to access the file or
		/// device for various types of I/O depending on the file or device and the flags and attributes specified.
		/// </summary>
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern SafeFileHandle CreateFile(string lpFileName, EFileAccess dwDesiredAccess, EFileShare dwShareMode, IntPtr lpSecurityAttributes, ECreationDisposition dwCreationDisposition, EFileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

		/// <summary>
		/// Reads data from the specified file or input/output (I/O) device. Reads occur at the position specified by the file pointer if supported by the device.
		/// </summary>
		[DllImport("kernel32", SetLastError = true)]
		private static extern unsafe bool ReadFile(SafeFileHandle handle, byte[] bytes, int numBytesToRead, out int numBytesRead, NativeOverlapped* overlapped);

		/// <summary>
		/// Writes data to the specified file or input/output (I/O) device.
		/// </summary>
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern unsafe bool WriteFile(SafeFileHandle handle, byte[] bytes, int numBytesToWrite, out uint numBytesWritten, NativeOverlapped* overlapped);

		/// <summary>
		/// Sends a control code directly to a specified device driver, causing the corresponding device to perform the corresponding operation.
		/// </summary>
		[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
		private static extern bool DeviceIoControl(SafeFileHandle hDevice, IoctlUsbTmc dwIoControlCode, byte[] lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

		/// <summary>
		/// Cancels running IO operation.
		/// </summary>
		[DllImport("kernel32.dll")]
		private static extern bool CancelIo(SafeFileHandle hFile);

		/// <summary>
		/// GUID of USB TMC devices.
		/// </summary>
		protected static readonly Guid USBTMC_CLASS_GUID = new("A9FDBB24-128A-11D5-9961-00108335E361");

		/// <summary>
		/// The SetupDiGetClassDevs function returns a handle to a device information set that contains requested device information elements for a local computer.
		/// </summary>
		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hWndParent, DiGetClassFlags Flags);

		/// <summary>
		/// The SetupDiEnumDeviceInfo function returns a SP_DEVINFO_DATA structure that specifies a device information element in a device information set.
		/// </summary>
		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

		/// <summary>
		/// The SetupDiEnumDeviceInterfaces function enumerates the device interfaces that are contained in a device information set.
		/// </summary>
		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, ref SP_DEVINFO_DATA devInfo, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

		/// <summary>
		/// The SetupDiGetDeviceInterfaceDetail function returns details about a device interface.
		/// </summary>
		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, ref uint requiredSize, IntPtr deviceInfoData);

		/// <summary>
		/// The SetupDiDestroyDeviceInfoList function deletes a device information set and frees all associated memory.
		/// </summary>
		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiDestroyDeviceInfoList (IntPtr DeviceInfoSet);

		/// <summary>
		/// Flags for CreateFile() function.
		/// </summary>
		[Flags]
		enum EFileAccess : uint
		{
			None = 0,
			AccessSystemSecurity = 0x1000000,
			MaximumAllowed = 0x2000000,

			Delete = 0x10000,
			ReadControl = 0x20000,
			WriteDAC = 0x40000,
			WriteOwner = 0x80000,
			Synchronize = 0x100000,

			StandardRightsRequired = Delete | ReadControl | WriteDAC | WriteOwner,
			StandardRightsRead = ReadControl,
			StandardRightsWrite = ReadControl,
			StandardRightsExecute = ReadControl,
			StandardRightsAll = StandardRightsRequired | Synchronize,

			FILE_READ_DATA = 0x0001,
			FILE_LIST_DIRECTORY = FILE_READ_DATA,
			FILE_WRITE_DATA = 0x0002,
			FILE_ADD_FILE = FILE_WRITE_DATA,
			FILE_APPEND_DATA = 0x0004,
			FILE_ADD_SUBDIRECTORY = FILE_APPEND_DATA,
			FILE_CREATE_PIPE_INSTANCE = FILE_APPEND_DATA,
			FILE_READ_EA = 0x0008,
			FILE_WRITE_EA = 0x0010,
			FILE_EXECUTE = 0x0020,
			FILE_TRAVERSE = FILE_EXECUTE,
			FILE_DELETE_CHILD = 0x0040,
			FILE_READ_ATTRIBUTES = 0x0080,
			FILE_WRITE_ATTRIBUTES = 0x0100,

			GenericRead = 0x80000000,
			GenericWrite = 0x40000000,
			GenericExecute = 0x20000000,
			GenericAll = 0x10000000,

			FILE_ALL_ACCESS =
				StandardRightsAll |
				FILE_LIST_DIRECTORY |
				FILE_ADD_FILE |
				FILE_ADD_SUBDIRECTORY |
				FILE_READ_EA |
				FILE_WRITE_EA |
				FILE_EXECUTE |
				FILE_DELETE_CHILD |
				FILE_READ_ATTRIBUTES |
				FILE_WRITE_ATTRIBUTES,

			FILE_GENERIC_READ =
				StandardRightsRead |
				FILE_READ_DATA |
				FILE_READ_ATTRIBUTES |
				FILE_READ_EA |
				Synchronize,

			FILE_GENERIC_WRITE =
				StandardRightsWrite |
				FILE_WRITE_DATA |
				FILE_WRITE_ATTRIBUTES |
				FILE_WRITE_EA |
				FILE_APPEND_DATA |
				Synchronize,

			FILE_GENERIC_EXECUTE =
				StandardRightsExecute |
				FILE_READ_ATTRIBUTES |
				FILE_EXECUTE |
				Synchronize
		}

		/// <summary>
		/// File sharing attributes for CreateFile() function.
		/// </summary>
		[Flags]
		public enum EFileShare : uint
		{
			None = 0x00000000,
			Read = 0x00000001,
			Write = 0x00000002,
			Delete = 0x00000004
		}

		/// <summary>
		/// Creation disposition options for CreateFile() function.
		/// </summary>
		public enum ECreationDisposition : uint
		{
			New = 1,
			CreateAlways = 2,
			OpenExisting = 3,
			OpenAlways = 4,
			TruncateExisting = 5
		}

		/// <summary>
		/// File attribute flags.
		/// </summary>
		[Flags]
		public enum EFileAttributes : uint
		{
			None = 0,
			Readonly = 0x00000001,
			Hidden = 0x00000002,
			System = 0x00000004,
			Directory = 0x00000010,
			Archive = 0x00000020,
			Device = 0x00000040,
			Normal = 0x00000080,
			Temporary = 0x00000100,
			SparseFile = 0x00000200,
			ReparsePoint = 0x00000400,
			Compressed = 0x00000800,
			Offline = 0x00001000,
			NotContentIndexed = 0x00002000,
			Encrypted = 0x00004000,
			Write_Through = 0x80000000,
			Overlapped = 0x40000000,
			NoBuffering = 0x20000000,
			RandomAccess = 0x10000000,
			SequentialScan = 0x08000000,
			DeleteOnClose = 0x04000000,
			BackupSemantics = 0x02000000,
			PosixSemantics = 0x01000000,
			OpenReparsePoint = 0x00200000,
			OpenNoRecall = 0x00100000,
			FirstPipeInstance = 0x00080000
		}

		/// <summary>
		/// ReadFile/WriteFile error code returned when the operation is handled asynchronously.
		/// </summary>
		private const uint ERROR_IO_PENDING = 997;

		/// <summary>
		/// DeviceIoControl flags for TMC devices according to the TMC specification.
		/// </summary>
		private enum IoctlUsbTmc : uint
		{
			GetInfo = 0x80002000,
			CancelIo = 0x80002004,
			WaitInterrupt = 0x80002008,
			ResetPipe = 0x8000201C,
			SendRequest = 0x80002080,
			GetLastError = 0x80002088,
		}

		/// <summary>
		/// Pipe reset types for ResetPipe action initiated by DeviceIoControl() function.
		/// </summary>
		private enum UsbTmcPipeType : byte
		{
			InterruptInPipe = 1,
			ReadDataPipe = 2,
			WriteDataPipe = 3,
			AllPipes = 4
		}

		/// <summary>
		/// USB TMC frame header according to the TMC specification. It is sent with each read and write request.
		/// </summary>
		[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
		private struct UsbTmcHeader {
			public UsbTmcMsgId MsgID;
			public byte bTag;
			public byte bTagInverse;
			public byte Reserved1;
			public uint TransferSize;
			public byte bmTransferAttributes;
			public byte Reserved2;
			public byte Reserved3;
			public byte Reserved4;
		}

		/// <summary>
		/// Message ID for UsbTmcHeader - distinguishes reads and writes.
		/// </summary>
		private enum UsbTmcMsgId : byte
		{
			DevDepMsgOut = 0x01,
			DevDepMsgIn = 0x02,
		}

		/// <summary>
		/// Filtering options for SetupDiGetClassDevs.
		/// </summary>
		[Flags]
		public enum DiGetClassFlags : uint
		{
			None = 0,
			DIGCF_DEFAULT = 0x00000001,
			DIGCF_PRESENT = 0x00000002,
			DIGCF_ALL_CLASSES = 0x00000004,
			DIGCF_PROFILE = 0x00000008,
			DIGCF_DEVICE_INTERFACE = 0x00000010,
		}

		/// <summary>
		/// Device information returned by SetupDiEnumDeviceInterfaces() function.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct SP_DEVINFO_DATA
		{
			public uint cbSize;
			public Guid ClassGuid;
			public uint DevInst;
			public IntPtr Reserved;
		}

		/// <summary>
		/// Device interface details returned by SetupDiGetDeviceInterfaceDetail() function.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct SP_DEVICE_INTERFACE_DATA
		{
			public int cbSize;
			public Guid interfaceClassGuid;
			public int flags;
			private readonly UIntPtr reserved;
		}

		/// <summary>
		/// Invalid handle value is returned in case of some error.
		/// </summary>
		protected static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

		/// <summary>
		/// Length of device path buffer in SP_DEVICE_INTERFACE_DETAIL_DATA structure. We use fixed buffer in order to make
		/// marshaling easier, so the buffer should be long enough for all device paths.
		/// </summary>
		private const int SP_DEVICE_INTERFACE_DETAIL_DATA_BUFFER_SIZE = 256;

		/// <summary>
		/// Device path retrieved by SetupDiGetDeviceInterfaceDetail function.
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private struct SP_DEVICE_INTERFACE_DETAIL_DATA
		{
			public int cbSize;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = SP_DEVICE_INTERFACE_DETAIL_DATA_BUFFER_SIZE)]
			public string DevicePath;
		}

		/// <summary>
		/// Number of milliseconds to wait between sending two subsequent commands.
		/// </summary>
		protected const int CommPauseMs = 1;

		/// <summary>
		/// Handle of opened USB device.
		/// </summary>
		protected SafeFileHandle DevHandle;

		/// <summary>
		/// Device path of the device.
		/// </summary>
		public string DevicePath { get; }

		/// <summary>
		/// Current value of Tag counter. This number is incremented with each TMC message starting from one
		/// because some devices do not like when it is zero.
		/// </summary>
		protected byte Tag;

		/// <summary>
		/// Communication timeout in milliseconds.
		/// </summary>
		public int Timeout { get; set; } = 1500;

		/// <summary>
		/// Default buffer size for read operations. This value can be changed in order to tweak the performance
		/// and communication stability. For example, my Keysight multimeter does not like when the buffer is larger than 128 bytes.
		/// </summary>
		public int DefaultBufferSize { get; set; } = 1024;

		/// <summary>
		/// This option allows to enable or disable checking of the Tag field in the header of TMC response.
		/// The device should send the same Tag value as the one sent in the request + its inverse, but some
		/// devices do not follow this rule and leave the Tag field zero. In such case, the TagCheckEnabled
		/// can be set to false to make these devices working.
		/// </summary>
		public bool TagCheckEnabled { get; set; } = true;

		/// <summary>
		/// Timestamp of the last command. Necessary to keep 1 ms gap between subsequent commands.
		/// This pause is recommended by some manufacturers.
		/// </summary>
		protected DateTime LastCmdTimeStamp;

		/// <summary>
		/// True if the device is open.
		/// </summary>
		public bool IsOpen => DevHandle?.IsInvalid == false && !DevHandle.IsClosed;

		/// <summary>
		/// Logger instance.
		/// </summary>
		protected ILogger<UsbScpiConnection> Logger { get; }

		/// <summary>
		/// Gets the list of connected USB devices.
		/// </summary>
		/// <returns>List of device paths which can be provided to the Create() method.</returns>
		public static List<string> GetUsbDeviceList()
		{
			Guid usbTmcGuid = USBTMC_CLASS_GUID;
			List<string> list = new();

			// Create a list of devices in USBTMC class:
			IntPtr deviceInterfaceSet = SetupDiGetClassDevs(ref usbTmcGuid, IntPtr.Zero, IntPtr.Zero, DiGetClassFlags.DIGCF_PRESENT | DiGetClassFlags.DIGCF_DEVICE_INTERFACE);
			if (deviceInterfaceSet == INVALID_HANDLE_VALUE) {
				throw CreateWin32Exception(Marshal.GetLastWin32Error(), "SetupDiGetClassDevs function failed");
			}

			// Iterate over the list:
			for (uint deviceIndex = 0; ; deviceIndex++) {
				// Create a Device Interface Data structure
				SP_DEVINFO_DATA deviceInfoData = new();
				deviceInfoData.cbSize = (uint)Marshal.SizeOf(deviceInfoData);

				// Call OS to get device info data:
				if (!SetupDiEnumDeviceInfo(deviceInterfaceSet, deviceIndex, ref deviceInfoData)) {
					break;
				}

				// The device can have more than one interface:
				for (uint deviceInterfaceIndex = 0; ; deviceInterfaceIndex++) {
					// Initialize struct to hold device interface data:
					SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new();
					deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

					// Get interface details:
					if (!SetupDiEnumDeviceInterfaces(deviceInterfaceSet, ref deviceInfoData, ref usbTmcGuid, deviceInterfaceIndex, ref deviceInterfaceData)) {
						break;
					}

					// Ask for device path, maximum buffer size is 256 bytes:
					uint requiredBufferSize = 0;
					SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailedData = new();
					if (IntPtr.Size == 8) {
						// for 64 bit operating systems
						deviceInterfaceDetailedData.cbSize = 8;
					} else {
						// for 32 bit systems
						deviceInterfaceDetailedData.cbSize = 4 + Marshal.SystemDefaultCharSize;
					}

					if (SetupDiGetDeviceInterfaceDetail(deviceInterfaceSet, ref deviceInterfaceData, ref deviceInterfaceDetailedData, SP_DEVICE_INTERFACE_DETAIL_DATA_BUFFER_SIZE, ref requiredBufferSize, IntPtr.Zero)) {
						list.Add(deviceInterfaceDetailedData.DevicePath);
					}
				}
			}

			// Clean up and return:
			SetupDiDestroyDeviceInfoList(deviceInterfaceSet);
			return list;
		}

		/// <summary>
		/// Creates an instance of UsbTmc device driver and opens the device..
		/// </summary>
		/// <param name="devPath">Device path to open.</param>
		/// <param name="logger">Instance of a logger abstraction.</param>
		public UsbScpiConnection(string devPath, ILogger<UsbScpiConnection> logger = null)
		{
			DevicePath = devPath;
			DevHandle = null;
			Tag = 1;
			Logger = logger;
		}

		/// <summary>
		/// Opens the connection. This method is implemented synchronously and the cancellation token is ignored.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		public Task Open(CancellationToken cancellationToken = default)
		{
			// Close old connection if exists:
			Close();

			// Open the device for exclusive asynchronous access:
			Logger?.LogInformation($"Opening USB TMC device '{DevicePath}'...");
			DevHandle = CreateFile(
				DevicePath,
				EFileAccess.GenericRead | EFileAccess.GenericWrite,
				EFileShare.Delete | EFileShare.Read | EFileShare.Write,
				IntPtr.Zero,
				ECreationDisposition.OpenExisting,
				EFileAttributes.Overlapped,
				IntPtr.Zero
			);

			if (DevHandle.IsInvalid) {
				throw CreateWin32Exception(Marshal.GetLastWin32Error(), "Failed to open USB TMC device");
			}

			// Bind device handle to the thread pool. This is necessary to make overlapped IO working:
			ThreadPool.BindHandle(DevHandle);

			// Ensure the device buffers are clear. This used to work in C++ with Tektronix OSC, but here
			// it breaks communication with Keysight multimeter.
			//ResetPipe();

			Logger?.LogInformation("USB TMC connection succeeded.");
			return Task.CompletedTask;
		}

		/// <summary>
		/// Closes the device. Called automatically when the device is disposed.
		/// </summary>
		public void Close()
		{
			if (DevHandle != null) {
				Logger?.LogInformation("Closing USB TMC device.");
				DevHandle.Close();
				DevHandle = null;
			}
		}

		/// <summary>
		/// Flushes communication buffers.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		public async Task ClearBuffers(CancellationToken cancellationToken = default)
		{
			if (!IsOpen) {
				throw new InvalidOperationException("Buffer clearing failed: TMC device is not open.");
			}

			// Read all data from the device until it is empty. The other way is to use the ResetPipe() method,
			// but it does not work with all TMC devices.
			Logger?.LogDebug("Clearing input buffer.");
			ReadResult result;
			byte[] buffer = new byte[1024];
			do {
				result = await Read(buffer, buffer.Length, 0, cancellationToken);
			} while (!result.Eof);
		}

		/// <summary>
		/// Resets the communication buffers.
		/// </summary>
		public void ResetPipe()
		{
			byte[] pipeType = new byte[4] { (byte)UsbTmcPipeType.AllPipes, 0, 0, 0 };

			Logger?.LogDebug("Reset pipe.");
			bool ret = DeviceIoControl(DevHandle, IoctlUsbTmc.ResetPipe, pipeType, pipeType.Length, IntPtr.Zero, 0, out int _, IntPtr.Zero);
			if (!ret) {
				throw CreateWin32Exception(Marshal.GetLastWin32Error(), "DeviceIoControl() function failed");
			}
		}

		/// <summary>
		/// Checks the time elapsed since the last command. If it is shorter than requested minimal pause,
		/// enforces a short delay not to flood the device by the commands.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		protected async Task EnforceCommPause(CancellationToken cancellationToken)
		{
			int lastPause = (DateTime.Now - LastCmdTimeStamp).Milliseconds;
			if (lastPause < CommPauseMs) {
				await Task.Delay(CommPauseMs - lastPause + 1, cancellationToken);
			}
			await Task.Delay(10, cancellationToken);
		}

		/// <summary>
		/// Asynchronously writes data to the USB device.
		/// </summary>
		/// <param name="data">Array of data to write.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Number of bytes written.</returns>
		protected async Task WriteUsb(byte[] data, CancellationToken cancellationToken)
		{
			uint bytesWritten = await Task.Run(() => {
				// This task will synchronize the asynchronous callback with async method:
				TaskCompletionSource<(uint,uint)> writeTask = new();

				unsafe {
					// Create overlapped structure pointer which will be used to process the asynchronous result:
					Overlapped overlapped = new();
					NativeOverlapped* nativeOverlapped = overlapped.Pack((uint errorCode, uint numBytes, NativeOverlapped* _) => writeTask.SetResult((errorCode, numBytes)), null);

					try {
						// Write data:
						if (WriteFile(DevHandle, data, data.Length, out uint written, nativeOverlapped)) {
							// Synchronous exit - the write has been completed immediately:
							return written;
						}

						// Function returned error. The only acceptable error is ERROR_IO_PENDING state:
						int err = Marshal.GetLastWin32Error();
						if (err != ERROR_IO_PENDING) {
							throw CreateWin32Exception(err, "WriteFile() system call failed");
						}

						// Now wait for the asynchronous callback:
						if (Task.WaitAny(new Task[] { writeTask.Task }, Timeout, cancellationToken) == -1) {
							CancelIo(DevHandle);
							throw new TimeoutException("WriteFile() system call timed out.");
						}

						// Now we should have our writeTask complete:
						(uint errorCode, uint numBytes) = writeTask.Task.Result;
						if (errorCode != 0) {
							throw CreateWin32Exception((int)errorCode, "WriteFile() system call failed asynchronously");
						}

						return numBytes;
					} finally {
						// Ensure the unmanaged pointer has been successfully released:
						Overlapped.Unpack(nativeOverlapped);
						Overlapped.Free(nativeOverlapped);
					}
				}
			});

			// Did we successfully write the whole message?
			if (bytesWritten != data.Length) {
				throw new Exception(string.Format("WriteUsb() failed: {0} bytes requested, but {1} written.", data.Length, bytesWritten));
			}
		}

		/// <summary>
		/// Performs asynchronous USB read operation.
		/// </summary>
		/// <param name="buffer">Buffer to read data into.</param>
		/// <param name="timeout">Timeout in milliseconds.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Number of bytes actually read.</returns>
		protected async Task<int> ReadUsb(byte[] buffer, int timeout, CancellationToken cancellationToken)
		{
			return await Task.Run(() => {
				// This task will synchronize the asynchronous callback with async method:
				TaskCompletionSource<(uint,uint)> readTask = new();

				unsafe {
					// Create overlapped structure pointer which will be used to process the asynchronous result:
					Overlapped overlapped = new();
					NativeOverlapped* nativeOverlapped = overlapped.Pack((uint errorCode, uint bytesRead, NativeOverlapped* _) => readTask.SetResult((errorCode, bytesRead)), null);

					try {
						// Issue read request:
						if (ReadFile(DevHandle, buffer, buffer.Length, out int read, nativeOverlapped)) {
							// Synchronous exit - the function has been completed immediately:
							return read;
						}

						// Function returned error. The only acceptable error is ERROR_IO_PENDING state:
						int err = Marshal.GetLastWin32Error();
						if (err != ERROR_IO_PENDING) {
							throw CreateWin32Exception(err, "ReadFile() system call failed");
						}

						// Now wait for the asynchronous callback:
						if (Task.WaitAny(new Task[] { readTask.Task }, timeout, cancellationToken) == -1) {
							CancelIo(DevHandle);
							throw new TimeoutException("ReadFile() system call timed out.");
						}

						// Now we should have our writeTask complete:
						(uint errorCode, uint readBytes) = readTask.Task.Result;
						if (errorCode != 0) {
							throw CreateWin32Exception((int)errorCode, "ReadFile() system call failed asynchronously");
						}

						return (int)readBytes;
					} finally {
						// Ensure the unmanaged pointer has been successfully released:
						Overlapped.Unpack(nativeOverlapped);
						Overlapped.Free(nativeOverlapped);
					}
				}
			});
		}

		/// <summary>
		/// Creates a TMC request.
		/// </summary>
		/// <param name="msgId">Message ID.</param>
		/// <param name="eom">True to set EOM flag (end of message).</param>
		/// <param name="data">Data to add after the header.</param>
		/// <param name="length">Length of data to be transferred. If zero, the length is inferred from the data array length.</param>
		/// <returns>Byte array containing the USB TMC request.</returns>
		private byte[] CreateTmcRequest(UsbTmcMsgId msgId, bool eom, byte[] data, uint length = 0)
		{
			uint len = length;
			if (data != null && length == 0) {
				len = (uint)data.Length;
			}

			// Increment the tag number:
			Tag = (byte)(Tag < 255 ? Tag + 1 : 1);

			// Create the write header:
			UsbTmcHeader header = new() {
				MsgID = msgId,
				bTag = Tag,
				bTagInverse = (byte)~Tag,
				Reserved1 = 0,
				TransferSize = len,
				bmTransferAttributes = 0x00,
				Reserved2 = 0,
				Reserved3 = 0,
				Reserved4 = 0
			};

			// EOM means this is the last message in series:
			if (eom) {
				header.bmTransferAttributes = 0x01;
			}

			// Align the buffer length to fit 4 byte alignment:
			int bufferLength = Marshal.SizeOf(typeof(UsbTmcHeader)) + (int)len;
			while ((bufferLength & 0x03) != 0) {
				bufferLength++;
			}

			// Now make the target buffer:
			byte[] buffer = new byte[bufferLength];

			// Copy the header to the beginning of the array:
			IntPtr headerPtr = IntPtr.Zero;
			try {
				headerPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UsbTmcHeader)));
				Marshal.StructureToPtr(header, headerPtr, true);
				Marshal.Copy(headerPtr, buffer, 0, Marshal.SizeOf(typeof(UsbTmcHeader)));
			} finally {
				Marshal.FreeHGlobal(headerPtr);
			}

			// Add the write data after the header:
			if (data != null) {
				Array.Copy(data, 0, buffer, Marshal.SizeOf(typeof(UsbTmcHeader)), data.Length);
			}

			return buffer;
		}

		/// <summary>
		/// Asynchronously writes data to the TMC device.
		/// </summary>
		/// <param name="data">Array of data to write.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Number of bytes written.</returns>
		public async Task Write(byte[] data, CancellationToken cancellationToken = default)
		{
			if (!IsOpen) {
				throw new InvalidOperationException("Write failed: The TMC device is not open.");
			}

			// Wait 1 ms between subsequent commands:
			await EnforceCommPause(cancellationToken);

			// Send the request:
			byte[] message = CreateTmcRequest(UsbTmcMsgId.DevDepMsgOut, true, data);
			await WriteUsb(message, cancellationToken);

			// Remember the last command timestamp:
			LastCmdTimeStamp = DateTime.Now;
		}

		/// <summary>
		/// Reads binary data from the device.
		/// </summary>
		/// <param name="buffer">Buffer to store the read data.</param>
		/// <param name="readLength">Maximal number of bytes to read.</param>
		/// <param name="specialTimeout">Special timeout (milliseconds). If zero (default value), uses Timeout property value for timeout.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Array of bytes actually read and bool which indicates end-of-file (i.e. no other data is currently waiting to be read).</returns>
		public async Task<ReadResult> Read(byte[] buffer, int readLength = -1, int specialTimeout = 0, CancellationToken cancellationToken = default)
		{
			int headerSize = Marshal.SizeOf(typeof(UsbTmcHeader));

			if (!IsOpen) {
				throw new InvalidOperationException("Read failed: The TMC device is not open.");
			}

			// Check/fix the reading length:
			if (readLength < 0) {
				readLength = buffer.Length;
			} else if (readLength > buffer.Length) {
				throw new ArgumentOutOfRangeException(nameof(readLength), "Read length cannot be greater than the buffer size.");
			}

			// Enforce 1 ms pause in the communication between two messages:
			await EnforceCommPause(cancellationToken);

			// Write reading request to the device:
			await WriteUsb(CreateTmcRequest(UsbTmcMsgId.DevDepMsgIn, false, null, (uint)readLength), cancellationToken);

			// Receive the answer:
			byte[] receptionBuffer = new byte[readLength + headerSize];
			int readTimeout = specialTimeout > 0 ? specialTimeout : Timeout;
			int receivedBytesCount = await ReadUsb(receptionBuffer, readTimeout, cancellationToken);

			// Remember the last command timestamp:
			LastCmdTimeStamp = DateTime.Now;

			// Try to parse the message header:
			UsbTmcHeader header = default;
			if (receivedBytesCount < headerSize) {
				throw new Exception("Received data is shorter than TMC header.");
			}

			// Now convert data to the UsbTmcHeader structure:
			IntPtr headerPtr = IntPtr.Zero;
			try {
				headerPtr = Marshal.AllocHGlobal(headerSize);
				Marshal.Copy(receptionBuffer, 0, headerPtr, headerSize);
				header = Marshal.PtrToStructure<UsbTmcHeader>(headerPtr);
			} finally {
				Marshal.FreeHGlobal(headerPtr);
			}

			// Check Tag field inversion:
			if (TagCheckEnabled && header.bTag != (byte)~header.bTagInverse) {
				throw new Exception("Received data is not valid. The Tag field is not inverted properly.");
			}
			// The Tag field should also match current version of our Tag property:
			if (TagCheckEnabled && header.bTag != Tag) {
				throw new Exception("Received data is not valid, because the Tag field contains unexpected value.");
			}

			// Do we have all data?
			int receiveLength = (int)header.TransferSize;
			if (receivedBytesCount < headerSize + receiveLength) {
				throw new Exception("Received data is too short. The data part is shorter than TransferSize field claims.");
			}

			// Copy message content to the output array:
			Array.Copy(receptionBuffer, headerSize, buffer, 0, receiveLength);

			// Check for EOF flag:
			bool eof = header.bmTransferAttributes == 0x01;

			// Create the reading result:
			return new ReadResult(receiveLength, eof, buffer);
		}

		/// <summary>
		/// Creates a Win32Exception containing the system error message prefixed by custom string.
		/// </summary>
		/// <param name="code">Error code retrieved from GetLastError() function.</param>
		/// <param name="messagePrefix">Custom message prefix.</param>
		/// <returns>Win32Exception instance.</returns>
		private static Win32Exception CreateWin32Exception(int code, string messagePrefix)
		{
			string systemError = new Win32Exception(code).Message;
			return new Win32Exception(code, string.Format("{0}: {1}", messagePrefix, systemError));
		}

		/// <summary>
		/// Disposes the device.
		/// </summary>
		/// <param name="disposing">True if the action was caused by a program call.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing) {
				Close();
			}
		}

		/// <summary>
		/// Disposes the device.
		/// </summary>
		public void Dispose() => Dispose(true);
	}
}

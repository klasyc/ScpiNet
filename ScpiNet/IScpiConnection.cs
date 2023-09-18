using System.Threading;
using System;
using System.Threading.Tasks;

namespace ScpiNet
{
	/// <summary>
	/// The IScpiConnection interface represents a low-level connection to a SCPI device. The driver is responsible for wrapping
	/// data to the TMC messages and for handling connection to the device.
	/// </summary>
	public interface IScpiConnection : IDisposable
	{
		/// <summary>
		/// Communication timeout in milliseconds.
		/// </summary>
		int Timeout { get; set; }

		/// <summary>
		/// Device path of the device.
		/// </summary>
		string DevicePath { get; }

		/// <summary>
		/// Opens the connection.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		Task Open(CancellationToken cancellationToken = default);

		/// <summary>
		/// Closes the connection.
		/// </summary>
		void Close();

		/// <summary>
		/// True if the connection is established.
		/// </summary>
		bool IsOpen { get; }

		/// <summary>
		/// Writes binary data to the device. The driver is responsible for wrapping the data to the TMC header.
		/// </summary>
		/// <param name="data">Array of data to write.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		Task Write(byte[] data, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads binary data from the device.
		/// </summary>
		/// <param name="buffer">Buffer to store the data.</param>
		/// <param name="readLength">Maximum number of bytes to read. Default value -1 reads up to the provided buffer size.</param>
		/// <param name="specialTimeout">Special timeout (milliseconds). If zero (default value), uses Timeout property value for timeout.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Reading result structure.</returns>
		Task<ReadResult> Read(byte[] buffer, int readLength = -1, int specialTimeout = 0, CancellationToken cancellationToken = default);

		/// <summary>
		/// Flushes communication buffers.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		Task ClearBuffers(CancellationToken cancellationToken = default);
	}
}

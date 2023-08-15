using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace ScpiNet
{
	/// <summary>
	/// The TcpScpiConnection represents a SCPI connection tunnelled via the TCP protocol.
	/// </summary>
	public class TcpScpiConnection : IScpiConnection
	{
		/// <summary>
		/// TCP client used for the communication.
		/// </summary>
		private TcpClient _Client;

		/// <summary>
		/// Message read/write timeout in milliseconds.
		/// </summary>
		public int Timeout { get; set; }

		/// <summary>
		/// SCPI device path.
		/// </summary>
		public string DevicePath { get; }

		/// <summary>
		/// Hostname or IP address of SCPI device.
		/// </summary>
		public string Host { get; }

		/// <summary>
		/// Target TCP port of the SCPI device.
		/// </summary>
		public int Port { get; }

		/// <summary>
		/// Creates an instance of TcpScpiConnection.
		/// </summary>
		/// <param name="host">DNS name or IP address of the SCPI device.</param>
		/// <param name="port">Target TCP port of the connected SCPI device.</param>
		/// <param name="timeout">Read/write operation timeout in milliseconds.</param>
		public TcpScpiConnection(string host, int port, int timeout = 250)
		{
			Host = host;
			Port = port;
			Timeout = timeout;
		}

		/// <summary>
		/// Asynchronously opens the SCPI connection.
		/// </summary>
		/// <returns>Connection task.</returns>
		public async Task Open(CancellationToken cancellationToken = default)
		{
			// Create a new TCP client instance:
			_Client?.Dispose();
			_Client = new TcpClient {
				// This forces the socket to be immediately closed when Close method is called.
				LingerState = new LingerOption(true, 0)
			};

			// Start asynchronous connection and connection timeout task:
			Task timeoutTask = Task.Delay(100, cancellationToken);
			Task connTask = _Client.ConnectAsync(Host, Port);

			// Wait for either successful connection or timeout:
			await Task.WhenAny(connTask, timeoutTask);

			// Check for cancelling:
			if (timeoutTask.IsCanceled) {
				_Client.Dispose();
				_Client = null;
				throw new OperationCanceledException();
			}

			// Check for timeout:
			if (timeoutTask.IsCompleted) {
				_Client.Dispose();
				_Client = null;
				throw new TimeoutException($"Connection to the remote device {Host}:{Port} timed out.");
			}
		}

		/// <summary>
		/// Closes active connection.
		/// </summary>
		public void Close()
		{
			_Client?.Dispose();
			_Client = null;
		}

		/// <summary>
		/// True if the connection is currently open.
		/// </summary>
		public bool IsOpen => _Client?.Connected == true;

		/// <summary>
		/// Reads data from the SCPI device.
		/// </summary>
		/// <param name="buffer">Buffer to read data to.</param>
		/// <param name="readLength">Maximal length of data to be read.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>(Array of bytes actually read, true if there is some data remaining).</returns>
		public async Task<ReadResult> Read(byte[] buffer, int readLength = -1, CancellationToken cancellationToken = default)
		{
			if (!IsOpen) {
				throw new InvalidOperationException("Cannot read data, the connection is not open.");
			}

			// Check/fix the reading length:
			if (readLength < 0) {
				readLength = buffer.Length;
			} else if (readLength > buffer.Length) {
				throw new ArgumentOutOfRangeException(nameof(readLength), "Read length cannot be greater than the buffer size.");
			}

			// Start reading operation:
			NetworkStream stream = _Client.GetStream();
			Task<int> readTask = stream.ReadAsync(buffer, 0, readLength, cancellationToken);

			// Wait until the read task is finished or timeout is reached:
			if (await Task.WhenAny(readTask, Task.Delay(Timeout, cancellationToken)) != readTask) {
				throw new TimeoutException("Reading from the device timed out.");
			}

			return new ReadResult(readTask.Result, !stream.DataAvailable, buffer);
		}

		/// <summary>
		/// Writes data to SCPI device.
		/// </summary>
		/// <param name="data">Array of bytes to write.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Running task.</returns>
		public async Task Write(byte[] data, CancellationToken cancellationToken = default)
		{
			if (!IsOpen) {
				throw new InvalidOperationException("Cannot write data, the connection is not open.");
			}

			// Start write operation:
			Task writeTask = _Client.GetStream().WriteAsync(data, 0, data.Length, cancellationToken);

			// Wait until the read task is finished or timeout is reached:
			if (await Task.WhenAny(writeTask, Task.Delay(Timeout, cancellationToken)) != writeTask) {
				throw new TimeoutException("Write to the device timed out.");
			}
		}

		/// <summary>
		/// Clears data in the input communication buffer.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Running task.</returns>
		public async Task ClearBuffers(CancellationToken cancellationToken = default)
		{
			byte[] buffer = new byte[4096];

			if (!IsOpen) {
				throw new InvalidOperationException("Cannot clear buffers, the connection is not open.");
			}

			NetworkStream stream = _Client.GetStream();
			while (stream.DataAvailable) {
				await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
			}
		}

		/// <summary>
		/// Disposes the connection resources.
		/// </summary>
		/// <param name="disposing">True if disposal has been initiated by the application.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing) {
				Close();
			}
		}

		/// <summary>
		/// Disposes the connection resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}
	}
}
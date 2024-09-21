using Microsoft.Extensions.Logging;
using ScpiNet;
using System.Threading;
using System.Threading.Tasks;

namespace SampleApp
{
	/// <summary>
	/// This class represents a simplistic device driver for some SCPI device.
	/// </summary>
	public class MyScpiDevice : ScpiDevice
	{
		/// <summary>
		/// This static factory method is used to asynchronously connect the device, handle its initial connection tasks
		/// and finally return the instance of device driver.
		/// </summary>
		/// <param name="connection">Connection instance to use for communication.</param>
		/// <param name="logger">Optional logger instance.</param>
		/// <returns>Instance of connected device or exception, if connection fails.</returns>
		public static async Task<MyScpiDevice> Create(IScpiConnection connection, ILogger<ScpiDevice> logger = null, CancellationToken cancellationToken = default)
		{
			// Try to open the connection:
			logger?.LogInformation($"Opening USB connection for device {connection.DevicePath}...");
			await connection.Open(cancellationToken);

			// Get device ID:
			logger?.LogInformation("Connection succeeded, trying to read device ID...");

			string id = await connection.GetId(cancellationToken);
			logger?.LogInformation($"Connection succeeded. Device id: {id}");

			// Create the driver instance.
			return new MyScpiDevice(connection, id, logger);
		}

		/// <summary>
		/// The constructor is private, because we want to make the programmer to use the asynchronous factory method
		/// (constructors cannot be async).
		/// </summary>
		/// <param name="connection">Instance of connection to be used for communication.</param>
		/// <param name="deviceId">Devie ID.</param>
		/// <param name="logger">Logger instance.</param>
		protected MyScpiDevice(IScpiConnection connection, string deviceId, ILogger<ScpiDevice> logger = null)
			: base(connection, deviceId, logger)
		{
		}

		/// <summary>
		/// This is how we can implement a driver-specific method.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Value of the device's status buffer.</returns>
		public async Task<string> ReadStatusByte(CancellationToken cancellationToken = default)
		{
			return await Query("*STB?", false, cancellationToken);
		}
	}
}

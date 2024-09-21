using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScpiNet;

namespace SampleApp
{
	/// <summary>
	/// This simple program demonstrates, how to control a USB TMC device with Scpi.NET.
	/// </summary>
	public static class Program
	{
		/// <summary>
		/// Main application entry point.
		/// </summary>
		public static void Main()
		{
			// Run the asynchronous code synchronously:
			MainAsync().GetAwaiter().GetResult();
		}

		/// <summary>
		/// Asynchronous Main.
		/// </summary>
		private static async Task MainAsync()
		{
			List<string> devices;

			// Prepare logger factory which will provide a logger instance for our driver:
			using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
			ILogger log = loggerFactory.CreateLogger("Program");

			try {
				// List available USB devices. We will get back USB identifiers which can be used to open the device.
				log.LogInformation("Searching USB SCPI devices...");
				devices = UsbScpiConnection.GetUsbDeviceList();

				// Exit if there is no USB device:
				if (devices.Count == 0) {
					log.LogError("No USB device found. Exiting.");
					return;
				}

				// Print all device descriptors:
				log.LogInformation("Search succeeded, found the following devices:");
				foreach (string d in devices) {
					Console.WriteLine(d);
				}
			} catch (Exception ex) {
				log.LogError($"USB device search failed: {ex.Message}");
				return;
			}

			// Try to connect the first available device and get its identifier:
			try {
				// Create the connection instance. The constructor only remembers the connection parameters,
				// actual connection is done in connection.Open() method which is called later from the driver's factory method.
				using IScpiConnection connection = new UsbScpiConnection(devices[0]);

				// Try to connect our device using the custom device driver:
				MyScpiDevice device = await MyScpiDevice.Create(connection, loggerFactory.CreateLogger<MyScpiDevice>());

				// Once the device is ready, we can call driver-specific methods like this one:
				string stb = await device.ReadStatusByte();
				Console.WriteLine($"Status buffer: {stb}");
			} catch (Exception ex) {
				log.LogError($"Connection failed: {ex.Message}");
			}
		}
	}
}

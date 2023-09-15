using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ScpiNet
{
	/// <summary>
	/// Common ancestor for all TMC device classes.
	/// </summary>
	public class ScpiDevice : IDisposable
	{
		/// <summary>
		/// Connection to the device.
		/// </summary>
		public IScpiConnection Connection { get; }

		/// <summary>
		/// Optional logger instance.
		/// </summary>
		protected ILogger<ScpiDevice> Logger { get; }

		/// <summary>
		/// Number style used to parse SCPI numbers in scientific format.
		/// </summary>
		public const NumberStyles NumberStyle = NumberStyles.Float;

		/// <summary>
		/// Creates an instance of SCPI device.
		/// </summary>
		/// <param name="connection">Connection to use for device controlling.</param>
		/// <param name="deviceId">Device identifier retrieved during connection initialization.</param>
		/// <param name="logger">Instance of a logger abstraction.</param>
		public ScpiDevice(IScpiConnection connection, string deviceId, ILogger<ScpiDevice> logger = null)
		{
			Connection = connection;
			InstrumentId = deviceId;
			Logger = logger;
		}

		/// <summary>
		/// Device identifier retrieved by *IDN? function call.
		/// </summary>
		public string InstrumentId { get; }

		/// <summary>
		/// Asynchronously disposes the device. In contrast to the Dispose() method,
		/// this method is asynchronous and can be used to peacefully terminate communication with the device.
		/// </summary>
		public virtual Task Close()
		{
			Dispose();
			return Task.CompletedTask;
		}

		/// <summary>
		/// When disposing the driver, switch back to local control mode.
		/// </summary>
		/// <param name="disposing">True if disposing was initiated from the code.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing) {
				Connection.Dispose();
			}
		}

		/// <summary>
		/// Disposes the device.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

		/// <summary>
		/// Formats double so that the multimeter is able to parse it.
		/// </summary>
		/// <param name="number">Number to format.</param>
		/// <returns>String representation of the number.</returns>
		protected static string DoubleToStr(double number)
		{
			return number.ToString("G", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Converts double precision number in scientific notation string to double.
		/// </summary>
		/// <param name="number">Number to parse.</param>
		/// <returns>Double-precision number.</returns>
		protected static double ParseDouble(string number)
		{
			return double.Parse(number, ScpiConnectionExtensions.NumberStyle, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Executes a SCPI command which does not return any value.
		/// </summary>
		/// <param name="command">Command to send. New line is automatically added.</param>
		protected async Task SendCmd(string command)
		{
			Logger?.LogDebug($"Command: {command}");
			await Connection.WriteString(command, true);
		}

		/// <summary>
		/// Performs a query to the device. First a command is sent and then a response is received.
		/// </summary>
		/// <param name="command">Command to send. New line is automatically added.</param>
		/// <returns>Command response.</returns>
		protected async Task<string> Query(string command)
		{
			Logger?.LogDebug($"Query: {command}");
			await Connection.WriteString(command, true);
			string response = await Connection.ReadString();

			// The response should start with the :command, but instead of trailing
			// question mark there is a space and the response. This header has to be removed:
			string expectedHeader = ":" + command.Replace("?", " ");
			if (!response.StartsWith(expectedHeader)) {
				throw new Exception($"Cannot find response header: '{response}'.");
			}

			// Remove the header and return the response:
			response = response.Substring(expectedHeader.Length);
			Logger?.LogDebug($"Response: {response}");
			return response;
		}

		/// <summary>
		/// Performs a query to the device which returns a dictionary of key-value pairs.
		/// </summary>
		/// <param name="command">Command to send. New line is automatically added.</param>
		/// <returns>Dictionary of key-value pairs.</returns>
		protected async Task<Dictionary<string, string>> QueryDictionary(string command)
		{
			Logger?.LogDebug($"Query dictionary: {command}");
			await Connection.WriteString(command, true);
			string response = await Connection.ReadString();

			// The response should start with the header in format :QUERY:SUBQUERY: which has to be removed:
			Match header = Regex.Match(response, "^:[a-zA-Z0-9:]+:");
			if (!header.Success) {
				throw new Exception($"Cannot find response header: '{response}'.");
			}
			// Remove the header:
			response = response.Substring(header.Length);

			// Particular value pairs are separated by semicolons:
			string[] pairs = response.Split(';');

			// Now parse each pair which is separated by empty space:
			Dictionary<string, string> result = new();
			foreach (string pair in pairs) {
				string[] keyValue = pair.Split(' ');
				if (keyValue.Length != 2) {
					throw new Exception($"Invalid key-value pair: '{pair}'");
				}

				result.Add(keyValue[0], keyValue[1]);
			}

			Logger?.LogDebug($"Response: {string.Join(", ", result)}");
			return result;
		}

		/// <summary>
		/// Performs a query to the device which returns a floating point number.
		/// </summary>
		/// <param name="command">Command to send. New line is automatically added.</param>
		/// <returns>Double precision number which is result of the query.</returns>
		protected async Task<double> QueryDouble(string command)
		{
			string doubleStr = await Query(command);
			return double.Parse(doubleStr, NumberStyle, CultureInfo.InvariantCulture);
		}
	}
}

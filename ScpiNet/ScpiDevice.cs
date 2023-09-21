using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Threading;

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
		/// Number of milliseconds to wait between two consecutive trigger polling requests.
		/// </summary>
		protected int EventPollingIntervalMs { get; set; } = 250;

		/// <summary>
		/// Extended command timeout used in the PollQuery() method which polls the device state when it is busy.
		/// </summary>
		protected int ExtendedPollingCommandTimeoutMs { get; set; } = 1500;

		/// <summary>
		/// Default timeout for BUSY? queries.
		/// </summary>
		public const int DefaultPollingTimeoutMs = 10000;

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
			return double.Parse(number, NumberStyle, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Strips the response header from the response string.
		/// </summary>
		/// <param name="response">Response string to strip response from.</param>
		/// <param name="query">Query used to generate the response.</param>
		/// <returns>Response without the leading header or exception if the header is not found.</returns>
		protected static string StripHeader(string response, string query)
		{
			// The response should start with the :command, but instead of trailing
			// question mark there is a space and the response. This header has to be removed:
			string expectedHeader = ":" + query.Replace("?", " ");
			if (!response.StartsWith(expectedHeader)) {
				throw new Exception($"Cannot find response header: '{response}'.");
			}

			return response.Substring(expectedHeader.Length);
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
			string response = StripHeader(await Connection.ReadString(), command);
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

			// The response should start with the :command, but instead of trailing
			// question mark there is a colon. This header has to be removed.
			string header = ":" + command.Replace("?", ":");
			if (!response.StartsWith(header)) {
				throw new Exception($"Cannot find response header: '{response}'.");
			}
			response = response.Substring(header.Length);

			// Particular value pairs are separated by semicolons:
			string[] pairs = response.Split(';');

			// Now parse each pair which is separated by empty space:
			Dictionary<string, string> result = new();
			foreach (string pair in pairs) {
				// The first space in the pair separates the key from the value. String values are quoted
				// with escaped double quotes. Use regular expression to parse the pair and remove the quotes:
				Match match = Regex.Match(pair, @"^(?<key>\S+) \\?""?(?<value>[^\\""]+)\\?""?$");
				if (!match.Success) {
					throw new Exception($"Invalid key-value pair: '{pair}'");
				}

				result.Add(match.Groups["key"].ToString(), match.Groups["value"].ToString());
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

		/// <summary>
		/// Periodically sends a command to the device, until the device responds with the expected response.
		/// The timeoutMs parameter specifies the maximum time to wait for the expected response.
		/// The reading function uses extended timeout to wait for the response.
		/// </summary>
		/// <param name="command">Command to poll the result of.</param>
		/// <param name="response">Expected command response to cancel polling on.</param>
		/// <param name="timeoutMs">Timeout in milliseconds. Zero disables the timeout and waits infinitely.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		protected async Task PollQuery(string command, string response, int timeoutMs = DefaultPollingTimeoutMs, CancellationToken cancellationToken = default)
		{
			Logger?.LogDebug($"Polling the '{command}' command output and waiting for '{response}'...");
			DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);

			while (!cancellationToken.IsCancellationRequested) {
				// Query the device state. We will not use the standard Query method because we don't want to spoil the log output
				// with the polling queries:
				await Connection.WriteString(command, true);

				// Wait for the response with extended timeout:
				string responseWithHeader = await Connection.ReadString(ExtendedPollingCommandTimeoutMs, cancellationToken);
				string strippedResponse = StripHeader(responseWithHeader, command);

				// Check we have the expected response:
				if (strippedResponse == response) {
					Logger?.LogDebug($"Polling successfully finished - got '{strippedResponse}'.");
					return;
				}

				// Wait for a while and then try again:
				await Task.Delay(EventPollingIntervalMs, cancellationToken);
				if (timeoutMs > 0 && DateTime.Now > deadline) {
					throw new TimeoutException($"Polling of '{command}' timed out.");
				}
			}
		}
	}
}

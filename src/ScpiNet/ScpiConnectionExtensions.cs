using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScpiNet
{
	/// <summary>
	/// This class adds general-purpose methods to IScpiConnection interface.
	/// </summary>
	public static class ScpiConnectionExtensions
	{
		/// <summary>
		/// Number style used to parse SCPI numbers in scientific format.
		/// </summary>
		public const NumberStyles NumberStyle = NumberStyles.Float | NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowDecimalPoint;

		/// <summary>
		/// Writes a string to the device.
		/// </summary>
		/// <param name="conn">Connection to write string to.</param>
		/// <param name="data">String message to send.</param>
		/// <param name="addNewLine">If true, automatically adds new line after the message.</param>
		public static async Task WriteString(this IScpiConnection conn, string data, bool addNewLine = true)
		{
			string msg = data;

			if (addNewLine && msg.Last() != '\n') {
				msg += '\n';
			}

			byte[] binaryData = Encoding.ASCII.GetBytes(msg);
			await conn.Write(binaryData);
		}

		/// <summary>
		/// Reads a string from the device. The reading is done until all data is read.
		/// </summary>
		/// <param name="conn">Connection to read string from.</param>
		/// <returns>String retrieved from the device.</returns>
		public static async Task<string> ReadString(this IScpiConnection conn)
		{
			ReadResult chunk;
			StringBuilder response = new();
			byte[] buffer = new byte[1024];

			do {
				chunk = await conn.Read(buffer);
				response.Append(Encoding.ASCII.GetString(chunk.Data, 0, chunk.Length));
			} while (!chunk.Eof && chunk.Length > 0);

			return response.ToString().TrimEnd('\r', '\n');
		}

		/// <summary>
		/// Performs a query to the device. First a command is sent and then a response is received.
		/// </summary>
		/// <param name="conn">Connection to query from.</param>
		/// <param name="command">Command to send. New line is automatically added.</param>
		/// <returns>Command response.</returns>
		public static async Task<string> Query(this IScpiConnection conn, string command)
		{
			await conn.WriteString(command, true);
			return await conn.ReadString();
		}

		/// <summary>
		/// Performs a query to the device which returns a dictionary of key-value pairs.
		/// </summary>
		/// <param name="conn">Connection to query from.</param>
		/// <param name="command">Command to send. New line is automatically added.</param>
		/// <returns>Dictionary of key-value pairs.</returns>
		public static async Task<Dictionary<string, string>> QueryDictionary(this IScpiConnection conn, string command)
		{
			await conn.WriteString(command, true);
			string response = await conn.ReadString();

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

			return result;
		}

		/// <summary>
		/// Performs a query to the device which returns a floating point number.
		/// </summary>
		/// <param name="conn">Connection to query from.</param>
		/// <param name="command">Command to send. New line is automatically added.</param>
		/// <returns>Double precision number which is result of the query.</returns>
		public static async Task<double> QueryDouble(this IScpiConnection conn, string command)
		{
			string doubleStr = await Query(conn, command);
			return double.Parse(doubleStr, NumberStyle, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Gets instrument identification using *IDN? command.
		/// </summary>
		/// <param name="conn">Connection to use for identification query.</param>
		/// <returns>Device identifier.</returns>
		public static async Task<string> GetId(this IScpiConnection conn)
		{
			return await Query(conn, "*IDN?");
		}
	}
}

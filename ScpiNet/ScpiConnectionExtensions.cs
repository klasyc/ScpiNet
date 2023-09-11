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
		public const NumberStyles NumberStyle = NumberStyles.Float;

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

using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ScpiNet
{
	/// <summary>
	/// This class adds general-purpose methods to IScpiConnection interface.
	/// </summary>
	public static class ScpiConnectionExtensions
	{
		/// <summary>
		/// Writes a string to the device.
		/// </summary>
		/// <param name="conn">Connection to write string to.</param>
		/// <param name="data">String message to send.</param>
		/// <param name="addNewLine">If true, automatically adds new line after the message.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		public static async Task WriteString(this IScpiConnection conn, string data, bool addNewLine = true, CancellationToken cancellationToken = default)
		{
			string msg = data;

			if (addNewLine && msg.Last() != '\n') {
				msg += '\n';
			}

			byte[] binaryData = Encoding.ASCII.GetBytes(msg);
			await conn.Write(binaryData, cancellationToken);
		}

		/// <summary>
		/// Reads a string from the device. The reading is done until all data is read.
		/// </summary>
		/// <param name="conn">Connection to read string from.</param>
		/// <param name="specialTimeout">Special timeout (milliseconds). If zero (default value), uses Timeout property value for timeout.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>String retrieved from the device.</returns>
		public static async Task<string> ReadString(this IScpiConnection conn, int specialTimeout = 0, CancellationToken cancellationToken = default)
		{
			ReadResult chunk;
			StringBuilder response = new();

			// Some devices (such as the Keysight multimeters) do not like when we require reading longer than some specific constraint.
			// Therefore we will use only 128-bytes long buffer for generic string reads.
			byte[] buffer = new byte[128];

			do {
				chunk = await conn.Read(buffer, buffer.Length, specialTimeout, cancellationToken);
				response.Append(Encoding.ASCII.GetString(chunk.Data, 0, chunk.Length));
			} while (!chunk.Eof && chunk.Length > 0);

			return response.ToString().TrimEnd('\r', '\n');
		}

		/// <summary>
		/// Reads a byte array from the device. The reading is done until all data is read.
		/// </summary>
		/// <param name="conn">Connection to read bytes from.</param>
		/// <param name="specialTimeout">Special timeout (milliseconds). If zero (default value), uses Timeout property value for timeout.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Bytes retrieved from the device.</returns>
		public static async Task<byte[]>ReadBytes(this IScpiConnection conn, int specialTimeout = 0, CancellationToken cancellationToken = default)
		{
			var result = new MemoryStream();
			var buffer = new byte[1024];

			ReadResult chunk;
			do {
				chunk = await conn.Read(buffer, -1, specialTimeout, cancellationToken);
				result.Write(buffer, 0, chunk.Length);
			} while (!chunk.Eof && chunk.Length > 0);

			return result.ToArray();
		}

		/// <summary>
		/// Gets instrument identification using *IDN? command.
		/// </summary>
		/// <param name="conn">Connection to use for identification query.</param>
		/// <returns>Device identifier.</returns>
		public static async Task<string> GetId(this IScpiConnection conn)
		{
			await conn.WriteString("*IDN?", true);
			return await conn.ReadString();
		}
	}
}

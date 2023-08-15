using System;
using System.Globalization;
using System.Threading.Tasks;

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
		/// True if the device has already been disposed.
		/// </summary>
		private bool _IsDisposed;

		/// <summary>
		/// Creates an instance of SCPI device.
		/// </summary>
		/// <param name="connection">Connection to use for device controlling.</param>
		/// <param name="deviceId">Device identifier retrieved during connection initialization.</param>
		/// <param name="isSupported">True if the device is supported by the current application version.</param>
		public ScpiDevice(IScpiConnection connection, string deviceId, bool isSupported = true)
		{
			Connection = connection;
			InstrumentId = deviceId;
			IsSupported = isSupported;
		}

		/// <summary>
		/// Device identifier retrieved by *IDN? function call.
		/// </summary>
		public string InstrumentId { get; }

		/// <summary>
		/// True if the instrument is directly supported by the current application version.
		/// If false, the instrument has not been tested yet and it is controlled by the generic device driver.
		/// </summary>
		public bool IsSupported { get; }

		/// <summary>
		/// Asynchronously disposes the device.
		/// </summary>
		protected virtual Task AsyncDispose()
		{
			Connection.Dispose();
			return Task.CompletedTask;
		}

		/// <summary>
		/// When disposing the driver, switch back to local control mode.
		/// </summary>
		/// <param name="disposing">True if disposing was initiated from the code.</param>
		protected virtual void Dispose(bool disposing)
		{
			_IsDisposed = true;

			// The disposal sequence is asynchronous, but we don't wait for its finishing:
			if (disposing && !_IsDisposed) {
				Task.Run(() => AsyncDispose());
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
		protected string DoubleToStr(double number)
		{
			return number.ToString("G", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Converts double precision number in scientific notation string to double.
		/// </summary>
		/// <param name="number">Number to parse.</param>
		/// <returns>Double-precision number.</returns>
		protected double ParseDouble(string number)
		{
			return double.Parse(number, ScpiConnectionExtensions.NumberStyle, CultureInfo.InvariantCulture);
		}
	}
}

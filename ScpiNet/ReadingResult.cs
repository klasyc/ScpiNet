namespace ScpiNet
{
	/// <summary>
	/// This structure holds the result of the SCPI read operation.
	/// </summary>
	public readonly struct ReadResult
	{
		/// <summary>
		/// Number of bytes actually read.
		/// </summary>
		public int Length { get; }

		/// <summary>
		/// If true, all data has been read.
		/// </summary>
		public bool Eof { get; }

		/// <summary>
		/// Reference to the data buffer. It is the same buffer as the one passed to the Read method.
		/// </summary>
		public byte[] Data { get; }

		/// <summary>
		/// Creates the ReadResult structure.
		/// </summary>
		/// <param name="length">Length of data actually read.</param>
		/// <param name="eof">If true, there is no more data to read.</param>
		/// <param name="data">Data array.</param>
		public ReadResult(int length, bool eof, byte[] data)
		{
			Length = length;
			Eof = eof;
			Data = data;
		}
	}
}
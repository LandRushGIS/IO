using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.IO.Compression
{
	// http://www.ietf.org/rfc/rfc1950.txt
	// http://blogs.msdn.com/b/bclteam/archive/2007/05/16/system-io-compression-capabilities-kim-hamilton.aspx

	// TODO: Write support
	// TODO: Async operations
	// TODO: Adler32 checksum support
	// TODO: Dictionary support
	// TODO: Exception specification

	/// <summary>
	/// Provides support for reading zlib-compressed streams (RFC 1950). Wraps DeflateStream.
	/// </summary>
	public class ZLibStream : Stream
	{
		private const string messageNotSupported = "Not supported";
		private const string messageDictionaryNotSupported = "Dictionary is not supported";
		private const string messageStreamClosed = "Stream closed";
		private const string messageInvalidState = "Stream is in invalid state";
		private const string messageHeaderCorrupted = "ZLib header corrupted";

		private Stream baseStream;
		private CompressionMode mode;
		private bool leaveOpen;

		private bool disposed;
		private bool validState;
		private DeflateStream deflateStream;

		public ZLibStream(Stream stream, CompressionMode mode)
			: this(stream, mode, false)
		{ }

		public ZLibStream(Stream stream, CompressionMode mode, bool leaveOpen)
		{
			if (stream == null)
				throw new ArgumentNullException("stream");

			this.baseStream = stream;
			this.mode = mode;
			this.leaveOpen = leaveOpen;

			this.disposed = false;
			this.validState = true;
			this.deflateStream = null;
		}

		public override bool CanRead
		{
			get
			{
				return
					this.disposed || !this.validState ?
						false :
						this.deflateStream == null ?
							this.baseStream.CanRead :
							this.deflateStream.CanRead;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return
					this.disposed || !this.validState ?
						false :
						this.deflateStream == null ?
							this.baseStream.CanWrite :
							this.deflateStream.CanWrite;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return
					this.disposed || !this.validState ?
						false :
						this.deflateStream == null ?
							this.baseStream.CanSeek :
							this.deflateStream.CanSeek;
			}
		}

		public override long Length
		{
			get { throw new NotSupportedException(messageNotSupported); }
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException(messageNotSupported);
		}

		public override long Position
		{
			get { throw new NotSupportedException(messageNotSupported); }
			set { throw new NotSupportedException(messageNotSupported); }
		}

		public Stream BaseStream
		{
			get { return this.baseStream; }
		}

		public override void Flush()
		{
			this.EnsureNotDisposed();
			this.EnsureValidState();
			if (this.deflateStream == null)
				this.baseStream.Flush();
			else
				this.deflateStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException(messageNotSupported);
		}

		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException(messageNotSupported);
		}

		public override int EndRead(IAsyncResult asyncResult)
		{
			throw new NotSupportedException(messageNotSupported);
		}

		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException(messageNotSupported);
		}

		public override void EndWrite(IAsyncResult asyncResult)
		{
			throw new NotSupportedException(messageNotSupported);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			this.ReadHeaderIfNeeded();
			this.EnsureValidState();
			return this.deflateStream.Read(buffer, offset, count);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException(messageNotSupported);
		}

		private void ReadHeaderIfNeeded()
		{
			this.EnsureNotDisposed();
			this.EnsureValidState();

			// if deflateStream is not created - header is not processed yet
			if (this.deflateStream == null)
			{
				int result = this.baseStream.ReadByte();
				if (result < 0)
					throw new InvalidDataException(messageHeaderCorrupted);
				byte compressionMethodAndFlags = (byte)result;

				result = this.baseStream.ReadByte();
				if (result < 0)
					throw new InvalidDataException(messageHeaderCorrupted);
				byte flags = (byte)result;

				// if dictionary present
				if ((flags >> 5 & 1) == 1)
				{
					this.validState = false;
					throw new NotSupportedException(messageDictionaryNotSupported);
				}

				this.deflateStream = new DeflateStream(this.baseStream, this.mode, this.leaveOpen);
			}
		}

		protected override void Dispose(bool disposing)
		{
			try
			{
				if (disposing && !this.disposed)
				{
					if (this.deflateStream == null)
					{
						if (!this.leaveOpen)
							this.baseStream.Dispose();
					}
					else
					{
						this.deflateStream.Dispose();
					}
				}
				this.baseStream = null;
				this.deflateStream = null;
				this.disposed = true;
			}
			finally
			{
				base.Dispose(disposing);
			}
		}

		private void EnsureNotDisposed()
		{
			if (this.disposed)
				throw new ObjectDisposedException(null, messageStreamClosed);
		}

		private void EnsureValidState()
		{
			if (!this.validState)
				throw new InvalidOperationException(messageInvalidState);
		}
	}
}

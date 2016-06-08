using System;
using BinaryReader = System.IO.BinaryReader;

namespace LandRush.IO.DMF
{
	internal static class BinaryReaderUtils
	{
		public static string ReadShortString(this BinaryReader reader, int maxLength = 0)
		{
			// read first byte containing length of the string
			byte length = reader.ReadByte();
			// read effective bytes of the string
			string resultString = System.Text.Encoding.Default.GetString(reader.ReadBytes(length));
			// read the rest bytes of the string if exist and omit it
			if (maxLength > length)
			{
				reader.ReadBytes(maxLength - length);
			}

			return resultString;
		}

		public static double ReadExtended(this BinaryReader reader)
		{
			byte[] buffer = reader.ReadBytes(10);

			// read sign
			int sign = buffer[9] >> 7;

			// read biased exponent
			Int16 biasedExtendedExponent = BitConverter.ToInt16(new byte[] { buffer[8], (byte)(buffer[9] & 0x7f) }, 0);

			// read integer bit of significand
			int intBit = buffer[7] >> 7;

			// read significand
			UInt64 significand = 0;
			significand = (UInt64)(buffer[7] & 0x7f);
			for (int i = 6; i >= 0; i--)
				significand = (significand << 8) | buffer[i];

			Int16 biasedDoubleExponent = 0;

			if (biasedExtendedExponent == 0)
			{
				// signed zero or denormalized value
				biasedDoubleExponent = 0;
				if (intBit == 1)
				{
					throw new System.InvalidCastException("Computer is too old");
				}
			}
			// TODO: special cases - infinity, NaN etc.
			else
			{
				// normalized value
				biasedDoubleExponent = (Int16)(biasedExtendedExponent - extendedExponentBias + doubleExponentBias);
				if ((biasedDoubleExponent < 1) || (biasedDoubleExponent > 2046))
					throw new System.OverflowException();
			}

			UInt64 doubleBits = (UInt64)sign << 63 | (UInt64)biasedDoubleExponent << 52 | significand >> 11;

			return BitConverter.Int64BitsToDouble((System.Int64)doubleBits);
		}

		const Int16 extendedExponentBias = 16383;
		const Int16 doubleExponentBias = 1023;
	}
}

﻿using Stream = System.IO.Stream;
using BinaryReader = System.IO.BinaryReader;
using BitArray = System.Collections.BitArray;
using BitConverter = System.BitConverter;
using Int16 = System.Int16;
using UInt64 = System.UInt64;
using System.Collections.Generic;

namespace LandRush.IO.DMF
{
	static class BinaryReaderUtils
	{
		public static string ReadShortString(this BinaryReader reader, int maxLength = 0)
		{
			byte length = reader.ReadByte();
			string result = System.Text.Encoding.Default.GetString(reader.ReadBytes(length));
			if (maxLength > length)
			{
				reader.ReadBytes(maxLength - length);
			}
			return result;
		}

		const Int16 extendedExponentBias = 16383;
		const Int16 doubleExponentBias = 1023;

		public static double ReadExtended(this BinaryReader reader)
		{
			byte[] buffer = reader.ReadBytes(10);

			// read sign
			int sign = buffer[9] >> 7;

			// read exponent
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
				// signed zero or denormalized number
				biasedDoubleExponent = 0;
			}
			else
			{
				// normalized number
				biasedDoubleExponent = (Int16)(biasedExtendedExponent - extendedExponentBias + doubleExponentBias);
				if ((biasedDoubleExponent < 1) || (biasedDoubleExponent > 2046))
					throw new System.OverflowException();
			}

			UInt64 doubleBits = (UInt64)sign << 63 | (UInt64)biasedDoubleExponent << 52 | significand >> 11;

			return BitConverter.Int64BitsToDouble((System.Int64)doubleBits);
		}
	}

	public struct Version
	{
		public Version(uint major, uint minor)
		{
			this.Major = major;
			this.Minor = minor;
		}

		public override string ToString()
		{
			return Major.ToString() + "." + Minor.ToString();
		}

		public readonly uint Major;
		public readonly uint Minor;
	}

	public class Signature
	{
		public Signature(Version version, bool isCompressed)
		{
			this.Version = version;
			this.IsCompressed = isCompressed;
		}

		public readonly Version Version;
		public readonly bool IsCompressed;
	}

	public class Header
	{
		public Header(double scale, uint objectCount, string name, string leftFile, string rightFile)
		{
			this.Scale = scale;
			this.ObjectCount = objectCount;
			this.Name = name;
			this.LeftFile = leftFile;
			this.RightFile = rightFile;
		}

		// Знаменатель масштаба карты
		public readonly double Scale;
		// Количество топографических объектов на карте
		public readonly uint ObjectCount;
		// Наименование карты
		public readonly string Name;
		// Имя растрового файла, содержащего левый снимок стереопары или снимок (карту) для моно режима
		public readonly string LeftFile;
		// Имя растрового файла, содержащего правый снимок стереопары
		public readonly string RightFile;
	}

	enum LayerVisibleStatus
	{
		Editable = 0,
		Markable = 1,
		Visible = 2,
		Invisible = 3
	}

	public static class Reader
	{
		public static void Read(Stream stream)
		{
			var signature = ReadSignature(stream);
			if (signature.Version.Major != 1 ||
				signature.Version.Minor != 10)
				throw new System.NotSupportedException("Version " + signature.Version + " is not supported");

			Stream inputStream = !signature.IsCompressed ?
					stream :
					new LandRush.IO.Compression.ZLibStream(stream, System.IO.Compression.CompressionMode.Decompress, true);
			using (var reader = new BinaryReader(inputStream))
			{
				var header = ReadHeader(reader);
				ReadLayerList(reader);
				ReadParameterList(reader);
				ReadSymbolList(reader);
				ReadObjectList(reader, header.ObjectCount);
			}
		}

		private static Signature ReadSignature(Stream stream)
		{
			// 32-byte text signature, for example:
			// 'GeoSystem DMF, Version 1.10 C  \x1A'
			byte[] signature = new byte[32];
			int result = stream.Read(signature, 0, signature.Length);
			if (result != signature.Length)
				throw new System.IO.InvalidDataException("Invalid file format: expected 32-byte signature");
			string versionString = System.Text.Encoding.ASCII.GetString(signature, 23, 4).Trim();
			string[] versionStringComponents = versionString.Split('.');
			uint majorVersion = uint.Parse(versionStringComponents[0]);
			uint minorVersion = uint.Parse(versionStringComponents[1]);
			bool isCompressed = signature[28] == (byte)'C';
			return new Signature(new Version(majorVersion, minorVersion), isCompressed);
		}

		private static Header ReadHeader(BinaryReader reader)
		{
			// header size - 4-byte integer
			uint headerSize = reader.ReadUInt32();
			if (headerSize < 910) throw new System.IO.InvalidDataException("Invalid file format: unsupported header size");

			// map scale - 10-byte real
			double scale = reader.ReadExtended();

			// object count - 4-byte integer
			uint objectCount = reader.ReadUInt32();

			// units - 4-byte integer
			int units = reader.ReadInt32();

			// status - 4-byte integer
			int status = reader.ReadInt32();

			// frame - 120 bytes total, 4 T3D structures, each contains 3 10-byte reals: X, Y, Z
			byte[] frame = reader.ReadBytes(120); // !!!

			// map name
			string mapName = reader.ReadShortString(255);

			// left stereo photo file name
			string leftStereoFileName = reader.ReadShortString(255);

			// right stereo photo file name
			string rightStereoFileName = reader.ReadShortString(255);

			// HACK: skip other data
			byte[] otherData = reader.ReadBytes((int)(headerSize - 910));

			return new Header(scale, objectCount, mapName, leftStereoFileName, rightStereoFileName);
		}

		private static void ReadLayerList(BinaryReader reader)
		{
			// List size in bytes
			uint size = reader.ReadUInt32();

			// List header size in bytes
			uint headerSize = reader.ReadUInt32();
			if (headerSize != 13) throw new System.IO.InvalidDataException("Invalid file format: unsupported layer list header size");

			// Normal layers count
			uint layersCount = reader.ReadUInt32();

			// Reserved
			int status = reader.ReadInt32();

			// Service layers count
			uint serviceLayersCount = (uint)(-reader.ReadInt32());
			// First service layer number
			int firstServiceLayerNumber = -(int)serviceLayersCount + 1;

			// Reserved
			reader.ReadByte();

			IDictionary<int, int> layerIDs = new Dictionary<int, int>((int)(layersCount + serviceLayersCount));
			IDictionary<int, LayerVisibleStatus> layerVisibility = new Dictionary<int, LayerVisibleStatus>((int)(layersCount + serviceLayersCount));

			for (int layerNumber = firstServiceLayerNumber; layerNumber <= layersCount; layerNumber++)
			{
				// Layer descriptor size in bytes
				uint layerDescriptorSize = reader.ReadUInt32();

				// Layer status
				int layerStatus = reader.ReadInt32();

				// !! Add to array of visibility status
				layerVisibility[layerNumber] = (LayerVisibleStatus)((layerStatus >> 16) & 0xFF);

				// Layer ID
				int layerID = reader.ReadInt32();

				// !! Add to array of IDs
				layerIDs[layerNumber] = layerID;

				// Min scale
				int minScale = reader.ReadInt32();

				// Max scale
				int maxScale = reader.ReadInt32();

				// Pen color
				int penColor = reader.ReadInt32();

				// Pen width
				int penWidth = reader.ReadInt32();

				// Brush color
				int brushColor = reader.ReadInt32();

				// Font color
				int fontColor = reader.ReadInt32();

				// Font size
				int fontSize = reader.ReadInt32();

				// Pen style
				byte penStyle = reader.ReadByte();

				// Brush style
				byte brushStyle = reader.ReadByte();

				// Font style
				byte fontStyle = reader.ReadByte();

				// Layer name
				string layerName = reader.ReadShortString();

				// Font name
				string fontName = reader.ReadShortString();

				// Reserved
				reader.ReadInt32();

				// Parameter bit array length
				uint parameterAvailableLength = reader.ReadUInt32();

				// Parameters bit array
				BitArray parameterAvailable = new BitArray(reader.ReadBytes((int)(parameterAvailableLength)));

				// Layer's symbol number in symbol library
				uint symbolNumber = reader.ReadUInt32();

				// Reserved
				string format = reader.ReadShortString();

				// Layer reference counter
				uint layerReferenceCounter = reader.ReadUInt32();

				uint penWidth100 = reader.ReadUInt32();
				uint fontSize10 = reader.ReadUInt32();

				// HACK: skip other data
				byte[] otherData = reader.ReadBytes(48);

				if ((int)layerDescriptorSize != (
					63 +
					layerName.Length + 1 +
					fontName.Length + 1 +
					format.Length + 1 +
					(int)(parameterAvailableLength) +
					otherData.Length))
					throw new System.IO.InvalidDataException("Invalid file format: invalid layer descriptor size or content");
			}
		}

		private static void ReadParameterList(BinaryReader reader)
		{
			// List size in bytes
			uint size = reader.ReadUInt32();

			// List header size in bytes
			uint headerSize = reader.ReadUInt32();
			if (headerSize != 13) throw new System.IO.InvalidDataException("Invalid file format: unsupported parameter list header size");

			// Normal parameters count
			uint parametersCount = reader.ReadUInt32();

			// Reserved
			int status = reader.ReadInt32();

			// Service parameters count
			uint serviceParametersCount = (uint)(-reader.ReadInt32());
			// First service parameter number
			int firstServiceParameterNumber = -(int)serviceParametersCount + 1;

			// Reserved
			reader.ReadByte();

			for (int parameterNumber = firstServiceParameterNumber; parameterNumber <= parametersCount; parameterNumber++)
			{
				// Parameter descriptor size in bytes
				uint parameterDescriptorSize = reader.ReadUInt32();
				reader.ReadBytes((int)parameterDescriptorSize);
			}
		}

		private static void ReadSymbolPrimitive(BinaryReader reader)
		{
			char kind = (char)reader.ReadByte();
			
			byte group = reader.ReadByte();

			byte penStyle = reader.ReadByte();

			byte brushStyle = reader.ReadByte();

			uint penColor = reader.ReadUInt32();

			uint penWidth = reader.ReadUInt32();

			uint brushColor = reader.ReadUInt32();

			int x1 = reader.ReadInt32();
			int y1 = reader.ReadInt32();
			int x2 = reader.ReadInt32();
			int y2 = reader.ReadInt32();
		}

		private static void ReadSymbol(BinaryReader reader)
		{
			// Symbol size in bytes
			uint size = reader.ReadUInt32();

			// Symbol header size in bytes
			uint headerSize = reader.ReadUInt32();
			if (headerSize != 24) throw new System.IO.InvalidDataException("Invalid file format: unsupported symbol header size");

			uint id = reader.ReadUInt32();

			uint primitivesCount = reader.ReadUInt32();

			// Symbol length (in micrometers)
			uint length = reader.ReadUInt32();

			// Symbol kind
			uint kind = reader.ReadUInt32();

			// Symbol height (in micrometers)
			uint height = reader.ReadUInt32();

			for (uint primitiveNumber = 0; primitiveNumber < primitivesCount; primitiveNumber++)
			{
				ReadSymbolPrimitive(reader);
			}

			// HACK
			reader.ReadBytes((int)primitivesCount);
		}

		private static void ReadSymbolList(BinaryReader reader)
		{
			// List size in bytes
			uint size = reader.ReadUInt32();

			uint symbolsCount = reader.ReadUInt32();

			for (uint symbolNumber = 1; symbolNumber <= symbolsCount; symbolNumber++)
			{
				ReadSymbol(reader);
			}
		}

		private static void ReadObjectList(BinaryReader reader, uint objectCount)
		{
			for (int objectNumber = 0; objectNumber < objectCount; objectNumber++)
			{
				// Object size in bytes
				uint objectSize = reader.ReadUInt32();

				// Unknown reserved 4 bytes!
				int unknownReserved = reader.ReadInt32();

				// Point storage format
				ushort format = reader.ReadUInt16();

				// Object's point count
				int pointCount = reader.ReadInt32();

				// LayerID - 4-byte integer
				int layerID = reader.ReadInt32();

				// Reserved (Kind)
				int kind = reader.ReadInt32();

				// Layer index in layer list
				int layerIndex = reader.ReadInt32();

				// Object id
				int objectId = reader.ReadInt32();

				// Object status:
				// 1 - hidden, 2 - deleted, 4 - marked
				int objectStatus = reader.ReadInt32();

				// Reserved (Where)
				int where = reader.ReadInt32();

				// Object scale
				int objectScale = reader.ReadInt32();

				// Reserved (Group)
				int group = reader.ReadInt32();

				// Reserved (Parent)
				int parent = reader.ReadInt32();

				// Symbol orientation
				int symbolOrientation = reader.ReadInt32();

				// Parameter string
				int parameterStringLength = reader.ReadInt32();
				string parameterString = System.Text.Encoding.Default.GetString(reader.ReadBytes(parameterStringLength));

				// Parse parameter string
				IDictionary<int, string> parameters = new Dictionary<int, string>();

				int i = 0;
				while (i < parameterString.Length)
				{
					if (parameterString[i] == (char)(1))
					{
						int splitterPosition = parameterString.IndexOf((char)(3), i + 1);
						if (splitterPosition < 0) continue;
						int endPosition = parameterString.IndexOf((char)(2), splitterPosition + 1);
						if (endPosition < 0) endPosition = parameterString.Length;
						int parameterNumber = int.Parse(parameterString.Substring(i + 1, splitterPosition - (i + 1)));
						string parameterValue = parameterString.Substring(splitterPosition + 1, endPosition - (splitterPosition + 1));
						parameters.Add(parameterNumber, parameterValue);
						i = endPosition;
					}
					i++;
				}

				// Read coordinates
				for (int pointNumber = 0; pointNumber < pointCount; pointNumber++)
				{
					// Point status
					int pointStatus = reader.ReadInt32();

					double x = reader.ReadExtended();
					double y = reader.ReadExtended();
					double z = reader.ReadExtended();
				}
			}
		}
	}
}

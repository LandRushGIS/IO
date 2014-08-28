using Stream = System.IO.Stream;
using BinaryReader = System.IO.BinaryReader;
using System.Collections.Generic;

namespace LandRush.IO.Dmf
{
	static class BinaryReaderUtils
	{
		public static string ReadStringInBuffer(this BinaryReader reader, int maxStringLength)
		{
			string result = reader.ReadString();
			reader.ReadBytes(maxStringLength - result.Length);
			return result;
		}

		[System.Security.SecuritySafeCritical]
		public static double ReadExtended(this BinaryReader reader)
		{
			byte[] buffer = reader.ReadBytes(10);
			return 0.0;
		}
	}

	public class Header
	{
		// Знаменатель масштаба карты
		public double Scale;
		// Количество топографических объектов на карте
		public uint ObjectCount;
		// Наименование карты
		public string Name;
		// Имя растрового файла, содержащего левый снимок стереопары или снимок (карту) для моно режима
		public string LeftFile;
		// Имя растрового файла, содержащего правый снимок стереопары
		public string RightFile;
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
			using (var reader = new BinaryReader(stream))
			{
				// ----------------------- READING DMF FORMAT -----------------------
				var header = ReadHeader(reader);
				ReadLayerList(reader);
				ReadParameterList(reader);
				ReadSymbolList(reader);
				ReadObjectList(reader);
			}
		}

		private static Header ReadHeader(BinaryReader reader)
		{
			// 32-byte text signature
			// 'GeoSystem DMF, Version 1.10 C'
			byte[] signature = reader.ReadBytes(32);

			// header size - 4-byte integer
			uint headerSize = reader.ReadUInt32();
			if (headerSize != 910) throw new System.IO.IOException("Invalid file format: unsupported header size");

			// map scale - 10-byte real
			byte[] scaleBuffer = reader.ReadBytes(10);

			// object count - 4-byte integer
			uint objectCount = reader.ReadUInt32();

			// units - 4-byte integer
			int units = reader.ReadInt32();

			// status - 4-byte integer
			int status = reader.ReadInt32();

			// frame - 120 bytes total, 4 T3D structures, each contains 3 10-byte reals: X, Y, Z
			byte[] frame = reader.ReadBytes(120); // !!!

			// map name
			string mapName = reader.ReadStringInBuffer(256);

			// left stereo photo file name
			string leftStereoFileName = reader.ReadStringInBuffer(256);

			// right stereo photo file name
			string rightStereoFileName = reader.ReadStringInBuffer(253);

			return new Header();
		}

		private static void ReadLayerList(BinaryReader reader)
		{
			// Layer list size in bytes
			int layerListBytesCount = reader.ReadInt32();

			// Reserved
			int layerListStatus = reader.ReadInt32();

			// Layer count from 1 to layerCount
			int layerCount = reader.ReadInt32();

			// Unknown integer !!!
			int unknown = reader.ReadInt32();

			// Minimal service layer number
			int minServiceLayerNumber = reader.ReadInt32();

			// Reserved
			byte reserved = reader.ReadByte();

			IDictionary<int, int> layerIDs = new Dictionary<int, int>(layerCount - minServiceLayerNumber);
			IDictionary<int, LayerVisibleStatus> layerVisibility = new Dictionary<int, LayerVisibleStatus>(layerCount - minServiceLayerNumber);

			for (int layerIndex = minServiceLayerNumber + 1; layerIndex <= layerCount; layerIndex++)
			{
				// Layer descriptor structure size in bytes
				int layerDescriptorSize = reader.ReadInt32();

				// Layer status
				int layerStatus = reader.ReadInt32();

				// !! Add to array of visibility status
				layerVisibility[layerIndex] = (LayerVisibleStatus)((layerStatus >> 16) & 0xFF);

				// Layer ID
				int layerID = reader.ReadInt32();

				// !! Add to array of IDs
				layerIDs[layerIndex] = layerID;

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
				string layerName = reader.ReadString();

				// Font name
				string fontName = reader.ReadString();

				// Skip another data
				reader.ReadBytes(layerDescriptorSize - (39 + layerName.Length + 1 + fontName.Length + 1));
			}
		}

		private static void ReadParameterList(BinaryReader reader)
		{
			// Parameter list size in bytes
			int parameterListBytesCount = reader.ReadInt32();

			int parameterListHeaderBytesCount = reader.ReadInt32();

			int parameterCount = reader.ReadInt32();

			int parameterListStatus = reader.ReadInt32();

			int minServiceParameterNumber = reader.ReadInt32();

			byte reserved2 = reader.ReadByte();

			for (int parameterNumber = 0; parameterNumber < parameterCount + (-minServiceParameterNumber); parameterNumber++)
			{
				int parameterBytesCount = reader.ReadInt32();
				reader.ReadBytes(parameterBytesCount);
			}
		}

		private static void ReadSymbolList(BinaryReader reader)
		{
			//
		}

		private static void ReadObjectList(BinaryReader reader)
		{
			//
		}
	}
}

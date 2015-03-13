using BinaryReader = System.IO.BinaryReader;
using BitArray = System.Collections.BitArray;
using BitConverter = System.BitConverter;
using Int16 = System.Int16;
using UInt64 = System.UInt64;
using Stream = System.IO.Stream;
using System.Collections.Generic;
using Geometries = GeoAPI.Geometries;
using NetTopologySuite.Geometries;

namespace LandRush.IO.DMF
{
	public static class Reader
	{
		public class Map
		{
			public Map(
				string name,
				double scale,
				IList<Layer> layers)
			{
				this.name = name;
				this.scale = scale;
				this.layers = layers;
			}

			public string Name
			{
				get { return this.name; }
			}

			public double Scale
			{
				get { return this.scale; }
			}

			public IList<Layer> Layers
			{
				get { return this.layers; }
			}

			private string name;
			private double scale;
			private IList<Layer> layers;
		}

		internal struct LayerInfo
		{
			public int Id;
			public int Index;
			public string Name;
			public State State;
			public int MinScale;
			public int MaxScale;
			public Pen Pen;
			public Brush Brush;
			public uint SymbolNumber;
			public Layer.LayerObjectsType ObjectsType;
			public BitArray Attributes;
		}

		internal struct AttributeInfo
		{
			public int Id;
			public string Name;
			public System.Type ValueType;
			public State State;
			public int MinScale;
			public int MaxScale;
			public Brush Brush;
			public Font Font;
			public uint SymbolNumber;
			public string Format;
		}

		internal struct AttributesLists
		{
			public AttributesLists(IList<AttributeInfo> attributes, IList<AttributeInfo> serviceAttributes)
			{
				this.Attributes = attributes;
				this.ServiceAttributes = serviceAttributes;
			}

			public readonly IList<AttributeInfo> Attributes;
			public readonly IList<AttributeInfo> ServiceAttributes;
		}

		internal struct FeatureInfo
		{
			public int LayerIndex;
			public int Id;
			public float Scale;
			public int SymbolOrientation;
			public IList<IList<Geometries.Coordinate>> CoordinatesLists;
			public IDictionary<int, string> Attributes;
			public bool IsHidden;
			public bool IsDeleted;
			public bool IsMarked;
		}

		internal struct PrimitiveRecord
		{
			public byte Type;
			public byte GroupNumber;
			public Pen Pen;
			public Brush Brush;
			public Point2D FirstPoint;
			public Point2D SecondPoint;
		}

		private static Geometries.IPoint BuildPoint(IList<Geometries.Coordinate> coordinateList)
		{
			if (coordinateList.Count != 1)
			{
				throw new System.IO.InvalidDataException("Invalid feature geometry: at least 1 coordinate required to build point");
			}

			return new NetTopologySuite.Geometries.Point(coordinateList[0]);
		}

		private static Geometries.ILineString BuildLineString(IList<Geometries.Coordinate> coordinateList)
		{
			if (coordinateList.Count < 2)
			{
				throw new System.IO.InvalidDataException("Invalid feature geometry: at least 2 coordinates required to build line string");
			}

			Geometries.Coordinate[] coordinates = new Geometries.Coordinate[coordinateList.Count];
			coordinateList.CopyTo(coordinates, 0);
			return new NetTopologySuite.Geometries.LineString(coordinates);
		}

		private static Geometries.ILinearRing BuildLinearRing(IList<Geometries.Coordinate> coordinateList)
		{
			if (coordinateList.Count < 4)
			{
				throw new System.IO.InvalidDataException("Invalid feature geometry: at least 4 coordinates required to build linear ring");
			}

			Geometries.Coordinate[] coordinates = new Geometries.Coordinate[coordinateList.Count];
			coordinateList.CopyTo(coordinates, 0);
			return new NetTopologySuite.Geometries.LinearRing(coordinates);
		}

		// assumed that simple polygon is a polygon without holes
		private static Geometries.IPolygon BuildSimplePolygon(IList<Geometries.Coordinate> coordinateList)
		{
			Geometries.ILinearRing shell = BuildLinearRing(coordinateList);
			return new NetTopologySuite.Geometries.Polygon(shell);
		}

		private static Geometries.IMultiPoint BuildMultiPoint(IList<IList<Geometries.Coordinate>> coordinateLists)
		{
			Geometries.IPoint[] points = new Geometries.IPoint[coordinateLists.Count];
			for (int i = 0; i < coordinateLists.Count; i++)
			{
				points[i] = BuildPoint(coordinateLists[i]);
			}
			return new NetTopologySuite.Geometries.MultiPoint(points);
		}

		private static Geometries.IMultiLineString BuildMultiLineString(IList<IList<Geometries.Coordinate>> coordinateLists)
		{
			Geometries.ILineString[] lineStrings = new Geometries.ILineString[coordinateLists.Count];
			for (int i = 0; i < coordinateLists.Count; i++)
			{
				lineStrings[i] = BuildLineString(coordinateLists[i]);
			}
			return new NetTopologySuite.Geometries.MultiLineString(lineStrings);
		}

		// assumed that complex polygon is a multipolygon or a polygon with holes
		private static Geometries.IGeometry BuildComplexPolygon(IList<IList<Geometries.Coordinate>> coordinateLists)
		{
			// create rings from coordinate lists
			Geometries.ILinearRing[] rings = new Geometries.ILinearRing[coordinateLists.Count];
			for (int i = 0; i < coordinateLists.Count; i++)
			{
				rings[i] = BuildLinearRing(coordinateLists[i]);
			}

			// build ring hierarchy
			Dictionary<Geometries.ILinearRing, Geometries.ILinearRing> parentByRing = new Dictionary<Geometries.ILinearRing, Geometries.ILinearRing>();
			for (int i = 0; i < rings.Length; i++)
			{
				for (int j = 0; j < rings.Length; j++)
				{
					Geometries.ILinearRing ringA = rings[i];
					Geometries.ILinearRing ringB = rings[j];

					if (!ringA.Equals(ringB) && new Polygon(ringA).Contains(new Polygon(ringB)))
					{
						parentByRing[ringB] = ringA;
					}
				}
			}

			// only leaf rings are holes, group them by parent ring
			Dictionary<Geometries.ILinearRing, ISet<Geometries.ILinearRing>> holesByShell = new Dictionary<Geometries.ILinearRing, ISet<Geometries.ILinearRing>>();
			List<Geometries.ILinearRing> ringsToHandle = new List<Geometries.ILinearRing>();
			for (int i = 0; i < rings.Length; i++)
			{
				if (parentByRing.ContainsKey(rings[i]) && !parentByRing.ContainsValue(rings[i]))
				{
					Geometries.ILinearRing ring = rings[i];
					Geometries.ILinearRing parent = parentByRing[ring];

					if (!holesByShell.ContainsKey(parent))
					{
						holesByShell[parent] = new HashSet<Geometries.ILinearRing>();
					}

					holesByShell[parent].Add(ring);
				}

				ringsToHandle.Add(rings[i]);
			}

			// create a polygon for each of the shells
			IList<Geometries.IPolygon> polygonList = new List<Geometries.IPolygon>();
			foreach (var shell in holesByShell.Keys)
			{
				ISet<Geometries.ILinearRing> holeSet = holesByShell[shell];
				Geometries.ILinearRing[] holes = new Geometries.ILinearRing[holeSet.Count];
				holeSet.CopyTo(holes, 0);
				polygonList.Add(new NetTopologySuite.Geometries.Polygon(shell, holes));

				ringsToHandle.Remove(shell);
				foreach (var hole in holes)
				{
					ringsToHandle.Remove(hole);
				}
			}

			// create a simple polygon for each of the unhandled rings
			foreach (var ring in ringsToHandle)
			{
				polygonList.Add(new NetTopologySuite.Geometries.Polygon(ring));
			}

			Geometries.IPolygon[] polygons = new Geometries.IPolygon[polygonList.Count];
			polygonList.CopyTo(polygons, 0);
			return new NetTopologySuite.Geometries.MultiPolygon(polygons);
		}

		private static Geometries.OgcGeometryType DetectGeometryType(IList<Geometries.Coordinate> coordinateList)
		{
			switch (coordinateList.Count)
			{
				case 1:
				{
					return Geometries.OgcGeometryType.Point;
				}
				case 2:
				case 3:
				{
					return Geometries.OgcGeometryType.LineString;
				}
				default:
				// if first and last points in coordinate list are the same, we are dealing with a polygon
				// if not, it is a polyline
				{
					return coordinateList[0].Equals(coordinateList[coordinateList.Count - 1]) ?
						Geometries.OgcGeometryType.Polygon : Geometries.OgcGeometryType.LineString;
				}
			}
		}

		private static Geometries.IGeometry BuildGeometry(IList<IList<Geometries.Coordinate>> coordinateLists)
		{
			switch (coordinateLists.Count)
			{
				case 0:
				{
					return null;
				}
				case 1:
				{
					IList<Geometries.Coordinate> coordinateList = coordinateLists[0];
					switch (DetectGeometryType(coordinateList))
					{
						case Geometries.OgcGeometryType.Point:
						{
							return BuildPoint(coordinateList);
						}
						case Geometries.OgcGeometryType.LineString:
						{
							return BuildLineString(coordinateList);
						}
						case Geometries.OgcGeometryType.Polygon:
						{
							return BuildSimplePolygon(coordinateList);
						}
					}
					break;
				}
				default:
				{
					IList<Geometries.Coordinate> firstCoordinateList = coordinateLists[0];
					switch (DetectGeometryType(firstCoordinateList))
					{
						case Geometries.OgcGeometryType.Point:
						{
							return BuildMultiPoint(coordinateLists);
						}
						case Geometries.OgcGeometryType.LineString:
						{
							return BuildMultiLineString(coordinateLists);
						}
						case Geometries.OgcGeometryType.Polygon:
						{
							return BuildComplexPolygon(coordinateLists);
						}
					}
					break;
				}
			}

			throw new System.InvalidOperationException("Error building feature geometry");
		}

		public static Map Read(Stream stream)
		{
			Signature signature = ReadSignature(stream);
			if (!supportedVersions.Contains(signature.Version))
			{
				throw new System.NotSupportedException("Version " + signature.Version + " is not supported");
			}

			Stream inputStream = !signature.IsCompressed ?
					stream :
					new LandRush.IO.Compression.ZLibStream(stream, System.IO.Compression.CompressionMode.Decompress, true);
			using (var reader = new BinaryReader(inputStream))
			{
				Header header = ReadHeader(reader);
				IList<LayerInfo> layersInfo = ReadLayerList(reader);
				AttributesLists attributesLists = ReadAttributesList(reader);
				IList<Symbol> symbols = ReadSymbolList(reader);
				if (signature.Version.Equals(new Version(1, 15)))
				{
					ReadAccessPolicies(reader);
				}
				ICollection<FeatureInfo> featuresInfo = ReadFeatureList(reader, header.FeatureCount);

				// create service attributes library
				IList<Attribute> serviceAttributes = new List<Attribute>();
				foreach (AttributeInfo attributeInfo in attributesLists.ServiceAttributes)
				{
					serviceAttributes.Add(new Attribute(
						attributeInfo.Id,
						attributeInfo.Name,
						attributeInfo.ValueType,
						attributeInfo.State,
						attributeInfo.MinScale,
						attributeInfo.MaxScale,
						attributeInfo.Brush,
						attributeInfo.Font,
						attributeInfo.SymbolNumber > 0 ? symbols[(int)attributeInfo.SymbolNumber - 1] : null,
						attributeInfo.Format));
				}

				// create attributes library
				IList<Attribute> attributes = new List<Attribute>();
				foreach (AttributeInfo attributeInfo in attributesLists.Attributes)
				{
					attributes.Add(new Attribute(
						attributeInfo.Id,
						attributeInfo.Name,
						attributeInfo.ValueType,
						attributeInfo.State,
						attributeInfo.MinScale,
						attributeInfo.MaxScale,
						attributeInfo.Brush,
						attributeInfo.Font,
						attributeInfo.SymbolNumber > 0 ? symbols[(int)attributeInfo.SymbolNumber - 1] : null,
						attributeInfo.Format));
				}

				// sort features by layers
				// TODO: the following code does not process layers containing objects that are not features
				// TODO: thus such layers are contained in featuresByLayerId dictionary
				// TODO: but their set of features does not exist
				IDictionary<int, ISet<Feature>> featuresByLayerIndex = new Dictionary<int, ISet<Feature>>();
				foreach (FeatureInfo featureInfo in featuresInfo)
				{
					if (!featuresByLayerIndex.ContainsKey(featureInfo.LayerIndex))
					{
						featuresByLayerIndex[featureInfo.LayerIndex] = new HashSet<Feature>();
					}

					// set geometry for the feature
					Geometries.IGeometry geometry = BuildGeometry(featureInfo.CoordinatesLists);
					if (geometry != null)
					{
						// create attributes list for the feature
						IDictionary<Attribute, object> attributeValues = new Dictionary<Attribute, object>();
						foreach (KeyValuePair<int, string> attributeValueString in featureInfo.Attributes)
						{
							Attribute attribute =
								attributeValueString.Key <= 0 ?
									serviceAttributes[attributeValueString.Key + serviceAttributes.Count - 1] :
									attributes[attributeValueString.Key - 1];

							attributeValues[attribute] = parsers[attribute.ValueType](attributeValueString.Value);
						}

						featuresByLayerIndex[featureInfo.LayerIndex].Add(new Feature(
							featureInfo.Id,
							featureInfo.Scale,
							featureInfo.SymbolOrientation,
							geometry,
							attributeValues,
							featureInfo.IsHidden,
							featureInfo.IsDeleted,
							featureInfo.IsMarked));
					}
				}

				IList<Layer> layers = new List<Layer>();
				foreach (LayerInfo layerInfo in layersInfo)
				{
					ISet<Attribute> layerAttributes = new HashSet<Attribute>();

					for (int i = 0; i < System.Math.Min(11, layerInfo.Attributes.Count); i++)
					{
						if (layerInfo.Attributes[i] == true)
						{
							layerAttributes.Add(serviceAttributes[i]);
						}
					}

					for (int i = 11; i < layerInfo.Attributes.Count; i++)
					{
						if (layerInfo.Attributes[i] == true)
						{
							layerAttributes.Add(attributes[i - 11]);
						}
					}

					layers.Add(new Layer(
						layerInfo.Id,
						layerInfo.Index,
						layerInfo.Name,
						layerInfo.State,
						layerInfo.MinScale,
						layerInfo.MaxScale,
						layerInfo.Pen,
						layerInfo.Brush,
						layerInfo.SymbolNumber > 0 ? symbols[(int)layerInfo.SymbolNumber - 1] : null,
						layerInfo.ObjectsType,
						layerAttributes,
						featuresByLayerIndex.ContainsKey(layerInfo.Index) ? featuresByLayerIndex[layerInfo.Index] : new HashSet<Feature>()));
				}

				return new Map(header.Name, header.Scale, layers);
			}
		}

		private static Signature ReadSignature(Stream stream)
		{
			// 32-byte text signature, for example:
			// 'GeoSystem DMF, Version 1.10 C  \x1A'
			byte[] signature = new byte[32];
			int result = stream.Read(signature, 0, signature.Length);
			if (result != signature.Length)
			{
				throw new System.IO.InvalidDataException("Invalid file format: expected 32-byte signature");
			}
			string versionString = System.Text.Encoding.ASCII.GetString(signature, 23, 4).Trim();
			string[] versionStringComponents = versionString.Split('.');
			uint majorVersion = uint.Parse(versionStringComponents[0]);
			uint minorVersion = uint.Parse(versionStringComponents[1]);
			bool isCompressed = (signature[28] == (byte)'C');

			return new Signature(new Version(majorVersion, minorVersion), isCompressed);
		}

		private static Header ReadHeader(BinaryReader reader)
		{
			// header size - 4-byte integer
			uint headerSize = reader.ReadUInt32();
			if (headerSize < 910)
			{
				throw new System.IO.InvalidDataException("Invalid file format: unsupported header size");
			}

			// map scale - 10-byte real
			double scale = reader.ReadExtended();

			// features count - 4-byte integer
			uint featuresCount = reader.ReadUInt32();

			// units - 4-byte integer - reserved
			int units = reader.ReadInt32();

			// status - 4-byte integer - reserved
			int status = reader.ReadInt32();

			// frame - 120 bytes total, 4 T3D structures, each contains 3 10-byte reals: X, Y, Z
			// TODO: implement parsing
			byte[] frame = reader.ReadBytes(120);

			// map name
			string mapName = reader.ReadShortString(255);

			// left stereo photo file name
			string leftStereoFileName = reader.ReadShortString(255);

			// right stereo photo file name
			string rightStereoFileName = reader.ReadShortString(255);

			// skip other data
			byte[] otherData = reader.ReadBytes((int)(headerSize - 910));

			return new Header(scale, featuresCount, mapName, leftStereoFileName, rightStereoFileName);
		}

		private static IList<LayerInfo> ReadLayerList(BinaryReader reader)
		{
			// TODO: use size to check count of bytes read
			// list size in bytes - 4-byte integer
			uint size = reader.ReadUInt32();

			// list header size in bytes - 4-byte integer
			uint headerSize = reader.ReadUInt32();
			if (headerSize != layerListHeaderSize)
			{
				throw new System.IO.InvalidDataException("Invalid file format: unsupported layer list header size");
			}

			// normal layers count - 4-byte integer
			uint layersCount = reader.ReadUInt32();

			// status - 4-byte integer - reserved
			reader.ReadInt32();

			// service layers count
			uint serviceLayersCount = (uint)(-reader.ReadInt32());
			// first service layer number
			int firstServiceLayerNumber = -(int)serviceLayersCount + 1;

			// reserved - 1 byte
			reader.ReadByte();

			IList<LayerInfo> layers = new List<LayerInfo>();
			for (int layerNumber = firstServiceLayerNumber; layerNumber <= layersCount; layerNumber++)
			{
				layers.Add(ReadLayer(reader, layerNumber));
			}

			return layers;
		}

		private static LayerInfo ReadLayer(BinaryReader reader, int layerIndex)
		{
			LayerInfo layerInfo = new LayerInfo();
			layerInfo.Index = layerIndex;

			// layer descriptor size in bytes - 4-byte integer
			uint layerDescriptorSize = reader.ReadUInt32();

			// layer status - 4-byte integer
			bool isPolygon = (reader.ReadByte() & (byte)1) == 1;
			reader.ReadByte();
			switch (reader.ReadByte())
			{
				case 0:
				{
					layerInfo.State = State.Editable;
					break;
				}
				case 1:
				{
					layerInfo.State = State.Markable;
					break;
				}
				case 2:
				{
					layerInfo.State = State.Visible;
					break;
				}
				case 3:
				{
					layerInfo.State = State.Invisible;
					break;
				}
				default:
				{
					throw new System.IO.InvalidDataException("Invalid file content: unsupported layer state");
				}
			}
			switch (reader.ReadByte())
			{
				case 1:
				{
					if (isPolygon)
					{
						layerInfo.ObjectsType = Layer.LayerObjectsType.Polygon;
					}
					else
					{
						layerInfo.ObjectsType = Layer.LayerObjectsType.Polyline;
					}
					break;
				}
				case 2:
				{
					if (isPolygon)
					{
						layerInfo.ObjectsType = Layer.LayerObjectsType.SmoothPolygon;
					}
					else
					{
						layerInfo.ObjectsType = Layer.LayerObjectsType.SmoothPolyline;
					}
					break;
				}
				case 4:
				{
					layerInfo.ObjectsType = Layer.LayerObjectsType.Symbol;
					break;
				}
				case 0: case 3: case 5: case 6: case 7: case 8:
				{
					layerInfo.ObjectsType = Layer.LayerObjectsType.Unknown;
					break;
				}
				default:
				{
					layerInfo.ObjectsType = Layer.LayerObjectsType.Unknown;
					break;
				}
			}

			// layer ID - 4-byte integer
			layerInfo.Id = reader.ReadInt32();

			// min scale - 4-byte integer
			layerInfo.MinScale = reader.ReadInt32();

			// max scale - 4-byte integer
			layerInfo.MaxScale = reader.ReadInt32();

			// pen color - 4-byte integer
			int penColor = reader.ReadInt32();

			// pen width - 4-byte integer
			int penWidth = reader.ReadInt32();

			// brush color - 4-byte integer
			int brushColor = reader.ReadInt32();

			// font color - 4-byte integer - reserved
			reader.ReadInt32();

			// font size - 4-byte integer - reserved
			reader.ReadInt32();

			// pen style - 1 byte
			Pen.PenStyle penStyle = (Pen.PenStyle)(reader.ReadByte());

			// brush style - 1 byte
			Brush.BrushStyle brushStyle = (Brush.BrushStyle)(reader.ReadByte());

			// font style - 1 byte - reserved
			reader.ReadByte();

			// layer name
			layerInfo.Name = reader.ReadShortString();

			// font name - reserved
			string fontName = reader.ReadShortString();

			// 4-byte integer - reserved
			reader.ReadInt32();

			// attribute bit array length - 4-byte integer
			uint attributesArrayLength = reader.ReadUInt32();

			// attributes bit array
			layerInfo.Attributes = new BitArray(reader.ReadBytes((int)(attributesArrayLength)));

			// layer symbol number in symbols library - 4-byte integer
			layerInfo.SymbolNumber = reader.ReadUInt32();

			// format - reserved
			string format = reader.ReadShortString();

			// layer reference counter - 4-byte integer
			reader.ReadUInt32();

			// addition to pen width - 4-byte integer
			int penWidth100 = reader.ReadInt32();

			// addition to font size - 4-byte integer - reserved
			reader.ReadInt32();

			layerInfo.Pen = new Pen(new Color(penColor), (penWidth*10 + penWidth100), penStyle);
			layerInfo.Brush = new Brush(new Color(brushColor), brushStyle);

			uint bytesRead =
				63u + // size of elements with fixed size
				(uint)layerInfo.Name.Length + 1 +
				(uint)fontName.Length + 1 +
				(uint)format.Length + 1 +
				attributesArrayLength;

			if (layerDescriptorSize < bytesRead)
			{
				throw new System.IO.InvalidDataException("Invalid file format: invalid layer descriptor size or content");
			}

			// skip other data
			byte[] otherData = reader.ReadBytes((int)(layerDescriptorSize - bytesRead));

			return layerInfo;
		}

		private static AttributesLists ReadAttributesList(BinaryReader reader)
		{
			// TODO: use size to check count of bytes read
			// list size in bytes - 4-byte integer
			uint size = reader.ReadUInt32();

			// list header size in bytes - 4-byte integer
			uint headerSize = reader.ReadUInt32();
			if (headerSize != attributesListHeaderSize)
			{
				throw new System.IO.InvalidDataException("Invalid file format: unsupported attributes list header size");
			}

			// normal attributes count - 4-byte integer
			uint attributesCount = reader.ReadUInt32();

			// status - 4-byte integer - reserved
			reader.ReadInt32();

			// service attributes count - 4-byte integer
			uint serviceAttributesCount = (uint)(-reader.ReadInt32());
			// first service attribute number
			int firstServiceAttributeNumber = -(int)serviceAttributesCount + 1;

			// reserved - 4-byte integer
			reader.ReadByte();

			IList<AttributeInfo> serviceAttributes = new List<AttributeInfo>();
			for (int attributeNumber = firstServiceAttributeNumber; attributeNumber <= 0; attributeNumber++)
			{
				serviceAttributes.Add(ReadAttribute(reader));
			}

			IList<AttributeInfo> attributes = new List<AttributeInfo>();
			for (int attributeNumber = 1; attributeNumber <= attributesCount; attributeNumber++)
			{
				attributes.Add(ReadAttribute(reader));
			}

			return new AttributesLists(attributes, serviceAttributes);
		}

		private static AttributeInfo ReadAttribute(BinaryReader reader)
		{
			AttributeInfo attributeInfo = new AttributeInfo();

			// attribute descriptor size in bytes - 4-byte integer
			uint attributeDescriptorSize = reader.ReadUInt32();

			// attribute status - 4-byte integer
			reader.ReadBytes(2);

			attributeInfo.State = State.Unknown;
			switch (reader.ReadByte())
			{
				case 0:
				{
					attributeInfo.State = State.Editable;
					break;
				}
				case 1:
				{
					attributeInfo.State = State.Markable;
					break;
				}
				case 2:
				{
					attributeInfo.State = State.Visible;
					break;
				}
				case 3:
				{
					attributeInfo.State = State.Invisible;
					break;
				}
				default:
				{
					throw new System.IO.InvalidDataException("Invalid file content: unsupported attribute state");
				}
			}

			if (!valueTypeByCode.TryGetValue(reader.ReadByte(), out attributeInfo.ValueType))
			{
				throw new System.IO.InvalidDataException("Invalid file content: unsupported attribute value type");
			}

			// attribute ID - 4-byte integer
			attributeInfo.Id = reader.ReadInt32();

			// min scale - 4-byte integer
			attributeInfo.MinScale = reader.ReadInt32();

			// max scale - 4-byte integer
			attributeInfo.MaxScale = reader.ReadInt32();

			// pen color - 4-byte integer - reserved
			reader.ReadInt32();

			// pen width - 4-byte integer - reserved
			reader.ReadInt32();

			// brush color - 4-byte integer
			int brushColor = reader.ReadInt32();

			// font color - 4-byte integer
			int fontColor = reader.ReadInt32();

			// font size - 4-byte integer
			int fontSize = reader.ReadInt32();

			// pen style - 1 byte - reserved
			reader.ReadByte();

			// brush style - 1 byte
			Brush.BrushStyle brushStyle = (Brush.BrushStyle)(reader.ReadByte());

			// font style - 1 byte
			byte fontStyle = reader.ReadByte();
			bool bold = ((fontStyle & 0x1) == 0x1);
			bool italic = ((fontStyle & 0x2) == 0x2);
			bool underline = ((fontStyle & 0x4) == 0x4);
			bool strikeOut = ((fontStyle & 0x8) == 0x8);

			// attribute name
			attributeInfo.Name = reader.ReadShortString();

			// font name
			string fontName = reader.ReadShortString();
			string[] nameParts = fontName.Split(':');
			byte charSet = (nameParts.Length < 2) ? CharSets.Default : byte.Parse(nameParts[1]);

			// 4-byte integer - reserved
			reader.ReadInt32();

			// attribute bit array length - 4-byte integer - not in use
			uint attributesArrayLength = reader.ReadUInt32();

			// attributes bit array - not in use
			reader.ReadBytes((int)(attributesArrayLength));

			// attribute symbol number in symbols library - 4-byte integer
			attributeInfo.SymbolNumber = reader.ReadUInt32();

			// format
			attributeInfo.Format = reader.ReadShortString();

			// attribute reference counter - 4-byte integer - reserved
			reader.ReadUInt32();

			// addition to pen width - 4-byte integer
			int penWidth100 = reader.ReadInt32();

			// addition to font size - 4-byte integer
			int fontSize10 = reader.ReadInt32();

			attributeInfo.Brush = new Brush(new Color(brushColor), brushStyle);
			attributeInfo.Font = new Font(
				bold,
				italic,
				underline,
				strikeOut,
				new Color(fontColor),
				(fontSize*10 + fontSize10),
				charSet,
				nameParts[0]);

			uint bytesRead =
				63u + // size of elements with fixed size
				(uint)attributeInfo.Name.Length + 1 +
				(uint)attributeInfo.Font.Name.Length + 1 +
				(uint)attributeInfo.Format.Length + 1 +
				attributesArrayLength;

			if (attributeDescriptorSize < bytesRead)
			{
				throw new System.IO.InvalidDataException("Invalid file format: invalid attribute descriptor size or content");
			}

			// skip other data
			byte[] otherData = reader.ReadBytes((int)(attributeDescriptorSize - bytesRead));

			return attributeInfo;
		}

		private static IList<Symbol> ReadSymbolList(BinaryReader reader)
		{
			// TODO: use size to check count of bytes read
			// list size in bytes - 4-byte integer
			uint size = reader.ReadUInt32();

			// symbols count - 4-byte integer
			uint symbolsCount = reader.ReadUInt32();

			List<Symbol> symbols = new List<Symbol>();
			for (uint symbolNumber = 1; symbolNumber <= symbolsCount; symbolNumber++)
			{
				symbols.Add(ReadSymbol(reader));
			}

			return symbols;
		}

		private static Symbol ReadSymbol(BinaryReader reader)
		{
			// symbol descriptor size in bytes - 4-byte integer
			uint symbolDescriptorSize = reader.ReadUInt32();

			// symbol header size in bytes - 4-byte integer
			uint headerSize = reader.ReadUInt32();
			if (headerSize != symbolHeaderSize)
			{
				throw new System.IO.InvalidDataException("Invalid file format: unsupported symbol header size");
			}

			// id - 4-byte integer - reserved
			reader.ReadUInt32();

			// primitive records count - 4-byte integer
			uint primitiveRecordsCount = reader.ReadUInt32();

			// symbol length (in micrometers) - 4-byte integer
			uint length = reader.ReadUInt32();

			// symbol type - 4-byte integer
			Symbol.SymbolType type;
			if (!symbolTypeByCode.TryGetValue(reader.ReadUInt32(), out type))
			{
				throw new System.IO.InvalidDataException("Invalid file content: unsupported symbol type");
			}

			// symbol height (in micrometers) - 4-byte integer
			uint height = reader.ReadUInt32();

			IList<Primitive> primitives = ReadSymbolPrimitives(reader, primitiveRecordsCount);

			uint bytesRead =
				headerSize + // size of elements with fixed size
				primitiveSize * primitiveRecordsCount;

			if (symbolDescriptorSize < bytesRead)
			{
				throw new System.IO.InvalidDataException("Invalid file format: invalid symbol descriptor size");
			}

			// skip other data
			byte[] otherData = reader.ReadBytes((int)(symbolDescriptorSize - bytesRead));

			return new Symbol(type, length, height, primitives);
		}

		private static IList<PrimitiveRecord> ReadSymbolPrimitiveRecords(BinaryReader reader, uint primitiveRecordsCount)
		{
			IList<PrimitiveRecord> primitiveRecords = new List<PrimitiveRecord>();

			for (uint primitiveRecordNumber = 0; primitiveRecordNumber < primitiveRecordsCount; ++primitiveRecordNumber)
			{
				PrimitiveRecord primitiveRecord = new PrimitiveRecord();

				// primitive type - 1-byte char
				primitiveRecord.Type = reader.ReadByte();

				// primitive group number - 1-byte
				primitiveRecord.GroupNumber = reader.ReadByte();

				// pen style - 1-byte
				Pen.PenStyle penStyle = (Pen.PenStyle)(reader.ReadByte());

				// brush style - 1-byte
				Brush.BrushStyle brushStyle = (Brush.BrushStyle)(reader.ReadByte());

				// pen color - 4-byte integer
				int penColor = reader.ReadInt32();

				// pen width - 4-byte integer
				int penWidth = reader.ReadInt32();

				// brush color - 4-byte integer
				int brushColor = reader.ReadInt32();

				primitiveRecord.Pen = new Pen(new Color(penColor), penWidth, penStyle);
				primitiveRecord.Brush = new Brush(new Color(brushColor), brushStyle);

				int x1 = reader.ReadInt32();
				int y1 = reader.ReadInt32();
				int x2 = reader.ReadInt32();
				int y2 = reader.ReadInt32();

				primitiveRecord.FirstPoint = new Point2D(x1, y1);
				primitiveRecord.SecondPoint = new Point2D(x2, y2);

				primitiveRecords.Add(primitiveRecord);
			}

			return primitiveRecords;
		}

		private static IList<Primitive> ReadSymbolPrimitives(BinaryReader reader, uint primitiveRecordsCount)
		{
			IList<PrimitiveRecord> primitiveRecords = ReadSymbolPrimitiveRecords(reader, primitiveRecordsCount);

			IList<Primitive> primitives = new List<Primitive>();

			int primitiveRecordNumber = 0;
			while (primitiveRecordNumber < primitiveRecords.Count)
			{
				switch ((char)(primitiveRecords[primitiveRecordNumber].Type))
				{
					case 'R':
					{
						primitives.Add(new RectanglePrimitive(
							primitiveRecords[primitiveRecordNumber].GroupNumber,
							primitiveRecords[primitiveRecordNumber].Pen,
							primitiveRecords[primitiveRecordNumber].Brush,
							primitiveRecords[primitiveRecordNumber].FirstPoint,
							primitiveRecords[primitiveRecordNumber].SecondPoint));
						++primitiveRecordNumber;
						break;
					}
					case 'C':
					{
						primitives.Add(new CirclePrimitive(
							primitiveRecords[primitiveRecordNumber].GroupNumber,
							primitiveRecords[primitiveRecordNumber].Pen,
							primitiveRecords[primitiveRecordNumber].Brush,
							primitiveRecords[primitiveRecordNumber].FirstPoint,
							primitiveRecords[primitiveRecordNumber].SecondPoint));
						++primitiveRecordNumber;
						break;
					}
					case 'M':
					{
						primitives.Add(new SemicirclePrimitive(
							primitiveRecords[primitiveRecordNumber].GroupNumber,
							primitiveRecords[primitiveRecordNumber].Pen,
							primitiveRecords[primitiveRecordNumber].Brush,
							primitiveRecords[primitiveRecordNumber].FirstPoint,
							primitiveRecords[primitiveRecordNumber].SecondPoint));
						++primitiveRecordNumber;
						break;
					}
					case 'P':
					{
						byte groupNumber = primitiveRecords[primitiveRecordNumber].GroupNumber;
						Pen pen = primitiveRecords[primitiveRecordNumber].Pen;
						Brush brush = primitiveRecords[primitiveRecordNumber].Brush;
						IList<Point2D> polylinePoints = new List<Point2D>();
						while (primitiveRecordNumber < primitiveRecordsCount
							&& ((char)(primitiveRecords[primitiveRecordNumber].Type) == 'P'))
						{
							PrimitiveRecord record = primitiveRecords[primitiveRecordNumber];
							foreach (Point2D point in new Point2D[] { record.FirstPoint, record.SecondPoint })
							{
								if (point.X == primitivesBreakSign)
								// end of the polyline
								{
									if (polylinePoints.Count > 1)
									// valid polyline consists of more than 1 point
									{
										primitives.Add(new PolylinePrimitive(groupNumber, pen, brush, polylinePoints));
									}
									polylinePoints = new List<Point2D>();
								}
								else
								{
									if (polylinePoints.Count == 0)
									// start new primitive
									{
										groupNumber = record.GroupNumber;
										pen = record.Pen;
										brush = record.Brush;
									}
									polylinePoints.Add(point);
								}
							}
							++primitiveRecordNumber;
						}
						if (polylinePoints.Count > 1)
						{
							primitives.Add(new PolylinePrimitive(groupNumber, pen, brush, polylinePoints));
						}
						break;
					}
					case 'L':
					{
						++primitiveRecordNumber;
						break;
					}
					default:
					{
						throw new System.IO.InvalidDataException("Invalid file content: unknown primitive type");
					}
				}
			}

			return primitives;
		}

		private static void ReadAccessPolicies(BinaryReader reader)
		{
			// access policies list size in bytes - 4-byte integer
			int size = reader.ReadInt32();

			int recordSize = reader.ReadInt32();

			int count = reader.ReadInt32();

			if (size != ((recordSize * count) + 8))
			{
				throw new System.IO.InvalidDataException("Invalid file format: invalid access policy records");
			}

			// read and skip access policy records
			for (int i = 0; i < count; i++)
			{
				reader.ReadBytes(recordSize);
			}
		}
		
		private static ICollection<FeatureInfo> ReadFeatureList(BinaryReader reader, uint featuresCount)
		{
			ICollection<FeatureInfo> features = new List<FeatureInfo>();

			for (int featureNumber = 0; featureNumber < featuresCount; featureNumber++)
			{
				features.Add(ReadFeature(reader));
			}

			return features;
		}

		private static FeatureInfo ReadFeature(BinaryReader reader)
		{
			FeatureInfo featureInfo = new FeatureInfo();

			// object descriptor size in bytes - 4-byte integer
			uint objectDescriptorSize = reader.ReadUInt32();

			// point storage format - 2-byte integer - reserved
			reader.ReadUInt16();

			// header size - 4-byte integer
			uint headerSize = reader.ReadUInt32();
			if (headerSize != featureHeaderSize)
			{
				throw new System.IO.InvalidDataException("Invalid file format: unsupported feature header size");
			}

			// feature points count - 4-byte integer
			int pointsCount = reader.ReadInt32();

			// layer id - 4-byte integer
			reader.ReadInt32();

			// kind - 4-byte integer - reserved
			reader.ReadInt32();

			// layer index in layers list - 4-byte integer
			featureInfo.LayerIndex = reader.ReadInt32();

			// feature id - 4-byte integer
			featureInfo.Id = reader.ReadInt32();

			// feature status - 4-byte integer
			int featureStatus = reader.ReadInt32();
			featureInfo.IsHidden = (((featureStatus >> 1) & 1) == 1);
			featureInfo.IsDeleted = (((featureStatus >> 2) & 1) == 1);
			featureInfo.IsMarked = (((featureStatus >> 4) & 1) == 1);

			// where - 4-byte integer - reserved
			reader.ReadInt32();

			// feature scale - 4-byte single
			featureInfo.Scale = reader.ReadSingle();

			// group - 4-byte integer - reserved
			reader.ReadInt32();

			// parent - 4-byte integer - reserved
			reader.ReadInt32();

			// symbol orientation - 4-byte integer
			featureInfo.SymbolOrientation = reader.ReadInt32();

			// attribute string length - 4-byte integer
			int attributeStringLength = reader.ReadInt32();
			string attributeString = System.Text.Encoding.Default.GetString(reader.ReadBytes(attributeStringLength));

			// parse attribute string
			featureInfo.Attributes = ParseAttributeString(attributeString);

			// read coordinates
			IList<IList<Geometries.Coordinate>> coordinatesLists = new List<IList<Geometries.Coordinate>>();
			List<Geometries.Coordinate> coordinatesList = new List<Geometries.Coordinate>();
			coordinatesLists.Add(coordinatesList);
			
			for (int pointNumber = 0; pointNumber < pointsCount; pointNumber++)
			{
				// point status - 4-byte integer
				reader.ReadInt32();

				double x = reader.ReadExtended();
				double y = reader.ReadExtended();
				double z = reader.ReadExtended();
				
				if (System.Math.Abs(x - breakSign) < 0.000001)
				{
					// create list for a new coordinate sequence
					coordinatesList = new List<Geometries.Coordinate>();
					coordinatesLists.Add(coordinatesList);
				}
				else
				{
					coordinatesList.Add(new Geometries.Coordinate(x, y, z));
				}
			}

			featureInfo.CoordinatesLists = coordinatesLists;

			return featureInfo;
		}

		private static IDictionary<int, string> ParseAttributeString(string attributeString)
		{
			IDictionary<int, string> attributes = new Dictionary<int, string>();
			int attributeNumber = 0;
			string attributeValue = string.Empty;

			int valuePosition = 0;
			int labelPosition = 0;
			int endPosition = 0;

			int i = 0;
			while (i < attributeString.Length)
			{
				if (attributeString[i] == (char)(1))
				{
					valuePosition = attributeString.IndexOf((char)(3), i + 1);
					if (valuePosition < 0)
					{
						throw new System.IO.InvalidDataException("Invalid file format: invalid attribute string");
					}

					attributeNumber = int.Parse(attributeString.Substring(i + 1, valuePosition - (i + 1)));

					// try to find label parameters start position
					labelPosition = attributeString.IndexOf((char)(5), valuePosition + 1);
					if (labelPosition < 0)
					{
						endPosition = attributeString.IndexOf((char)(2), valuePosition + 1);
						if (endPosition < 0)
						{
							throw new System.IO.InvalidDataException("Invalid file format: invalid attribute string");
						}

						attributeValue = attributeString.Substring(valuePosition + 1, endPosition - (valuePosition + 1));
					}
					else
					{
						attributeValue = attributeString.Substring(valuePosition + 1, labelPosition - (valuePosition + 1));

						// TODO: parse label parameters

						endPosition = attributeString.IndexOf((char)(2), labelPosition + 1);
						if (endPosition < 0)
						{
							throw new System.IO.InvalidDataException("Invalid file format: invalid attribute string");
						}						
					}

					attributes.Add(attributeNumber, attributeValue);

					i = endPosition;
				}
				i++;
			}

			return attributes;
		}

		// sign of break between two components of multigeometry in coordinates list
		private static double breakSign = -2684354.56;

		private static int primitivesBreakSign = -268435456;

		// layers list header size in bytes
		private static uint layerListHeaderSize = 13u;

		// attributes list header size in bytes
		private static uint attributesListHeaderSize = 13u;

		// symbol header size in bytes
		private static uint symbolHeaderSize = 24u;

		// feature header size in bytes
		private static uint featureHeaderSize = 44u;

		// primitive size in bytes
		private static uint primitiveSize = 32u;

		private static ISet<Version> supportedVersions = new HashSet<Version>
		{
			new Version(1, 10),
			new Version(1, 15)
		};
		
		private static IDictionary<byte, System.Type> valueTypeByCode = new Dictionary<byte, System.Type>()
		{
			{ 1, typeof(System.Byte) },
			{ 2, typeof(System.Int16) },
			{ 3, typeof(System.Int32) },
			{ 4, typeof(System.Double) },
			{ 5, typeof(System.String) },
			{ 6, typeof(System.Boolean) }
			//{ 7, typeof(System.String) },
			//{ 8, typeof(List) },
			//{ 9, typeof(Table) }
		};

		private static IDictionary<uint, Symbol.SymbolType> symbolTypeByCode = new Dictionary<uint, Symbol.SymbolType>()
		{
			{ 0, Symbol.SymbolType.Single },
			{ 1, Symbol.SymbolType.Linear },
			{ 2, Symbol.SymbolType.Areal },
			{ 3, Symbol.SymbolType.LinearOriented },
			{ 4, Symbol.SymbolType.LinearScalable },
			{ 5, Symbol.SymbolType.Bilinear }
		};

		private static IDictionary<System.Type, System.Func<string, object>> parsers = new Dictionary<System.Type, System.Func<string, object>>()
		{
			{ typeof(System.Byte), s => System.Byte.Parse(s) },
			{ typeof(System.Int16), s => System.Int16.Parse(s) },
			{ typeof(System.Int32), s => System.Int32.Parse(s) },
			{ typeof(System.Double), s => System.Double.Parse(s) },
			{ typeof(System.String), s => s },
			{ typeof(System.Boolean), s => System.Boolean.Parse(s) }
		};
	}
}

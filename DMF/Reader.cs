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
			public BitArray Parameters;
		}

		internal struct ParameterInfo
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

		internal struct ParameterLists
		{
			public ParameterLists(IList<ParameterInfo> parameters, IList<ParameterInfo> serviceParameters)
			{
				this.Parameters = parameters;
				this.ServiceParameters = serviceParameters;
			}

			public readonly IList<ParameterInfo> Parameters;
			public readonly IList<ParameterInfo> ServiceParameters;
		}

		internal struct FeatureInfo
		{
			public int LayerIndex;
			public int Id;
			public float Scale;
			public int SymbolOrientation;
			public IList<IList<Geometries.Coordinate>> CoordinatesLists;
			public IDictionary<int, string> Parameters;
			public bool IsHidden;
			public bool IsDeleted;
			public bool IsMarked;
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
				case 1: return Geometries.OgcGeometryType.Point;
				case 2: case 3: return Geometries.OgcGeometryType.LineString;
				default:
					// if first and last points in coordinate list are the same, we are dealing with polygon
					// if not, it is just a polyline
					return coordinateList[0].Equals(coordinateList[coordinateList.Count - 1]) ?
						Geometries.OgcGeometryType.Polygon : Geometries.OgcGeometryType.LineString;
			}
		}

		private static Geometries.IGeometry BuildGeometry(IList<IList<Geometries.Coordinate>> coordinateLists)
		{
			switch (coordinateLists.Count)
			{
				case 0: return null;
				case 1:
				{
					IList<Geometries.Coordinate> coordinateList = coordinateLists[0];
					switch (DetectGeometryType(coordinateList))
					{
						case Geometries.OgcGeometryType.Point: return BuildPoint(coordinateList);
						case Geometries.OgcGeometryType.LineString: return BuildLineString(coordinateList);
						case Geometries.OgcGeometryType.Polygon: return BuildSimplePolygon(coordinateList);
					}
					break;
				}
				default:
				{
					IList<Geometries.Coordinate> firstCoordinateList = coordinateLists[0];
					switch (DetectGeometryType(firstCoordinateList))
					{
						case Geometries.OgcGeometryType.Point: return BuildMultiPoint(coordinateLists);
						case Geometries.OgcGeometryType.LineString: return BuildMultiLineString(coordinateLists);
						case Geometries.OgcGeometryType.Polygon: return BuildComplexPolygon(coordinateLists);
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
				ParameterLists parameterLists = ReadParameterList(reader);
				IList<Symbol> symbols = ReadSymbolList(reader);
				if (signature.Version.Equals(new Version(1, 15)))
				{
					ReadAccessPolicies(reader);
				}
				ICollection<FeatureInfo> featuresInfo = ReadFeatureList(reader, header.FeatureCount);

				// create service parameter library
				IList<Parameter> serviceParameters = new List<Parameter>();
				foreach (ParameterInfo parameterInfo in parameterLists.ServiceParameters)
				{
					serviceParameters.Add(new Parameter(
						parameterInfo.Id,
						parameterInfo.Name,
						parameterInfo.ValueType,
						parameterInfo.State,
						parameterInfo.MinScale,
						parameterInfo.MaxScale,
						parameterInfo.Brush,
						parameterInfo.Font,
						parameterInfo.SymbolNumber > 0 ? symbols[(int)parameterInfo.SymbolNumber - 1] : null,
						parameterInfo.Format));
				}

				// create parameter library
				IList<Parameter> parameters = new List<Parameter>();
				foreach (ParameterInfo parameterInfo in parameterLists.Parameters)
				{
					parameters.Add(new Parameter(
						parameterInfo.Id,
						parameterInfo.Name,
						parameterInfo.ValueType,
						parameterInfo.State,
						parameterInfo.MinScale,
						parameterInfo.MaxScale,
						parameterInfo.Brush,
						parameterInfo.Font,
						parameterInfo.SymbolNumber > 0 ? symbols[(int)parameterInfo.SymbolNumber - 1] : null,
						parameterInfo.Format));
				}

				// sort features by layers
				// TODO: the following code does not process layers containing objects that are not features
				// TODO: thus such layers still will be contained in featuresByLayerId dictionary
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
						// create parameter list for the feature
						IDictionary<Parameter, object> parameterValues = new Dictionary<Parameter, object>();
						foreach (KeyValuePair<int, string> parameterValueString in featureInfo.Parameters)
						{
							Parameter parameter =
								parameterValueString.Key <= 0 ?
									serviceParameters[parameterValueString.Key + serviceParameters.Count - 1] :
									parameters[parameterValueString.Key - 1];			

							parameterValues[parameter] = parsers[parameter.ValueType](parameterValueString.Value);
						}

						featuresByLayerIndex[featureInfo.LayerIndex].Add(new Feature(
							featureInfo.Id,
							featureInfo.Scale,
							featureInfo.SymbolOrientation,
							geometry,
							parameterValues,
							featureInfo.IsHidden,
							featureInfo.IsDeleted,
							featureInfo.IsMarked));
					}
				}

				IList<Layer> layers = new List<Layer>();
				foreach (LayerInfo layerInfo in layersInfo)
				{
					ISet<Parameter> layerParameters = new HashSet<Parameter>();

					for (int i = 0; i < System.Math.Min(11, layerInfo.Parameters.Count); i++)
					{
						if (layerInfo.Parameters[i] == true)
						{
							layerParameters.Add(serviceParameters[i]);
						}
					}

					for (int i = 11; i < layerInfo.Parameters.Count; i++)
					{
						if (layerInfo.Parameters[i] == true)
						{
							layerParameters.Add(parameters[i - 11]);
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
						layerParameters,
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

			// parameter bit array length - 4-byte integer
			uint parametersArrayLength = reader.ReadUInt32();

			// parameters bit array
			layerInfo.Parameters = new BitArray(reader.ReadBytes((int)(parametersArrayLength)));

			// layer's symbol number in symbol library - 4-byte integer
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
				parametersArrayLength;

			if (layerDescriptorSize < bytesRead)
			{
				throw new System.IO.InvalidDataException("Invalid file format: invalid layer descriptor size or content");
			}

			// skip other data
			byte[] otherData = reader.ReadBytes((int)(layerDescriptorSize - bytesRead));

			return layerInfo;
		}

		private static ParameterLists ReadParameterList(BinaryReader reader)
		{
			// TODO: use size to check count of bytes read
			// list size in bytes - 4-byte integer
			uint size = reader.ReadUInt32();

			// list header size in bytes - 4-byte integer
			uint headerSize = reader.ReadUInt32();
			if (headerSize != parameterListHeaderSize)
			{
				throw new System.IO.InvalidDataException("Invalid file format: unsupported parameter list header size");
			}

			// normal parameters count - 4-byte integer
			uint parametersCount = reader.ReadUInt32();

			// status - 4-byte integer - reserved
			reader.ReadInt32();

			// service parameters count - 4-byte integer
			uint serviceParametersCount = (uint)(-reader.ReadInt32());
			// first service parameter number
			int firstServiceParameterNumber = -(int)serviceParametersCount + 1;

			// reserved - 4-byte integer
			reader.ReadByte();

			IList<ParameterInfo> serviceParameters = new List<ParameterInfo>();
			for (int parameterNumber = firstServiceParameterNumber; parameterNumber <= 0; parameterNumber++)
			{
				serviceParameters.Add(ReadParameter(reader));
			}

			IList<ParameterInfo> parameters = new List<ParameterInfo>();
			for (int parameterNumber = 1; parameterNumber <= parametersCount; parameterNumber++)
			{
				parameters.Add(ReadParameter(reader));
			}

			return new ParameterLists(parameters, serviceParameters);
		}

		private static ParameterInfo ReadParameter(BinaryReader reader)
		{
			ParameterInfo parameterInfo = new ParameterInfo();

			// parameter descriptor size in bytes - 4-byte integer
			uint parameterDescriptorSize = reader.ReadUInt32();

			// parameter status - 4-byte integer
			reader.ReadBytes(2);

			parameterInfo.State = State.Unknown;
			switch (reader.ReadByte())
			{
				case 0:
				{
					parameterInfo.State = State.Editable;
					break;
				}
				case 1:
				{
					parameterInfo.State = State.Markable;
					break;
				}
				case 2:
				{
					parameterInfo.State = State.Visible;
					break;
				}
				case 3:
				{
					parameterInfo.State = State.Invisible;
					break;
				}
				default:
				{
					throw new System.IO.InvalidDataException("Invalid file content: unsupported parameter state");
				}
			}

			if (!valueTypeByCode.TryGetValue(reader.ReadByte(), out parameterInfo.ValueType))
			{
				throw new System.IO.InvalidDataException("Invalid file content: unsupported parameter value type");
			}

			// parameter ID - 4-byte integer
			parameterInfo.Id = reader.ReadInt32();

			// min scale - 4-byte integer
			parameterInfo.MinScale = reader.ReadInt32();

			// max scale - 4-byte integer
			parameterInfo.MaxScale = reader.ReadInt32();

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

			// parameter name
			parameterInfo.Name = reader.ReadShortString();

			// font name
			string fontName = reader.ReadShortString();
			string[] nameParts = fontName.Split(':');
			byte charSet = (nameParts.Length < 2) ? CharSets.Default : byte.Parse(nameParts[1]);

			// 4-byte integer - reserved
			reader.ReadInt32();

			// parameter bit array length - 4-byte integer - not in use
			uint parametersArrayLength = reader.ReadUInt32();

			// parameters bit array - not in use
			reader.ReadBytes((int)(parametersArrayLength));

			// parameter's symbol number in symbol library - 4-byte integer
			parameterInfo.SymbolNumber = reader.ReadUInt32();

			// format
			parameterInfo.Format = reader.ReadShortString();

			// parameter reference counter - 4-byte integer - reserved
			reader.ReadUInt32();

			// addition to pen width - 4-byte integer
			int penWidth100 = reader.ReadInt32();

			// addition to font size - 4-byte integer
			int fontSize10 = reader.ReadInt32();

			parameterInfo.Brush = new Brush(new Color(brushColor), brushStyle);
			parameterInfo.Font = new Font(
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
				(uint)parameterInfo.Name.Length + 1 +
				(uint)parameterInfo.Font.Name.Length + 1 +
				(uint)parameterInfo.Format.Length + 1 +
				parametersArrayLength;

			if (parameterDescriptorSize < bytesRead)
			{
				throw new System.IO.InvalidDataException("Invalid file format: invalid parameter descriptor size or content");
			}

			// skip other data
			byte[] otherData = reader.ReadBytes((int)(parameterDescriptorSize - bytesRead));

			return parameterInfo;
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

			// primitives count - 4-byte integer
			uint primitivesCount = reader.ReadUInt32();

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

			IList<Symbol.Primitive> primitives = new List<Symbol.Primitive>();
			for (uint primitiveNumber = 0; primitiveNumber < primitivesCount; primitiveNumber++)
			{
				primitives.Add(ReadSymbolPrimitive(reader));
			}

			uint bytesRead =
				headerSize + // size of elements with fixed size
				primitiveSize * primitivesCount;

			if (symbolDescriptorSize < bytesRead)
			{
				throw new System.IO.InvalidDataException("Invalid file format: invalid symbol descriptor size");
			}

			// skip other data
			byte[] otherData = reader.ReadBytes((int)(symbolDescriptorSize - bytesRead));

			return new Symbol(type, length, height, primitives);
		}

		private static Symbol.Primitive ReadSymbolPrimitive(BinaryReader reader)
		{
			// primitive type - 1-byte char
			Symbol.Primitive.PrimitiveType type;
			if (!primitiveTypeByCode.TryGetValue((char)reader.ReadByte(), out type))
			{
				throw new System.IO.InvalidDataException("Invalid file content: unsupported primitive type");
			}

			// primitive group number - 1-byte
			byte groupNumber = reader.ReadByte();

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

			int x1 = reader.ReadInt32();
			int y1 = reader.ReadInt32();
			int x2 = reader.ReadInt32();
			int y2 = reader.ReadInt32();

			return new Symbol.Primitive(
				type,
				groupNumber,
				new Pen(new Color(penColor), penWidth, penStyle),
				new Brush(new Color(brushColor), brushStyle),
				new Point2D(x1, y1),
				new Point2D(x2, y2));
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

			// feature's points count - 4-byte integer
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

			// parameter string length - 4-byte integer
			int parameterStringLength = reader.ReadInt32();
			string parameterString = System.Text.Encoding.Default.GetString(reader.ReadBytes(parameterStringLength));

			// parse parameter string
			featureInfo.Parameters = ParseParameterString(parameterString);

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

		private static IDictionary<int, string> ParseParameterString(string parameterString)
		{
			IDictionary<int, string> parameters = new Dictionary<int, string>();
			int parameterNumber = 0;
			string parameterValue = string.Empty;

			int valuePosition = 0;
			int labelPosition = 0;
			int endPosition = 0;

			int i = 0;
			while (i < parameterString.Length)
			{
				if (parameterString[i] == (char)(1))
				{
					valuePosition = parameterString.IndexOf((char)(3), i + 1);
					if (valuePosition < 0)
					{
						throw new System.IO.InvalidDataException("Invalid file format: invalid parameter string");
					}

					parameterNumber = int.Parse(parameterString.Substring(i + 1, valuePosition - (i + 1)));

					// try to find label parameters start position
					labelPosition = parameterString.IndexOf((char)(5), valuePosition + 1);
					if (labelPosition < 0)
					{
						endPosition = parameterString.IndexOf((char)(2), valuePosition + 1);
						if (endPosition < 0)
						{
							throw new System.IO.InvalidDataException("Invalid file format: invalid parameter string");
						}

						parameterValue = parameterString.Substring(valuePosition + 1, endPosition - (valuePosition + 1));
					}
					else
					{
						parameterValue = parameterString.Substring(valuePosition + 1, labelPosition - (valuePosition + 1));

						// TODO: parse label parameters

						endPosition = parameterString.IndexOf((char)(2), labelPosition + 1);
						if (endPosition < 0)
						{
							throw new System.IO.InvalidDataException("Invalid file format: invalid parameter string");
						}						
					}

					parameters.Add(parameterNumber, parameterValue);

					i = endPosition;
				}
				i++;
			}

			return parameters;
		}

		// sign of break between two components of multigeometry in coordinates list
		private static double breakSign = -2684354.56;

		// layer list header size in bytes
		private static uint layerListHeaderSize = 13u;

		// parameter list header size in bytes
		private static uint parameterListHeaderSize = 13u;

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

		private static IDictionary<char, Symbol.Primitive.PrimitiveType> primitiveTypeByCode = new Dictionary<char, Symbol.Primitive.PrimitiveType>()
		{
			{ 'P', Symbol.Primitive.PrimitiveType.Polyline },
			{ 'C', Symbol.Primitive.PrimitiveType.Circle },
			{ 'R', Symbol.Primitive.PrimitiveType.Rectangle },
			{ 'M', Symbol.Primitive.PrimitiveType.Semicircle },
			{ 'L', Symbol.Primitive.PrimitiveType.Unsupported }
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

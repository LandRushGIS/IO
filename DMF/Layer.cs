using System.Collections.Generic;
//using Geometries = GeoAPI.Geometries;

namespace LandRush.IO.DMF
{
	public class Layer
	{
		public enum LayerObjectsType
		{
			Unknown = 0,
			Polygon = 1,
			Polyline = 2,
			Symbol = 3
			//SmoothPolygon.
			//SmoothPolyline.
			//Chainage.
			//Frame_and_Legend.
			//Table.
			//DigitalTerrainModel.
			//SheetMarking.
			//LayersGroup
		}

		public Layer(
			int id,
			string name,
			Status status,
			int minScale,
			int maxScale,
			Pen pen,
			Brush brush,
			Font font,
			Symbol symbol,
			LayerObjectsType objectsType,
			ISet<Parameter> parameters,
			ISet<Feature> features)
		{
			this.id = id;
			this.name = name;
			this.status = status;
			this.minScale = minScale;
			this.maxScale = maxScale;
			this.pen = pen;
			this.brush = brush;
			this.font = font;
			this.symbol = symbol;
			this.objectsType = objectsType;
			this.parameters = parameters;
			this.features = features;
		}

		public int Id
		{
			get { return this.id; }
		}

		public string Name
		{
			get { return this.name; }
		}

		public int MinScale
		{
			get { return this.minScale; }
		}

		public int MaxScale
		{
			get { return this.maxScale; }
		}

		public Pen Pen
		{
			get { return this.pen; }
		}

		public Brush Brush
		{
			get { return this.brush; }
		}

		public Font Font
		{
			get { return this.font; }
		}

		public Symbol Symbol
		{
			get { return this.symbol; }
		}

		public LayerObjectsType ObjectsType
		{
			get { return this.objectsType; }
		}

		IEnumerable<Parameter> GetParameters()
		{
			return this.parameters;
		}

		IEnumerable<Feature> GetFeatures()
		{
			return this.features;
		}

		private int id;
		private string name;
		private Status status;
		private int minScale;
		private int maxScale;
		private Pen pen;
		private Brush brush;
		private Font font;
		private Symbol symbol;
		private LayerObjectsType objectsType;
		private ISet<Parameter> parameters;
		private ISet<Feature> features;
	}
}

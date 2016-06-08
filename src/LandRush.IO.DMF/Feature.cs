using System.Collections.Generic;
using Geometries = GeoAPI.Geometries;
using NetTopologySuite.Features;

namespace LandRush.IO.DMF
{
	public class Feature : IFeature
	{
		public Feature(
			int id,
			float scale,
			int symbolOrientation,
			Geometries.IGeometry geometry,
			IDictionary<Attribute, object> attributesValues,
			bool isHidden,
			bool isDeleted,
			bool isMarked)
		{
			this.id = id;
			this.scale = scale;
			this.symbolOrientation = symbolOrientation;
			this.geometry = geometry;
			this.attributesTable = new AttributesTable(attributesValues);
			this.isHidden = isHidden;
			this.isDeleted = isDeleted;
			this.isMarked = isMarked;
		}

		public int Id
		{
			get { return this.id; }
		}

		public float Scale
		{
			get { return this.scale; }
		}

		public int SymbolOrientation
		{
			get { return this.symbolOrientation; }
		}

		public Geometries.IGeometry Geometry
		{
			get { return this.geometry; }
			set { throw new System.NotSupportedException(); }
		}

		public IAttributesTable Attributes
		{
			get { return this.attributesTable; }
			set { throw new System.NotSupportedException(); }
		}

		public bool IsHidden
		{
			get { return this.isHidden; }
		}

		public bool IsDeleted
		{
			get { return this.isDeleted; }
		}

		public bool IsMarked
		{
			get { return this.isMarked; }
		}

		private int id;
		private float scale;
		private int symbolOrientation;
		private Geometries.IGeometry geometry;
		private IAttributesTable attributesTable;
		private bool isHidden;
		private bool isDeleted;
		private bool isMarked;
	}
}

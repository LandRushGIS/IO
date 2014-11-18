using System.Collections.Generic;
using Geometries = GeoAPI.Geometries;

namespace LandRush.IO.DMF
{
	public class Feature
	{
		public Feature(
			int id,
			float scale,
			int symbolOrientation,
			Geometries.IGeometry geometry,
			IDictionary<Parameter, object> parameterValues,
			bool isHidden,
			bool isDeleted,
			bool isMarked)
		{
			this.id = id;
			this.scale = scale;
			this.symbolOrientation = symbolOrientation;
			this.geometry = geometry;
			this.parameterValues = parameterValues;
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
		}

		public object GetParameterValue(Parameter parameter)
		{
			return this.parameterValues[parameter];
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
		private IDictionary<Parameter, object> parameterValues;
		private bool isHidden;
		private bool isDeleted;
		private bool isMarked;
	}
}

using System.Collections.Generic;
using Geometries = GeoAPI.Geometries;

namespace LandRush.IO.DMF
{
	public class Feature
	{
		public enum FeatureStatus
		{
			Unknown = 0,
			Hidden = 1,
			Deleted = 2,
			Marked = 3
		}

		public Feature(
			int id,
			FeatureStatus status,
			float scale,
			int symbolOrientation,
			Geometries.IGeometry geometry,
			IDictionary<Parameter, object> parameterValues)
		{
			this.id = id;
			this.status = status;
			this.scale = scale;
			this.symbolOrientation = symbolOrientation;
			this.geometry = geometry;
			this.parameterValues = parameterValues;
		}

		public int Id
		{
			get { return this.id; }
		}

		public FeatureStatus Status
		{
			get { return this.status; }
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

		private int id;
		private FeatureStatus status;
		private float scale;
		private int symbolOrientation;
		private Geometries.IGeometry geometry;
		private IDictionary<Parameter, object> parameterValues;
	}
}

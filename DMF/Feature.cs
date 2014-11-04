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
			ISet<Geometries.IPoint> points,
			IDictionary<Parameter, string> parameterValues)
		{
			this.id = id;
			this.status = status;
			this.scale = scale;
			this.points = points;
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

		public IEnumerable<Geometries.IPoint> GetPoints()
		{
			return this.points;
		}

		public string GetParameterValue(Parameter parameter)
		{
			return this.parameterValues[parameter];
		}

		private int id;
		private FeatureStatus status;
		private float scale;
		private ISet<Geometries.IPoint> points;
		private IDictionary<Parameter, string> parameterValues;
	}
}

using System.Collections.Generic;

namespace LandRush.IO.DMF
{
	public class Symbol
	{
		public class Primitive
		{
			public enum PrimitiveType
			{
				Unknown = 0,
				Polyline = 1,
				Circle = 2,
				Rectangle = 3,
				Semicircle = 4,
				// primitive type with unknown purposes
				Unsupported = 5
			}

			public Primitive(
				PrimitiveType type,
				byte groupNumber,
				Pen pen,
				Brush brush,
				Point2D firstPoint,
				Point2D secondPoint)
			{
				this.type = type;
				this.groupNumber = groupNumber;
				this.pen = pen;
				this.brush = brush;
				this.firstPoint = firstPoint;
				this.secondPoint = secondPoint;
			}

			public PrimitiveType Type
			{
				get { return this.type; }
			}

			public byte GroupNumber
			{
				get { return this.groupNumber; }
			}

			public Pen Pen
			{
				get { return this.pen; }
			}

			public Brush Brush
			{
				get { return this.brush; }
			}

			public Point2D FirstPoint
			{
				get { return this.firstPoint; }
			}

			public Point2D SecondPoint
			{
				get { return this.secondPoint; }
			}

			private PrimitiveType type;
			private byte groupNumber;
			private Pen pen;
			private Brush brush;
			private Point2D firstPoint;
			private Point2D secondPoint;
		}

		public enum SymbolType
		{
			Unknown = 0,
			Single = 1,
			Linear = 2,
			Areal = 3,
			LinearOriented = 4,
			LinearScalable = 5,
			Bilinear = 6
		}

		public Symbol(
			SymbolType type,
			uint length,
			uint height,
			IList<Primitive> primitives)
		{
			this.type = type;
			this.length = length;
			this.height = height;
			this.primitives = primitives;
		}

		public SymbolType Type
		{
			get { return this.type; }
		}

		public uint Length
		{
			get { return this.length; }
		}

		public uint Height
		{
			get { return this.height; }
		}

		public IEnumerable<Primitive> Primitives
		{
			get { return this.primitives; }
		}

		private SymbolType type;
		private uint length;
		private uint height;
		private IList<Primitive> primitives;
	}
}

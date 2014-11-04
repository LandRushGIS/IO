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
				Semicircle = 4
			}

			public Primitive(
				PrimitiveType type,
				Pen pen,
				Brush brush,
				Point2D topLeft,
				Point2D bottomRight)
			{
				this.type = type;
				this.pen = pen;
				this.brush = brush;
				this.topLeft = topLeft;
				this.bottomRight = bottomRight;
			}

			public PrimitiveType Type
			{
				get { return this.type; }
			}

			public Pen Pen
			{
				get { return this.pen; }
			}

			public Brush Brush
			{
				get { return this.Brush; }
			}

			public Point2D TopLeft
			{
				get { return this.topLeft; }
			}

			public Point2D BottomRight
			{
				get { return this.bottomRight; }
			}

			private PrimitiveType type;
			private Pen pen;
			private Brush brush;
			private Point2D topLeft;
			private Point2D bottomRight;
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
			int length,
			int high,
			ISet<Primitive> primitives)
		{
			this.type = type;
			this.length = length;
			this.high = high;
			this.primitives = primitives;
		}

		public SymbolType Type
		{
			get { return this.type;  }
		}

		public int Length
		{
			get { return this.length; }
		}

		public int High
		{
			get { return this.high; }
		}

		public IEnumerable<Primitive> GetPrimitives()
		{
			return this.primitives;
		}

		private SymbolType type;
		private int length;
		private int high;
		private ISet<Primitive> primitives;
	}
}

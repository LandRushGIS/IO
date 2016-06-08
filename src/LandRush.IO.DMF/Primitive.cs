using System;
using System.Collections.Generic;

namespace LandRush.IO.DMF
{
	public abstract class Primitive
	{
		public Primitive(
			byte groupNumber,
			Pen pen,
			Brush brush)
		{
			this.groupNumber = groupNumber;
			this.pen = pen;
			this.brush = brush;
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

		private byte groupNumber;
		private Pen pen;
		private Brush brush;
	}

	public class RectanglePrimitive : Primitive
	{
		public RectanglePrimitive(
			byte groupNumber,
			Pen pen,
			Brush brush,
			Point2D leftTopPoint,
			Point2D rightBottomPoint)
			: base(groupNumber, pen, brush)
		{
			if (rightBottomPoint.X <= leftTopPoint.X)
			{
				throw new System.ArgumentException("invalid x coordinate");
			}
			if (rightBottomPoint.Y <= leftTopPoint.Y)
			{
				throw new System.ArgumentException("invalid y coordinate");
			}

			this.rightBottomPoint = rightBottomPoint;
			this.leftTopPoint = leftTopPoint;
		}

		public Point2D LeftTopPoint
		{
			get { return this.leftTopPoint; }
		}

		public uint Width
		{
			get { return (uint)(this.rightBottomPoint.X - this.leftTopPoint.X); }
		}

		public uint Height
		{
			get { return (uint)(this.rightBottomPoint.Y - this.leftTopPoint.Y); }
		}

		private Point2D leftTopPoint;
		private Point2D rightBottomPoint;
	}

	public class CirclePrimitive : Primitive
	{
		public CirclePrimitive(
			byte groupNumber,
			Pen pen,
			Brush brush,
			Point2D leftTopPoint,
			Point2D rightBottomPoint)
			: base(groupNumber, pen, brush)
		{
			if (rightBottomPoint.X <= leftTopPoint.X)
			{
				throw new System.ArgumentException("invalid x coordinate");
			}
			if (rightBottomPoint.Y <= leftTopPoint.Y)
			{
				throw new System.ArgumentException("invalid y coordinate");
			}
			if (((rightBottomPoint.X - leftTopPoint.X) % 2) == 1)
			{
				throw new System.ArgumentException("Non integer radius");
			}
			if ((rightBottomPoint.X - leftTopPoint.X) != (rightBottomPoint.Y - leftTopPoint.Y))
			{
				throw new System.ArgumentException("Invalid circle parameters");
			}

			this.rightBottomPoint = rightBottomPoint;
			this.leftTopPoint = leftTopPoint;
		}

		public Point2D Centre
		{
			get
			{
				return new Point2D((this.rightBottomPoint.X + this.leftTopPoint.X) / 2,
					(this.rightBottomPoint.Y + this.leftTopPoint.Y) / 2);
			}
		}

		public uint Radius
		{
			get { return (uint)(this.rightBottomPoint.X - this.leftTopPoint.X) / 2; }
		}

		private Point2D leftTopPoint;
		private Point2D rightBottomPoint;
	}

	public class SemicirclePrimitive : Primitive
	{
		public SemicirclePrimitive(
			byte groupNumber,
			Pen pen,
			Brush brush,
			Point2D firstPoint,
			Point2D secondPoint)
			: base(groupNumber, pen, brush)
		{
			if (firstPoint == secondPoint)
			{
				throw new System.ArgumentException("Invalid semicircle parameters");
			}
			if (((firstPoint.X + secondPoint.X) % 2) == 1)
			{
				throw new System.ArgumentException("Centre coordinates are not integer");
			}

			this.firstPoint = firstPoint;
			this.secondPoint = secondPoint;
		}

		public Point2D Centre
		{
			get
			{
				return new Point2D((this.firstPoint.X + this.secondPoint.X) / 2,
					(this.firstPoint.Y + this.secondPoint.Y) / 2);
			}
		}

		public double Radius
		{
			get
			{
				return Math.Sqrt(
					(this.firstPoint.X - this.Centre.X) * (this.firstPoint.X - this.Centre.X)
					+ (this.firstPoint.Y - this.Centre.Y) * (this.firstPoint.Y - this.Centre.Y));
			}
		}

		public double StartAngle
		{
			get
			{
				return Math.Atan2((this.Centre.Y - this.firstPoint.Y), (this.Centre.X - this.firstPoint.X));
			}
		}

		private Point2D firstPoint;
		private Point2D secondPoint;
	}

	public class PolylinePrimitive : Primitive
	{
		public PolylinePrimitive(
			byte groupNumber,
			Pen pen,
			Brush brush,
			IList<Point2D> points)
			: base(groupNumber, pen, brush)
		{
			this.points = points;
		}

		public IEnumerable<Point2D> Points
		{
			get { return this.points; }
		}

		private IList<Point2D> points;
	}
}
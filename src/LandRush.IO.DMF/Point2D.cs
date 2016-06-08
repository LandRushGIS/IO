namespace LandRush.IO.DMF
{
	public struct Point2D
	{
		public Point2D(int x, int y)
		{
			this.X = x;
			this.Y = y;
		}

		public static bool operator ==(Point2D point1, Point2D point2)
		{
			return ((point1.X == point2.X) && (point1.Y == point2.Y));
		}

		public static bool operator !=(Point2D point1, Point2D point2)
		{
			return !((point1.X == point2.X) && (point1.Y == point2.Y));
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Point2D))
			{
				return false;
			}

			Point2D point = (Point2D)obj;
			return this == point;
		}

		public override int GetHashCode()
		{
			return X.GetHashCode() ^ Y.GetHashCode();
		}

		public readonly int X;
		public readonly int Y;
	}
}

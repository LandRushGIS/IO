namespace LandRush.IO.DMF
{
	public class Parameter
	{
		public enum ParameterType
		{
			Unknown = 0,
			Byte = 1,
			Word = 2,
			Int = 3,
			Real = 4,
			String = 5,
			Bool = 6,
			File = 7,
			List = 8,
			Table = 9
		}

		public Parameter(
			int id,
			string name,
			ParameterType type,
			Status status,
			int minScale,
			int maxScale,
			Brush brush,
			Font font,
			string format)
		{
			this.id = id;
			this.name = name;
			this.type = type;
			this.status = status;
			this.minScale = minScale;
			this.maxScale = maxScale;
			this.brush = brush;
			this.font = font;
			this.format = format;
		}

		public int Id
		{
			get { return this.id; }
		}

		public string Name
		{
			get { return this.name; }
		}

		public ParameterType Type
		{
			get { return this.type; }
		}

		public int MinScale
		{
			get { return this.minScale; }
		}

		public int MaxScale
		{
			get { return this.maxScale; }
		}

		public Brush Brush
		{
			get { return this.brush; }
		}

		public Font Font
		{
			get { return this.font; }
		}

		public string Format
		{
			get { return this.format; }
		}

		private int id;
		private string name;
		private ParameterType type;
		private Status status;
		private int minScale;
		private int maxScale;
		private Brush brush;
		private Font font;
		private string format;
	}
}
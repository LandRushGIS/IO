namespace LandRush.IO.DMF
{
	public class Parameter
	{
		public Parameter(
			int id,
			string name,
			System.Type valueType,
			State state,
			int minScale,
			int maxScale,
			Brush brush,
			Font font,
			Symbol symbol,
			string format)
		{
			this.id = id;
			this.name = name;
			this.valueType = valueType;
			this.state = state;
			this.minScale = minScale;
			this.maxScale = maxScale;
			this.brush = brush;
			this.font = font;
			this.symbol = symbol;
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

		public System.Type ValueType
		{
			get { return this.valueType; }
		}

		public State State
		{
			get { return this.state; }
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

		public Symbol Symbol
		{
			get { return this.symbol; }
		}

		public string Format
		{
			get { return this.format; }
		}

		private int id;
		private string name;
		private System.Type valueType;
		private State state;
		private int minScale;
		private int maxScale;
		private Brush brush;
		private Font font;
		private Symbol symbol;
		private string format;
	}
}
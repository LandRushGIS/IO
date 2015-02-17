using System.Collections.Generic;

namespace LandRush.IO.DMF
{
	public class Symbol
	{
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

		public IList<Primitive> Primitives
		{
			get { return this.primitives; }
		}

		private SymbolType type;
		private uint length;
		private uint height;
		private IList<Primitive> primitives;
	}
}

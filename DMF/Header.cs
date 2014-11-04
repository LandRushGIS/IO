namespace LandRush.IO.DMF
{
	internal struct Header
	{
		public Header(
			double scale,
			uint objectCount,
			string name,
			string leftFile,
			string rightFile)
		{
			this.Scale = scale;
			this.ObjectCount = objectCount;
			this.Name = name;
			this.LeftFile = leftFile;
			this.RightFile = rightFile;
		}

		// Знаменатель масштаба карты
		public readonly double Scale;
		// Количество топографических объектов на карте
		public readonly uint ObjectCount;
		// Наименование карты
		public readonly string Name;
		// Имя растрового файла, содержащего левый снимок стереопары или снимок (карту) для моно режима
		public readonly string LeftFile;
		// Имя растрового файла, содержащего правый снимок стереопары
		public readonly string RightFile;
	}
}

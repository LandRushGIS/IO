namespace LandRush.IO.DMF.Viewer
{
	class Program
	{
		static void Main(string[] args)
		{
			string fileName = System.Console.ReadLine();
			DMF.Reader.Read(new System.IO.FileStream(fileName, System.IO.FileMode.Open,System.IO.FileAccess.Read, System.IO.FileShare.Read));
		}
	}
}

using System;
using System.Text;

using namespace MapData
{
	public class ChPointSerializeJSON
	{
		public static serializeChPointJSON(List<ChPoint> points,(int,int) origin)
		{
			//Begin JSON
			StringBuilder pointsJSONBuilder = new StringBuilder($"\{\"originX\": {origin.Item1}, \"originY\": {origin.Item2},\"points\": [");

			//Output JSON Points Array

		}
	}
}
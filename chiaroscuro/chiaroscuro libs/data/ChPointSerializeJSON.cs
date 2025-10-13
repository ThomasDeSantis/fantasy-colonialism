using System;
using System.Text;

namespace MapData
{
    public class ChPointSerializeJSON
    {
        public static string serializeChPointJSON(HashSet<ChPoint> points, (int, int) origin)
        {
            //Begin JSON
            StringBuilder pointsJSONBuilder = new StringBuilder($"{{\"originX\": {origin.Item1}, \"originY\": {origin.Item2},\"points\": [");

            //Output JSON Points Array
            foreach ( ChPoint p in points ) {
                pointsJSONBuilder.Append($"{{\"x\": {p.X}, \"y\": {p.Y}}},");
            }
            pointsJSONBuilder.Remove(pointsJSONBuilder.Length - 1, 1); // Remove trailing comma
            pointsJSONBuilder.Append("]}");

            return pointsJSONBuilder.ToString();
        }
    }
}
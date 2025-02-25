using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismBackend
{
    class Point
    {

        //Returns coordinates of the neighbors of a point in a plus shape
        public static List<(int, int)> getNeighborsPlus((int, int) point)
        {
            int x = point.Item1;
            int y = point.Item2;
            return new List<(int, int)>
            {
                (x - 1, y), // left
                (x + 1, y), // right
                (x, y - 1), // up
                (x, y + 1)  // down
            };
        }

        //Returns coordinates of the neighbors of a point in a plus shape
        public static List<(int, int)> getNeighborsSquare((int, int) point)
        {
            int x = point.Item1;
            int y = point.Item2;
            return new List<(int, int)>
            {
                (x - 1, y), // left
                (x + 1, y), // right
                (x, y - 1), // up
                (x, y + 1),  // down
                (x - 1, y - 1), // up left
                (x -1, y + 1), // down left
                (x + 1, y - 1), // up right
                (x + 1, y + 1) // down right
            };
        }

        //Returns the closest point to the given point within the given province
        //Use BFS to ensure you arent just using the first you find, but to ensure it will be the closest (or at least close to it)
        public static (int, int) findClosestPointWithinAProvince(int provinceId, int x, int y, DBConnection database)
        {
            string getPointsInProvinceQuery = "SELECT x, y FROM Points WHERE provinceId = @provinceId";
            var getPointsCmd = new MySqlCommand(getPointsInProvinceQuery, database.Connection);
            getPointsCmd.Parameters.AddWithValue("@provinceId", provinceId);

            MySqlDataReader rdr = getPointsCmd.ExecuteReader();
            List<(int, int)> pointsInProvince = new List<(int, int)>();

            while (rdr.Read())
            {
                pointsInProvince.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
            }
            rdr.Close();

            HashSet<(int, int)> visited = new HashSet<(int, int)>();
            Queue<(int, int)> queue = new Queue<(int, int)>();
            queue.Enqueue((x, y));

            while (queue.Count > 0)
            {
                (int, int) currentPoint = queue.Dequeue();
                if (visited.Contains(currentPoint))
                {
                    continue;
                }
                visited.Add(currentPoint);

                if (pointsInProvince.Contains(currentPoint))
                {
                    return currentPoint;
                }

                foreach ((int, int) neighbor in getNeighborsPlus(currentPoint))
                {
                    if (!visited.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            throw new Exception("No point found within the province.");
        }
    }
}

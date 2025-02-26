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

        //Returns coordinates of the neighbors of a point in a x shape
        public static List<(int, int)> getNeighborsX((int,int) point)
        {
            int x = point.Item1;
            int y = point.Item2;
            return new List<(int, int)>
            {
                (x - 1, y - 1), // up left
                (x -1, y + 1), // down left
                (x + 1, y - 1), // up right
                (x + 1, y + 1) // down right
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

        public static List<(int provinceId, int x, int y)> getListOfAllPointsWithProvinces(DBConnection database)
        {
            string query = "SELECT provinceId, x, y FROM Points ORDER BY provinceId";
            var cmd = new MySqlCommand(query, database.Connection);

            MySqlDataReader rdr = cmd.ExecuteReader();
            List<(int provinceId, int x, int y)> pointsWithProvinces = new List<(int provinceId, int x, int y)>();

            while (rdr.Read())
            {
                pointsWithProvinces.Add((rdr.GetInt32(0), rdr.GetInt32(1), rdr.GetInt32(2)));
            }
            rdr.Close();

            return pointsWithProvinces;
        }

        //Searches for neighbors that are valid points in a plus pattern  
        //If plus == true then search using the getNeighborsPlus pattern  
        //Returns a province id if there is at least one valid province, returns -1 if there are no valid provinces  
        public static int getNeighborValidPoint(DBConnection database, (int, int) point, bool plus)
        {
            string query = null;
            if (plus)
            {
                query = "SELECT DISTINCT provinceId from Points WHERE (x = (@x + 1) AND y = @y) OR (x = (@x - 1) AND y = @y) OR (x = @x AND y = (@y + 1)) OR (x = @x AND y = (@y - 1))";
            }
            else
            {
                query = "SELECT DISTINCT provinceId from Points WHERE (x = (@x + 1) AND y = (@y + 1)) OR (x = (@x - 1) AND y = (@y - 1)) OR (x = (@x + 1) AND y = (@y - 1)) OR (x = (@x - 1) AND y = (@y + 1))";
            }

            var cmd = new MySqlCommand(query, database.Connection);
            cmd.Parameters.AddWithValue("@x", point.Item1);
            cmd.Parameters.AddWithValue("@y", point.Item2);

            MySqlDataReader rdr = cmd.ExecuteReader();

            List<int> provinces = new List<int>();

            while (rdr.Read())
            {
                provinces.Add(Int32.Parse(rdr[0].ToString()));
            }
            rdr.Close();

            if (provinces.Count == 0)
            {
                return -1;
            }
            else
            {
                //Return the most frequent element of the list
                return provinces.GroupBy(i => i).OrderByDescending(grp => grp.Count()).Select(grp => grp.Key).First();
            }
        }
    }
}

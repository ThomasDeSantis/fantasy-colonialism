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
        //0 - West
        // 1 - East
        // 2 - North
        // 3 - South
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
            string query = "SELECT provinceId, x, y FROM Points WHERE land = true ORDER BY provinceId ";
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

        public static List<(int x, int y)> getListOfAllOceanPointsWithProvinces(DBConnection database)
        {
            string query = "SELECT x, y FROM Points WHERE type = 'ocean'";
            var cmd = new MySqlCommand(query, database.Connection);

            MySqlDataReader rdr = cmd.ExecuteReader();
            List<(int x, int y)> oceanPoints = new List<(int x, int y)>();

            while (rdr.Read())
            {
                oceanPoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
            }
            rdr.Close();

            return oceanPoints;
        }

        public static List<(int x, int y)> getListOfAllLakePointsWithProvinces(DBConnection database)
        {
            string query = "SELECT x, y FROM Points WHERE type = 'lake'";
            var cmd = new MySqlCommand(query, database.Connection);

            MySqlDataReader rdr = cmd.ExecuteReader();
            List<(int x, int y)> lakePoints = new List<(int x, int y)>();

            while (rdr.Read())
            {
                lakePoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
            }
            rdr.Close();

            return lakePoints;
        }

        //Searches for neighbors that are valid points in a plus pattern  
        //If plus == true then search using the getNeighborsPlus pattern  
        //Returns a province id if there is at least one valid province, returns -1 if there are no valid provinces  
        public static int getNeighborValidPoint(DBConnection database, (int, int) point, bool plus)
        {
            string query = null;
            if (plus)
            {
                query = "SELECT provinceId from Points WHERE (x = (@x + 1) AND y = @y) OR (x = (@x - 1) AND y = @y) OR (x = @x AND y = (@y + 1)) OR (x = @x AND y = (@y - 1)) AND land = true;";
            }
            else
            {
                query = "SELECT provinceId from Points WHERE (x = (@x + 1) AND y = (@y + 1)) OR (x = (@x - 1) AND y = (@y - 1)) OR (x = (@x + 1) AND y = (@y - 1)) OR (x = (@x - 1) AND y = (@y + 1)) AND land = true";
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

        //Searches for neighbors that are valid points in a plus pattern  
        //If plus == true then search using the getNeighborsPlus pattern  
        //Returns a province id if there is at least one valid province, returns -1 if there are no valid provinces  
        public static int getNeighborValidPoint(Dictionary<(int,int),int> validPoints, (int, int) point)
        {
            string query = "SELECT provinceId from Points WHERE (x = (@x + 1) AND y = @y) OR (x = (@x - 1) AND y = @y) OR (x = @x AND y = (@y + 1)) OR (x = @x AND y = (@y - 1)) AND land = true;";
            List<int> provinces = new List<int>();

            //Retrieve the north,east,south, and west province ids by referencing the dictionary
            if (validPoints.ContainsKey((point.Item1,point.Item2 - 1)))
            {
                provinces.Add(validPoints[(point.Item1, point.Item2 - 1)]);
            }
            if (validPoints.ContainsKey((point.Item1 + 1, point.Item2)))
            {
                provinces.Add(validPoints[(point.Item1 + 1, point.Item2)]);
            }
            if (validPoints.ContainsKey((point.Item1, point.Item2 + 1)))
            {
                provinces.Add(validPoints[(point.Item1, point.Item2 + 1)]);
            }
            if (validPoints.ContainsKey((point.Item1 - 1, point.Item2)))
            {
                provinces.Add(validPoints[(point.Item1 - 1, point.Item2)]);
            }

            //If there are no provinces, then you should leave it alone.
            if(provinces.Count == 0)
            {
                return -1;
            }

            //Return the most frequent province in the list
            return provinces.GroupBy(i => i).OrderByDescending(grp => grp.Count()).Select(grp => grp.Key).First();

        }

        //Write each line of a point list
        public static void outputPointList(List<(int,int)> points)
        {
            Console.WriteLine("Points.\n");
            foreach ((int, int) point in points)
            {
                Console.WriteLine("(" + point.Item1 + ", " + point.Item2 + ")\n");
            }
            Console.WriteLine("End of points.\n");
        }

        public static Dictionary<(int,int), int> retrieveAllValidLandPoints(DBConnection database)
        {
            string query = "SELECT x, y, provinceId FROM Points WHERE land = true and provinceId IS NOT NULL";
            var cmd = new MySqlCommand(query, database.Connection);
            MySqlDataReader rdr = cmd.ExecuteReader();
            Dictionary<(int, int), int> validLandPoints = new Dictionary<(int, int), int>();
            while (rdr.Read())
            {
                validLandPoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)), rdr.GetInt32(2));
            }
            rdr.Close();
            return validLandPoints;
        }

    }
}

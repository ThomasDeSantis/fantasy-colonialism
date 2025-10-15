using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseLibraries;

namespace MapData
{
    static class PointDBLib
    {
       
        //Returns the closest point to the given point within the given province
        //Use BFS to ensure you arent just using the first you find, but to ensure it will be the closest (or at least close to it)
        public static (int, int) findClosestPointWithinAProvince(int provinceId, int x, int y, DBConnection database)
    {
        string getPointsInProvinceQuery = $"SELECT x, y FROM \"Points\" WHERE provinceId = {provinceId};";
        NpgsqlDataReader rdr = database.runQueryCommand(getPointsInProvinceQuery);

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
        string query = "SELECT provinceId, x, y FROM \"Points\" WHERE land = true ORDER BY provinceId ";

        NpgsqlDataReader rdr = database.runQueryCommand(query);
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
        string query = "SELECT x, y FROM \"Points\" WHERE type = 'ocean'";

        NpgsqlDataReader rdr = database.runQueryCommand(query);

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
        string query = "SELECT x, y FROM \"Points\" WHERE type = 'lake'";
        NpgsqlDataReader rdr = database.runQueryCommand(query);

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
            query = $"SELECT provinceId from \"Points\" WHERE (x = ({point.Item1} + 1) AND y = {point.Item2}) OR (x = ({point.Item1} - 1) AND y = {point.Item2}) OR (x = {point.Item1} AND y = ({point.Item2} + 1)) OR (x = {point.Item1} AND y = ({point.Item2} - 1)) AND land = true;";
        }
        else
        {
            query = $"SELECT provinceId from \"Points\" WHERE (x = ({point.Item1} + 1) AND y = ({point.Item2} + 1)) OR (x = ({point.Item1} - 1) AND y = ({point.Item2} - 1)) OR (x = ({point.Item1} + 1) AND y = ({point.Item2} - 1)) OR (x = ({point.Item1} - 1) AND y = ({point.Item2} + 1)) AND land = true";
        }

        NpgsqlDataReader rdr = database.runQueryCommand(query);

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

    public static Dictionary<(int, int), int> retrieveAllValidLandPoints(DBConnection database)
    {
        string query = "SELECT x, y, provinceId FROM \"Points\" WHERE land = true and provinceId IS NOT NULL";
        NpgsqlDataReader rdr = database.runQueryCommand(query);
        Dictionary<(int, int), int> validLandPoints = new Dictionary<(int, int), int>();
        while (rdr.Read())
        {
            validLandPoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)), rdr.GetInt32(2));
        }
        rdr.Close();
        return validLandPoints;
    }

    //Given a latitude and longitude, find the closest point within the set of points provided
    //Returns the closest point and the distance to that point
    //If no point is found, closest will be null and minDistance will be -1
    public static void findClosestPoint(double latitude, double longitude, List<Point> points, out Point closest, out double minDistance)
    {
        closest = null;
        minDistance = double.MaxValue;

        double latRadians = degreesToRadians(latitude);
        double lonRadians = degreesToRadians(longitude);

        foreach (var point in points)
        {
            double distance = calculateHaversineDistance(latRadians, lonRadians, point.LatitudeRadians, point.LongitudeRadians, true);

            if (distance < minDistance)
            {
                minDistance = distance;
                closest = point;
            }
        }

        if (closest == null)
        {
            minDistance = -1; // No point found
        }
    }

}
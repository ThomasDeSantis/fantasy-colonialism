using MySql.Data.MySqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class Point
    {
        //You will not need to change any of the properties after instantiation.
        private int x = -1;
        private int y = -1;
        private bool land;
        private float waterSalinity = -1;
        private PointType type = PointType.undefined;
        private TerrainType terrainType = TerrainType.undefined;
        private int provinceId = -1;
        private int id = -1;//The database id on the Points table
        private int worldPointId = -1;//The database id on the WorldPoints table


        private double latitude = -1.0; //Latitude in degrees
        private double longitude = -1.0; //Longitude in degrees

        private double latitudeRadians = -1.0; //Latitude in radians
        private double longitudeRadians = -1.0; //Longitude in radians

        private double coastalDistance = -1; //Distance to the nearest coast in kilometers

        private int width = -1;//Width (N/S) in meters
        private int length = -1; //Length (E/W) in meters

        private int area = -1; //Area in square meters

        private int height = -1; //Height in meters above sea level

        private double averageTemperatureSummer = -1; //Average temperature in summer in degrees Celsius
        private double averageTemperatureWinter = -1; //Average temperature in winter in degrees Celsius

        private double averageRainfall = -1; //Average rainfall in mm/year

        private double erosionFactor = 0.5; //Used for river generation. Holds how easily the ground erodes.
        private double waterRunoff = 0.5; //Used for river generation. Holds the volume of runoff the point receives. Directly accesible as it is purely used by river generation.


        int lakeDepth; // Average depth of the lake at this point in meters.  
        double lakePointAreaPercent; // Percentage of the point that is covered by the lake.  
        int lakeId = -1; // The ID of the lake this point belongs to.


        int riverId = -1;
        int riverLastIteration = -1; // The last iteration of the river generation algorithm that this point was used in.
        double riverVolume = -1; // Volume of water in the river at this point in cubic meters per second.
        double riverDepth = -1; // Depth of the river at this point in meters.
        double riverWidth = -1; // Width of the river at this point in meters.
        double riverAverageSteepness = -1; // Average steepness of the river at this point, used for river generation and erosion calculations.
        double rockiness = -1; // Rockiness of the point, used for river generation and erosion calculations. This is a value between 0 and 1, where 0 is no rockiness and 1 is maximum rockiness.
        double riverRockiness = -1; //Rockiness of the river in the point
        double riverErosionVolume = -1; // Volume of erosion caused by the river

        // Getter Properties
        public int Id { get => id; }
        public int WorldPointId { get => worldPointId; set => worldPointId = value; }
        public int X { get => x; }
        public int Y { get => y; }
        public bool Land { get => land; }
        public float WaterSalinity { get => waterSalinity; }
        public PointType Type { get => type; }
        public TerrainType TerrainType { get => terrainType; }
        public int ProvinceId { get => provinceId; }
        public double Latitude { get => latitude; }
        public double Longitude { get => longitude; }
        public double LatitudeRadians { get => latitudeRadians; }
        public double LongitudeRadians { get => longitudeRadians; }
        public double CoastalDistance { get => coastalDistance; set => coastalDistance = value; }
        public int Width { get => width; }
        public int Length { get => length; }
        public int Area { get => area; }
        public int Height { get => height; }
        public double AverageTemperatureSummer { get => averageTemperatureSummer; }
        public double AverageTemperatureWinter { get => averageTemperatureWinter; }
        public double AverageRainfall { get => averageRainfall; }
        public double ErosionFactor { get => erosionFactor; set => erosionFactor = value; }

        public double WaterRunoff { get => waterRunoff; set => waterRunoff = value; }
        public int LakeDepth { get => lakeDepth; set => lakeDepth = value; }
        public double LakePointAreaPercent { get => lakePointAreaPercent; set => lakePointAreaPercent = value; }
        public int LakeId { get => lakeId; set => lakeId = value; }

        public int RiverId { get => riverId; set => riverId = value; }
        public double RiverDepth { get => riverDepth; set => riverDepth = value; }
        public double RiverWidth { get => riverWidth; set => riverWidth = value; }
        public double RiverAverageSteepness { get => riverAverageSteepness; set => riverAverageSteepness = value; }
        public double Rockiness { get => rockiness; set => rockiness = value; }
        public double RiverRockiness { get => riverRockiness; set => riverRockiness = value; }
        public double RiverErosionVolume { get => riverErosionVolume; set => riverErosionVolume = value; }
        public int RiverLastIteration { get => riverLastIteration; set => riverLastIteration = value; }





        //Used for generating water points
        public Point(int x, int y, bool land, float waterSalinity, PointType type)
        {
            this.x = x;
            this.y = y;
            this.land = land;
            this.waterSalinity = waterSalinity;
            this.type = type;
        }

        //Used for generating land points
        public Point(int x, int y, bool land, PointType type, int provinceId)
        {
            this.x = x;
            this.y = y;
            this.land = land;
            this.type = type;
            this.provinceId = provinceId;
        }

        //Used for generating points for lat/long calculations
        public Point(int id, double latitude, double longitude)
        {
            this.id = id;
            this.latitude = latitude;
            this.longitude = longitude;
            latitudeRadians = degreesToRadians(latitude);
            longitudeRadians = degreesToRadians(longitude);
        }

        //Used for generating points with all properties
        public Point(int id, int worldPointId, int x, int y, bool land, float waterSalinity, PointType type, int provinceId,
                     double latitude, double longitude, double coastalDistance, int width, int length, int area, int height,
                     double averageTemperatureSummer, double averageTemperatureWinter, double averageRainfall, TerrainType terrainType)
        {
            this.id = id;
            this.worldPointId = worldPointId;
            this.x = x;
            this.y = y;
            this.land = land;
            this.waterSalinity = waterSalinity;
            this.type = type;
            this.provinceId = provinceId;
            this.latitude = latitude;
            this.longitude = longitude;
            this.latitudeRadians = degreesToRadians(latitude);
            this.longitudeRadians = degreesToRadians(longitude);
            this.coastalDistance = coastalDistance;
            this.width = width;
            this.length = length;
            this.area = area;
            this.height = height;
            this.averageTemperatureSummer = averageTemperatureSummer;
            this.averageTemperatureWinter = averageTemperatureWinter;
            this.averageRainfall = averageRainfall;
            this.terrainType = terrainType;
        }

        private static double degreesToRadians(double deg) => deg * Math.PI / 180.0;

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

        //Returns coordinates of the neighbors of a point in a plus shape, ensuring they are within the bounds of the given width and height
        public static List<(int, int)> getNeighborsPlusSafe((int, int) point, int width, int height)
        {
            int x = point.Item1;
            int y = point.Item2;
            var neighbors = new List<(int, int)>
            {
                (x - 1, y), // left
                (x + 1, y), // right
                (x, y - 1), // up
                (x, y + 1)  // down
            };

            // Filter neighbors to ensure they are within bounds
            return neighbors.Where(n =>
                n.Item1 >= 0 && n.Item1 < width &&
                n.Item2 >= 0 && n.Item2 < height
            ).ToList();
        }

        public List<(int, int)> getNeighborsPlusSafe(int width, int height)
        {
            var neighbors = new List<(int, int)>
            {
                (x - 1, y), // left
                (x + 1, y), // right
                (x, y - 1), // up
                (x, y + 1)  // down
            };

            // Filter neighbors to ensure they are within bounds
            return neighbors.Where(n =>
                n.Item1 >= 0 && n.Item1 < width &&
                n.Item2 >= 0 && n.Item2 < height
            ).ToList();
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

        //Returns coordinates of the neighbors of a point in a x shape
        public static List<(int, int)> getNeighborsXSafe((int, int) point, int width, int height)
        {
            int x = point.Item1;
            int y = point.Item2;
            var neighbors = new List<(int, int)>
            {
                (x - 1, y - 1), // up left
                (x -1, y + 1), // down left
                (x + 1, y - 1), // up right
                (x + 1, y + 1) // down right
            };

            // Filter neighbors to ensure they are within bounds
            return neighbors.Where(n =>
                n.Item1 >= 0 && n.Item1 < width &&
                n.Item2 >= 0 && n.Item2 < height
            ).ToList();
        }

        //Returns coordinates of the neighbors of a point in a x shape
        public List<(int, int)> getNeighborsXSafe(int width, int height)
        {
            var neighbors = new List<(int, int)>
            {
                (x - 1, y - 1), // up left
                (x -1, y + 1), // down left
                (x + 1, y - 1), // up right
                (x + 1, y + 1) // down right
            };

            // Filter neighbors to ensure they are within bounds
            return neighbors.Where(n =>
                n.Item1 >= 0 && n.Item1 < width &&
                n.Item2 >= 0 && n.Item2 < height
            ).ToList();
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

        //Returns coordinates of the neighbors of a point in a plus shape
        public static List<(int, int)> getNeighborsSquareSafe((int, int) point, int width, int height)
        {
            int x = point.Item1;
            int y = point.Item2;
            var neighbors = new List<(int, int)>
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

            // Filter neighbors to ensure they are within bounds
            return neighbors.Where(n =>
                n.Item1 >= 0 && n.Item1 < width &&
                n.Item2 >= 0 && n.Item2 < height
            ).ToList();
        }

        //Returns coordinates of the neighbors of a point in a plus shape
        public List<(int, int)> getNeighborsSquareSafe(int width, int height, bool abs)
        {
            var neighbors = new List<(int, int)>
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

            // Filter neighbors to ensure they are within bounds
            return neighbors.Where(n =>
                n.Item1 >= 0 && n.Item1 < width &&
                n.Item2 >= 0 && n.Item2 < height
            ).ToList();
        }

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

        //Searches for neighbors that are valid points in a plus pattern  
        //If plus == true then search using the getNeighborsPlus pattern  
        //Returns a province id if there is at least one valid province, returns -1 if there are no valid provinces  
        public static int getNeighborValidPoint(Dictionary<(int,int),int> validPoints, (int, int) point)
        {
            //string query = $"SELECT provinceId from  Points WHERE (x = (@x + 1) AND y = @y) OR (x = (@x - 1) AND y = @y) OR (x = @x AND y = (@y + 1)) OR (x = @x AND y = (@y - 1)) AND land = true;";
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
                double distance = calculateHaversineDistance(latRadians, lonRadians, point.LatitudeRadians, point.LongitudeRadians,true);

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

        //https://en.wikipedia.org/wiki/Haversine_formula
        //Calculates the distance between 2 points on the world given latitude and longitude.
        //KM determines whether or not we send it in KM or M
        public static double calculateHaversineDistance(double lat1, double lon1, double lat2, double lon2, bool KM)
        {
            const double R = 6371e3; // Earth radius in meters

            double dLat = lat2 - lat1;
            double dLon = lon2 - lon1;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            if (KM)
            {
                return (R * c)/1000.0;
            }
            else
            {
                return R * c;
            }
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            //Output coordinates
            if ((x + y) != -2)//If both are undefined this will be the value
            {
                if (x == -1 && y == -1)
                {
                    stringBuilder.AppendLine($"Point ({x},{y}):\n");
                } else if (x == -1 && y == -1)
                {
                    stringBuilder.AppendLine($"Point ({x},{y}):\n");
                }
                else
                {
                    stringBuilder.AppendLine($"Point ({x},{y})|({x},{y}):");
                }
            }
            //Output id
            if(id != -1 || worldPointId != -1)
            {
                if(id == -1)
                {
                    stringBuilder.AppendLine($"WorldPointId:{worldPointId}");
                }
                else if (worldPointId == -1)
                {
                    stringBuilder.AppendLine($"Id:{id}");
                }
                else
                {
                    stringBuilder.AppendLine($"Id:{id}|{worldPointId}");
                }               
            }
            if(PointType.undefined != type)
            {
                stringBuilder.AppendLine($"Type:{getTerrainDescription()}");
            }
            if(coastalDistance != -1)
            {
                stringBuilder.AppendLine($"Coastal Distance:{coastalDistance} km");
            }
            if ((length != -1 && width != -1) || (area != -1))
            {
                stringBuilder.AppendLine(getDimensionsDescription());
            }
            if (height != -1)
            {
                stringBuilder.AppendLine($"Height:{height} m above sea level");
            }
            if(averageTemperatureSummer != -1 || averageTemperatureWinter != -1)
            {
                stringBuilder.AppendLine($"Average Temperature Summer:{averageTemperatureSummer} C Winter:{averageTemperatureWinter} C");
            } 
            if(averageRainfall != -1)
            {
                stringBuilder.AppendLine($"Average Rainfall:{averageRainfall} mm/year");
            }
            return stringBuilder.ToString();

        }

        private string getTerrainDescription()
        {
            switch (type)
            {
                case PointType.land:
                    if(coastalDistance == 0)
                    {
                        return "Coastline";
                    }
                    return "Land";
                case PointType.ocean:
                    return $"Ocean ({waterSalinity}%s)";
                case PointType.lake:
                    return $"Lake({waterSalinity}%s)";
                default:
                    return "Unknown";
            }
        }

        private string getDimensionsDescription()
        {
            StringBuilder dimensions = new StringBuilder();
            if (width != -1 && length != -1)
            {
                dimensions.Append($"Width(N/S):{width}m Length(E/W):{length}m ");
            }
            else if (area != -1)
            {
                dimensions.Append($"Area:{area}m^2 ");
            }
            return dimensions.ToString();
        }

        public static PointType stringToPointType(string typeString)
        {
            switch (typeString.ToLower())
            {
                case "land":
                    return PointType.land;
                case "ocean":
                    return PointType.ocean;
                case "lake":
                    return PointType.lake;
                default:
                    return PointType.undefined;
            }
        }

        public static TerrainType stringToTerrainType(string typeString)
        {
            switch (typeString.ToLower())
            {
                case "flatland":
                    return TerrainType.flatland;
                case "hills":
                    return TerrainType.hills;
                case "mountains":
                    return TerrainType.mountains;
                default:
                    return TerrainType.undefined;
            }
        }

        //Returns x y tuple
        public (int,int) getCoordinates()
        {
            return (x, y);
        }

    }

    enum PointType{
        undefined = 0,
        land = 1,
        ocean = 2,
        lake = 3,
    }

    enum TerrainType
    {
        undefined = 0,
        flatland = 1,
        hills = 2,
        mountains  = 3
    }

}

using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class Map
    {
        private Point[][] pointMap;
        public int width { get; private set; }
        public int height { get; private set; }

        public Map(DBConnection db)
        {

            width = db.getIntFromQuery("SELECT MAX(x) FROM \"Points\";") + 1;
            height = db.getIntFromQuery("SELECT MAX(y) FROM \"Points\";") + 1;


            string query = "SELECT p1.id, p1.worldPointId, p1.x, p1.y, p1.land, p1.waterSalinity, p1.provinceId, p1.latitude, p1.longitude, p1.coastalDistance, p1.width, p1.length, p1.area, p1.height, p1.summerSolsticeAverageTemperature, p1.winterSolsticeAverageTemperature, p1.averageRainfall, p1.type, p1.terraintype  FROM \"Points\" p1 order by X,Y;";
            pointMap = new Point[height][];
            for (int i = 0; i < height; i++)
            {
                pointMap[i] = new Point[width];
            }

            NpgsqlDataReader rdr = db.runQueryCommand(query);

            //As we want this to work for the database in any condition, we must check each nullable field for null values
            while (rdr.Read())
            {


                int id = rdr.GetInt32(0);
                int worldPointId = rdr.GetInt32(1);
                int x = rdr.GetInt32(2);
                int y = rdr.GetInt32(3);
                bool land = rdr.GetBoolean(4);
                float waterSalinity = !rdr.IsDBNull(5) ? rdr.GetFloat(5) : -1.0f;
                int provinceId = rdr.GetInt32(6);
                double latitude = !rdr.IsDBNull(7) ? rdr.GetDouble(7) : -1.0;
                double longitude = !rdr.IsDBNull(8) ? rdr.GetDouble(8) : -1.0;
                double coastalDistance = !rdr.IsDBNull(9) ? rdr.GetInt32(9) : -1.0;
                int widthVal = rdr.IsDBNull(10) ? rdr.GetInt32(10) : -1;
                int length = !rdr.IsDBNull(11) ? rdr.GetInt32(11) : -1;
                int area = !rdr.IsDBNull(12) ? rdr.GetInt32(12) : -1;
                int heightVal = !rdr.IsDBNull(13) ? rdr.GetInt32(13) : -1;
                double summerSolsticeAverageTemperature = !rdr.IsDBNull(14) ? rdr.GetDouble(14) : -1.0;
                double winterSolsticeAverageTemperature = !rdr.IsDBNull(15) ? rdr.GetDouble(15) : -1.0;
                double averageRainfall = !rdr.IsDBNull(16) ? rdr.GetDouble(16) : -1.0;
                PointType type = !rdr.IsDBNull(17) ? Point.stringToPointType(rdr.GetString(17)) : PointType.undefined;
                TerrainType terrainType = !rdr.IsDBNull(18) ? Point.stringToTerrainType(rdr.GetString(18)) : TerrainType.undefined;

                pointMap[y][x] = new Point(id, worldPointId, x, y, land, waterSalinity, type, provinceId, latitude, longitude, coastalDistance, widthVal, length, area, heightVal, summerSolsticeAverageTemperature, winterSolsticeAverageTemperature, averageRainfall, terrainType);
                if (x == 700 && (y % 100) == 0)
                {
                    Console.WriteLine(pointMap[y][x]);
                }
            }
            rdr.Close();
        }

        public Point getPoint(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                throw new ArgumentOutOfRangeException("Coordinates are out of bounds of the map.");
            }
            return pointMap[y][x];
        }

        //Returns a list of points from the map orthagonally given a point x,y
        public List<Point> getNeighborsPlus(int x, int y)
        {
            var points = Point.getNeighborsPlusSafe((x, y), width, height);
            return points.Select(p => getPoint(p.Item1, p.Item2)).ToList();
        }

        public List<Point> getNeighborsPlus(Point p)
        {
            var points = Point.getNeighborsPlusSafe((p.X, p.Y), width, height);
            return points.Select(p => getPoint(p.Item1, p.Item2)).ToList();
        }

        //Returns a list of points from the map diagonally given a point x,y
        public List<Point> getNeighborsX(int x, int y)
        {
            var points = Point.getNeighborsXSafe((x, y), width, height);
            return points.Select(p => getPoint(p.Item1, p.Item2)).ToList();
        }

        public List<Point> getNeighborsSquare(int x, int y)
        {
            var points = Point.getNeighborsSquareSafe((x, y), width, height);
            return points.Select(p => getPoint(p.Item1, p.Item2)).ToList();
        }

        public List<Point> getNeighborsSquare(Point p)
        {
            var points = Point.getNeighborsSquareSafe((p.X, p.Y), width, height);
            return points.Select(p => getPoint(p.Item1, p.Item2)).ToList();
        }

        public Point getPointInDirection(int x, int y, Direction direction)
        {
            switch (direction)
            {
                case Direction.north:
                    y--;
                    break;
                case Direction.northeast:
                    x++;
                    y--;
                    break;
                case Direction.east:
                    x++;
                    break;
                case Direction.southeast:
                    x++;
                    y++;
                    break;
                case Direction.south:
                    y++;
                    break;
                case Direction.southwest:
                    x--;
                    y++;
                    break;
                case Direction.west:
                    x--;
                    break;
                case Direction.northwest:
                    x--;
                    y--;
                    break;
            }
            return getPoint(x, y);
        }

        //Finds the direction from x1, y1 to x2, y2
        //Returns center if it is the same point
        //Returns undefined if it is not exactly one point
        public Direction findRelativeSinglePointDirection(int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;

            switch (dx, dy)
            {
                case (0, 0):
                    return Direction.center;
                case (0, -1):
                    return Direction.north;
                case (1, -1):
                    return Direction.northeast;
                case (1, 0):
                    return Direction.east;
                case (1, 1):
                    return Direction.southeast;
                case (0, 1):
                    return Direction.south;
                case (-1, 1):
                    return Direction.southwest;
                case (-1, 0):
                    return Direction.west;
                case (-1, -1):
                    return Direction.northwest;
                default:
                    return Direction.undefined; // Not a valid single point direction
            }
        }

        public Point getValueFromSinglePointDirection(int x, int y, Direction direction)
        {
            switch (direction)
            {
                case Direction.north:
                    return getPoint(x, y - 1);
                case Direction.northeast:
                    return getPoint(x + 1, y - 1);
                case Direction.east:
                    return getPoint(x + 1, y);
                case Direction.southeast:
                    return getPoint(x + 1, y + 1);
                case Direction.south:
                    return getPoint(x, y + 1);
                case Direction.southwest:
                    return getPoint(x - 1, y + 1);
                case Direction.west:
                    return getPoint(x - 1, y);
                case Direction.northwest:
                    return getPoint(x - 1, y - 1);
                default:
                    throw new ArgumentException("Invalid direction for single point retrieval.");
            }
        }

        //Gets a list of all points ordered by height
        public List<Point> getListOfPointsByHeight()
        {
            return getListOfPoints().OrderBy(p => p.Height).ToList();
        }

        public List<Point> getListOfPoints()
        {
            var allPoints = new List<Point>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var point = pointMap[y][x];
                    if (point != null)
                    {
                        allPoints.Add(point);
                    }
                }
            }
            return allPoints;
        }
    }
    public enum Direction
    {
        north = 0,
        northeast = 1,
        east = 2,
        southeast = 3,
        south = 4,
        southwest = 5,
        west = 6,
        northwest = 7,
        center = 8,
        undefined = 9
    }
}

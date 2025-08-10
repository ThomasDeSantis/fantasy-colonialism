using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Npgsql;
using System.ComponentModel;

namespace FantasyColonialismMapgen
{
    
    class RiverErosionGen
    {

        
        string parentDirectory;
        IConfiguration config;
        Map map;
        int height;
        int width;
        List<Lake> lakes = new List<Lake>();
        Dictionary<int,int> pointToLake = new Dictionary<int, int>(); // Maps point ID to lake ID
        public RiverErosionGen(DBConnection db, IConfiguration config, string parentDirectory)
        {
            this.parentDirectory = parentDirectory;
            this.config = config;
            map = new Map(db);

            height = map.height;
            width = map.width;
            r = new Random();

        }

        public void generateRiversAndLakes(DBConnection db)
        {
            Console.WriteLine($"Begin river generation cycle: {DateTime.UtcNow.ToString()}");
            riverErosionGenerationCycle(db);

        }

        private void riverErosionGenerationCycle(DBConnection db)
        {
            for(int i = 0; i < 1000; i++)
            {
                Dictionary<(int,int),Direction> downhillFlowDirection = generateDownhillFlowDirections();
                generateWaterRunoff(downhillFlowDirection, db);

                int sinkCount = 0;

                List<Point> sinkPoints = getPointsWithoutFlowDirection(downhillFlowDirection, map.getListOfPointsByHeight());
                lakes = generateLakesWithVolume(downhillFlowDirection, sinkPoints);
                generateRivers(downhillFlowDirection,i);

            }
        }

        //Generate a dictionary of downhill flow directions for each point on the map.
        //A cardinal direction indicates which direction it flows
        //An undefined direction indicates that the point is invalid to receive flow
        //A center direction indicates that the point has nowhere to flow.
        private Dictionary<(int,int),Direction> generateDownhillFlowDirections()
        {
            Dictionary<(int,int), Direction> downhillFlowDirection = new Dictionary<(int, int), Direction>();
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Point p = map.getPoint(j, i);
                    //Check if point is land
                    if (!p.Land)
                    {
                        downhillFlowDirection[(j,i)] = Direction.undefined; // Ocean or water points do not flow further
                        continue;
                    }
                    //Find the lowest neighboring point
                    //TODO: Add variation so that the flow direction is not always the constant
                    int x = j, y = i;
                    decimal minHeight = p.Height;

                    foreach (Point neighbor in map.getNeighborsPlus(j,i))
                    {
                        if (neighbor.Height < minHeight)
                        {
                            minHeight = neighbor.Height;
                            x = neighbor.X;
                            y = neighbor.Y;
                        }
                    }
                    downhillFlowDirection[(j, i)] = map.findRelativeSinglePointDirection(j, i, x, y);
                }
            }
            return downhillFlowDirection;
        }

        private void generateWaterRunoff(Dictionary<(int, int), Direction> downhillFlowDirection, DBConnection db)
        {
            List<Point> pointsByHeight = map.getListOfPointsByHeight();
            // Initialize each point's water flux to its local rainfall contribution (volume per second).
            foreach (Point p in pointsByHeight)
            {
                // rainfall (mm/year) -> m/year by dividing 1000, then to m^3/sec:
                double rainVolumePerYear = (p.AverageRainfall / 1000.0) * p.Area; // m^3 of water per year
                double rainVolumePerSecond = rainVolumePerYear / (365 * 24 * 3600);
                p.WaterRunoff = rainVolumePerSecond;
            }

            //Now we need to carry the water down to its downhill neighbor
            foreach (Point p in pointsByHeight)
            {
                //If direction is center then it is a sink and stays in the point, where it may become a potential lake
                //If it is undefined it is invalid
                Direction dir = downhillFlowDirection[(p.X,p.Y)];
                if (dir != Direction.center || dir != Direction.undefined)
                {
                    Point dP = map.getPointInDirection(p.X, p.Y, dir);
                    // pass all its water to the downhill neighbor
                    dP.WaterRunoff += p.WaterRunoff;
                }
            }

            return;
        }

        public List<Lake> generateLakesWithVolume(Dictionary<(int, int), Direction> downhillFlowDirection, List<Point> sinkPoints)
        {
            //Key: point id
            //Value: whether it has been visited or not
            HashSet<(int,int)> visited = new HashSet<(int, int)>();

            Console.WriteLine($"Found {sinkPoints.Count} sink points to process for lakes.");

            List<Lake> lakeList = new List<Lake>();

            foreach (Point sink in sinkPoints)
            {
                //This sink has already been processed so we may ignore it
                if (visited.Contains(sink.getCoordinates())){
                    continue;
                }

                lakeList.Add(floodFillLake(sink, visited));
            }

            return lakeList;
        }

        private List<(int,int)> queryPointsByHeight(DBConnection db)
        {
            List<(int, int)> pointsByHeight = new List<(int, int)>();
            string query = "SELECT x, y FROM \"Points\" WHERE land = true ORDER BY height DESC;";
            NpgsqlDataReader rdr = db.runQueryCommand(query);
            while (rdr.Read())
            {
                int x = rdr.GetInt32(0);
                int y = rdr.GetInt32(1);
                pointsByHeight.Add((x, y));
            }
            rdr.Close();
            return pointsByHeight;
        }

        //Finds points that are sinks, i.e. have no neighbors that are lower than them
        private List<Point> getPointsWithoutFlowDirection(Dictionary<(int, int), Direction> downhillFlowDirection, List<Point> points)
        {
            List<Point> sinkPoints = new List<Point>();
            foreach(Point p in points)
            {
                //If the point has no flow direction, it is a sink
                if (downhillFlowDirection[(p.X, p.Y)] == Direction.center)
                {
                    sinkPoints.Add(p);
                }
            }
            return sinkPoints;
        }

        private Lake floodFillLake(Point sink, HashSet<(int, int)> visited)
        {
            //Create new priority queue with the priority being the height of the point
            PriorityQueue<Point, int> pointQueue = new PriorityQueue<Point, int>();
            HashSet<(int,int)> basinVisited = new HashSet<(int,int)>();

            int currentWaterLevel = sink.Height;
            int lowestRimHeight = int.MaxValue;
            Point rimOutflow = null;

            pointQueue.Enqueue(sink, sink.Height);
            visited.Add(sink.getCoordinates());
            basinVisited.Add(sink.getCoordinates());

            List<Point> basinPoints = new List<Point>();

            int lakeId = lakes.Count;

            while(pointQueue.Count > 0)
            {
                Point point = pointQueue.Dequeue();

                if (point.Height > currentWaterLevel)
                {
                    currentWaterLevel = point.Height;
                }

                basinPoints.Add(point);
                pointToLake[point.Id] = lakes.Count; // Map point ID to the current lake ID
                point.LakeId = lakes.Count;
                


                foreach (Point neighbor in map.getNeighborsSquare(point))
                {
                    (int,int) neighborCoords = neighbor.getCoordinates();

                    if (basinVisited.Contains(neighborCoords))
                    {
                        continue;
                    }

                    basinVisited.Add(neighborCoords);
                    visited.Add(neighborCoords);

                    if (!neighbor.Land)
                    {
                        if (neighbor.Height < lowestRimHeight)
                        {
                            lowestRimHeight = neighbor.Height;
                        }
                        continue;
                    }

                    if (neighbor.Height <= currentWaterLevel)
                    {
                        pointQueue.Enqueue(neighbor, neighbor.Height);
                    }
                    else
                    {
                        if (neighbor.Height < lowestRimHeight)
                        {
                            lowestRimHeight = neighbor.Height;
                            rimOutflow = neighbor;
                        }
                        pointQueue.Enqueue(neighbor, neighbor.Height);
                    }
                }
            }
            // Calculate inflow
            double V_in = basinPoints.Sum(c => c.WaterRunoff);
            if (V_in <= 0.0)
            {
                return null;
            }

            // Calculate capacity to rim
            double V_rim = basinPoints
                .Where(c => c.Height < lowestRimHeight)
                .Sum(c => (double)(lowestRimHeight - c.Height) * c.Area);

            int surfaceHeight;
            double finalVolume;
            double outflow;
            int minHeight = basinPoints.Min(c => c.Height);
            if (V_in >= V_rim)
            {
                surfaceHeight = lowestRimHeight;
                finalVolume = V_rim;
                outflow = V_in - V_rim;
            }
            else
            {
                surfaceHeight = findFillHeight(basinPoints, V_in, minHeight, lowestRimHeight);
                finalVolume = V_in;
                outflow = 0.0;
            }

            Lake lake = new Lake(lakeId,basinPoints, surfaceHeight, minHeight, outflow, finalVolume);

            //Set depth and water level for each point in the lake
            foreach (Point p in basinPoints)
            {
                p.LakeDepth = surfaceHeight - p.Height;
            }

            if (outflow > 0 && rimOutflow != null)
            {
                rimOutflow.WaterRunoff += outflow;
                lake.OutflowPoint = rimOutflow;
            }

            return lake;
        }


        // Finds the fill height for a partial lake when V_in < V_rim
        //TODO: Account for porousness of the soil
        private int findFillHeight(List<Point> basinPoints, double V_in, int minHeight, int rimHeight)
        {
            var sorted = basinPoints.OrderBy(c => c.Height).ToList();
            double volumeRemaining = V_in;
            int currentHeight = minHeight;

            for (int i = 1; i <= sorted.Count; i++)
            {
                int nextHeight = (i < sorted.Count) ? sorted[i].Height : rimHeight;
                int cellsBelow = i;
                double volumeNeeded = (nextHeight - currentHeight) * cellsBelow * sorted.Average(c => c.Area); 

                if (volumeRemaining >= volumeNeeded)
                {
                    volumeRemaining -= volumeNeeded;
                    currentHeight = nextHeight;
                }
                else
                {
                    currentHeight += (int)(volumeRemaining / (double)(cellsBelow * sorted[0].Area));
                    break;
                }
            }

            return currentHeight;
        }

        //TODO: Have some attrition on the outflow by ground absorption
        private void generateRivers(Dictionary<(int, int), Direction> downhillFlowDirection, int iteration)
        {
            //Must be recalculated as heights can change
            List<Point> pointsOrderedByHeight = map.getListOfPointsByHeight(); 
            foreach (Point point in pointsOrderedByHeight)
            {
                //If point is land and has a downhill flow direction, it may be a river source
                if (point.Land && point.LakeId == -1 && point.RiverId == -1 && isHeadwater(point, downhillFlowDirection))
                {
                    River newRiver = new River(rivers.Count, point);
                    while(newRiver.OutflowPoint == null)
                    {
                        //If river id is already set then the point is already assigned to a river, and this river is now a tributary of the river
                        if (point.RiverId != -1)
                        {
                            newRiver.OutflowPoint = point;
                            newRiver.TributaryType = WaterType.river;
                            newRiver.TributaryId = point.RiverId;
                        }
                        //If point is not land it has outflowed into an ocean
                        else if (!point.Land)
                        {
                            newRiver.OutflowPoint = point;
                            newRiver.TributaryType = WaterType.ocean;
                        }
                        //If lake id is not -1 then we know it is a valid lake
                        else if(point.LakeId != -1)
                        {
                            newRiver.OutflowPoint = point;
                            newRiver.TributaryId = point.LakeId;
                            newRiver.TributaryType = WaterType.lake;
                        }
                        else // If no cases apply then the point is valid to be added to the river
                        {
                            newRiver.addRiverPoint(point);
                            point.RiverId = newRiver.Id;
                            point.RiverLastIteration = iteration;
                            if (iteration == 0)
                            {
                                //point.Riv
                            }

                        }
                    }

                }
            }
        }

        private bool isHeadwater(Point p, Dictionary<(int, int), Direction> downhillFlowDirection)
        {
            //A point is a headwater if it has water flow < 0 and it has no neighbors flowing into it
            if (p.WaterRunoff <= 0 && downhillFlowDirection[(p.X, p.Y)] != Direction.center)
            {
                //Check if any neighbors flow into it
                foreach (Point neighbor in map.getNeighborsPlus(p))
                {
                    if (downhillFlowDirection[(neighbor.X, neighbor.Y)] == map.findRelativeSinglePointDirection(neighbor.X, neighbor.Y, p.X, p.Y))
                    {
                        return false; // A neighbor flows into this point
                    }
                }
                return true; // No neighbors flow into this point
            }
            else
            {
                return false; // Not a headwater if water runoff is not negative or it has neighbors flowing into it
            }
        }

    }
}
        }
    }
}

            height = map.height;
            width = map.width;

        }

        public void generateRiversAndLakes(DBConnection db)
        {
            Console.WriteLine($"Begin river generation cycle: {DateTime.UtcNow.ToString()}");
            riverErosionGenerationCycle(db);

        }

        private void riverErosionGenerationCycle(DBConnection db)
        {
            for(int i = 0; i < 1000; i++)
            {
                Dictionary<(int,int),Direction> downhillFlowDirection = generateDownhillFlowDirections();
                generateWaterRunoff(downhillFlowDirection, db);
                List<Lake> lakes = generateLakes(downhillFlowDirection);
            }
        }

        //Generate a dictionary of downhill flow directions for each point on the map.
        //A cardinal direction indicates which direction it flows
        //An undefined direction indicates that the point is invalid to receive flow
        //A center direction indicates that the point has nowhere to flow.
        private Dictionary<(int,int),Direction> generateDownhillFlowDirections()
        {
            Dictionary<(int,int), Direction> downhillFlowDirection = new Dictionary<(int, int), Direction>();
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Point p = map.getPoint(j, i);
                    //Check if point is land
                    if (!p.Land)
                    {
                        downhillFlowDirection[(j,i)] = Direction.undefined; // Ocean or water points do not flow further
                        continue;
                    }
                    //Find the lowest neighboring point
                    //TODO: Add variation so that the flow direction is not always the constant
                    int x = j, y = i;
                    decimal minHeight = p.Height;

                    foreach (Point neighbor in map.getNeighborsPlus(j,i))
                    {
                        if (neighbor.Height < minHeight)
                        {
                            minHeight = neighbor.Height;
                            x = neighbor.X;
                            y = neighbor.Y;
                        }
                    }
                    downhillFlowDirection[(j, i)] = map.findRelativeSinglePointDirection(j, i, x, y);
                }
            }
            return downhillFlowDirection;
        }

        private void generateWaterRunoff(Dictionary<(int, int), Direction> downhillFlowDirection, DBConnection db)
        {
            List<Point> pointsByHeight = map.getListOfPointsByHeight();
            // Initialize each point's water flux to its local rainfall contribution (volume per second).
            foreach (Point p in pointsByHeight)
            {
                // rainfall (mm/year) -> m/year by dividing 1000, then to m^3/sec:
                double rainVolumePerYear = (p.AverageRainfall / 1000.0) * p.Area; // m^3 of water per year
                double rainVolumePerSecond = rainVolumePerYear / (365 * 24 * 3600);
                p.WaterRunoff = rainVolumePerSecond;
            }

            //Now we need to carry the water down to its downhill neighbor
            foreach (Point p in pointsByHeight)
            {
                //If direction is center then it is a sink and stays in the point, where it may become a potential lake
                //If it is undefined it is invalid
                Direction dir = downhillFlowDirection[(p.X,p.Y)];
                if (dir != Direction.center || dir != Direction.undefined)
                {
                    Point dP = map.getPointInDirection(p.X, p.Y, dir);
                    // pass all its water to the downhill neighbor
                    dP.WaterRunoff += p.WaterRunoff;
                }
            }

            return;
        }

        public List<Lake> generateLakesWithVolume(Dictionary<(int, int), Direction> downhillFlowDirection)
        {
            //Key: point id
            //Value: whether it has been visited or not
            HashSet<(int,int)> visited = new HashSet<(int, int)>();
            List<Point> sinkPoints = getPointsWithoutFlowDirection(downhillFlowDirection, map.getListOfPointsByHeight());

            Console.WriteLine($"Found {sinkPoints.Count} sink points to process for lakes.");

            List<Lake> lakeList = new List<Lake>();

            foreach (Point sink in sinkPoints)
            {
                //This sink has already been processed so we may ignore it
                if (visited.Contains(sink.getCoordinates())){
                    continue;
                }

                lakeList.Add(floodFillLake(sink, visited));
            }

            return lakeList;
        }

        private List<(int,int)> queryPointsByHeight(DBConnection db)
        {
            List<(int, int)> pointsByHeight = new List<(int, int)>();
            string query = "SELECT x, y FROM \"Points\" WHERE land = true ORDER BY height DESC;";
            NpgsqlDataReader rdr = db.runQueryCommand(query);
            while (rdr.Read())
            {
                int x = rdr.GetInt32(0);
                int y = rdr.GetInt32(1);
                pointsByHeight.Add((x, y));
            }
            rdr.Close();
            return pointsByHeight;
        }

        //Finds points that are sinks, i.e. have no neighbors that are lower than them
        private List<Point> getPointsWithoutFlowDirection(Dictionary<(int, int), Direction> downhillFlowDirection, List<Point> points)
        {
            List<Point> sinkPoints = new List<Point>();
            foreach(Point p in points)
            {
                //If the point has no flow direction, it is a sink
                if (downhillFlowDirection[(p.X, p.Y)] == Direction.center)
                {
                    sinkPoints.Add(p);
                }
            }
            return sinkPoints;
        }

        private Lake floodFillLake(Point sink, HashSet<(int, int)> visited)
        {
            //Create new priority queue with the priority being the height of the point
            PriorityQueue<Point, int> pointQueue = new PriorityQueue<Point, int>();
            HashSet<(int,int)> basinVisited = new HashSet<(int,int)>();

            int currentWaterLevel = sink.Height;
            int lowestRimHeight = int.MaxValue;
            Point rimOutflow = null;

            pointQueue.Enqueue(sink, sink.Height);
            visited.Add(sink.getCoordinates());
            basinVisited.Add(sink.getCoordinates());

            List<Point> basinPoints = new List<Point>();

            while(pointQueue.Count > 0)
            {
                Point point = pointQueue.Dequeue();

                if (point.Height > currentWaterLevel)
                {
                    currentWaterLevel = point.Height;
                }

                basinPoints.Add(point);

                foreach (Point neighbor in map.getNeighborsSquare(point))
                {
                    (int,int) neighborCoords = neighbor.getCoordinates();

                    if (basinVisited.Contains(neighborCoords))
                    {
                        continue;
                    }

                    basinVisited.Add(neighborCoords);
                    visited.Add(neighborCoords);

                    if (!neighbor.Land)
                    {
                        if (neighbor.Height < lowestRimHeight)
                        {
                            lowestRimHeight = neighbor.Height;
                        }
                        continue;
                    }

                    if (neighbor.Height <= currentWaterLevel)
                    {
                        pointQueue.Enqueue(neighbor, neighbor.Height);
                    }
                    else
                    {
                        if (neighbor.Height < lowestRimHeight)
                        {
                            lowestRimHeight = neighbor.Height;
                            rimOutflow = neighbor;
                        }
                        pointQueue.Enqueue(neighbor, neighbor.Height);
                    }
                }
            }
            // Calculate inflow
            double V_in = basinPoints.Sum(c => c.WaterRunoff);
            if (V_in <= 0.0)
            {
                return null;
            }

            // Calculate capacity to rim
            double V_rim = basinPoints
                .Where(c => c.Height < lowestRimHeight)
                .Sum(c => (double)(lowestRimHeight - c.Height) * c.Area);

            int surfaceHeight;
            double finalVolume;
            double outflow;
            int minHeight = basinPoints.Min(c => c.Height);
            if (V_in >= V_rim)
            {
                surfaceHeight = lowestRimHeight;
                finalVolume = V_rim;
                outflow = V_in - V_rim;
            }
            else
            {
                surfaceHeight = findFillHeight(basinPoints, V_in, minHeight, lowestRimHeight);
                finalVolume = V_in;
                outflow = 0.0;
            }

            Lake lake = new Lake(basinPoints, surfaceHeight, minHeight, outflow, finalVolume);

            if (outflow > 0 && rimOutflow != null)
            {
                rimOutflow.WaterRunoff += outflow;
                lake.OutflowPoint = rimOutflow;
            }

            return lake;
        }


        // Finds the fill height for a partial lake when V_in < V_rim
        //TODO: Account for porousness of the soil
        private int findFillHeight(List<Point> basinPoints, double V_in, int minHeight, int rimHeight)
        {
            var sorted = basinPoints.OrderBy(c => c.Height).ToList();
            double volumeRemaining = V_in;
            int currentHeight = minHeight;

            for (int i = 1; i <= sorted.Count; i++)
            {
                double nextHeight = (i < sorted.Count) ? (double)sorted[i].Height : rimHeight;
                int cellsBelow = i;
                double volumeNeeded = (nextHeight - currentHeight) * cellsBelow * sorted.Average(c => c.Area); 

                if (volumeRemaining >= volumeNeeded)
                {
                    volumeRemaining -= volumeNeeded;
                    currentHeight = nextHeight;
                }
                else
                {
                    currentHeight += volumeRemaining / (cellsBelow * sorted[0].Area);
                    break;
                }
            }

            return currentHeight;
        }

    }
}

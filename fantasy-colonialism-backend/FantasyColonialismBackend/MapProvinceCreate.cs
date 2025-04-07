using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FantasyColonialismBackend
{
    class MapProvinceCreate
    {
        enum gridOrientation
        {
            topLeft,
            topRight,
            bottomLeft,
            bottomRight,
            topMiddle,
            bottomMiddle,
            leftMiddle,
            rightMiddle
        }
        private static string pointInsertQuery = "INSERT INTO points (x, y,provinceId) VALUES (@x, @y,@provinceId)";
        private static string provinceInsertQuery = "INSERT INTO provinces (id) VALUES (@id)";
        private static string checkIfPointInAProvince = "SELECT provinceId FROM Points WHERE x = @x AND y = @y";
        private static string edgeInsertQuery = "INSERT INTO renderEdges (x1,y1,x2,y2,provinceId) VALUES (@x1,@y1,@x2,@y2,@provinceId)";
        private static string truncateRenderEdges = "TRUNCATE TABLE renderEdges";
        public static void processImageIntoPoints(string inputPath, DBConnection database)
        {
            Console.WriteLine("Image processing began: " + DateTime.UtcNow.ToString());
            int pointId = 0;// Point ID that will be stored in DB
            int provinceId = 0;//Province ID that will be stored in DB
            List<(int, int)> blackPoints = new List<(int, int)>(); //This list will store all the black points in the image
            HashSet<(int, int)> whitePoints = new HashSet<(int, int)>(); //This hashset will store all the white points in the image. Is a hashset for faster lookup times.

            var pointCmd = new MySqlCommand(pointInsertQuery, database.Connection);
            var provinceCmd = new MySqlCommand(provinceInsertQuery, database.Connection);
            using (SixLabors.ImageSharp.Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(inputPath))
            {
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        var color = image[x, y];
                        if (color.R == 255 && color.G == 255 && color.B == 255)
                        {
                            //This is a white pixel
                            //Add it to the list of white points
                            whitePoints.Add((x, y));
                        }
                        else if (color.R == 0 && color.G == 0 && color.B == 0)
                        {
                            //This is a black pixel
                            //Add it to the list of black points
                            blackPoints.Add((x, y));
                        }
                    }
                }
            }
            Console.WriteLine("White Points: " + whitePoints.Count() + "Black Points: " + blackPoints.Count());
            Console.WriteLine("Finished processing image: " + DateTime.UtcNow.ToString());
            HashSet<(int, int)> visited = new HashSet<(int, int)>();

            //Do a depth first search
            //At this point all white points will be divided into provinces and pushed into the DB
            foreach ((int, int) point in whitePoints)
            {
                if (!visited.Contains(point))
                {
                    //This point has not been visited yet
                    //Create a new province and add it to the DB
                    int currentProvinceId = provinceId++;
                    provinceCmd.Parameters.AddWithValue("@id", currentProvinceId);
                    provinceCmd.ExecuteNonQuery();
                    provinceCmd.Parameters.Clear();

                    Stack<(int, int)> stack = new Stack<(int, int)>();
                    stack.Push(point);
                    while (stack.Count > 0)
                    {
                        (int, int) currentPoint = stack.Pop();
                        if (visited.Contains(currentPoint))
                        {
                            continue;
                        }
                        visited.Add(currentPoint);
                        //Create a point in the database
                        pointCmd.Parameters.AddWithValue("@id", pointId++);
                        pointCmd.Parameters.AddWithValue("@x", currentPoint.Item1);
                        pointCmd.Parameters.AddWithValue("@y", currentPoint.Item2);
                        pointCmd.Parameters.AddWithValue("@provinceId", currentProvinceId);
                        pointCmd.ExecuteNonQuery();
                        pointCmd.Parameters.Clear();

                        //Check all the neighbors of the current point
                        foreach ((int, int) neighbor in Point.getNeighborsPlus(currentPoint))
                        {
                            if (whitePoints.Contains(neighbor) && !visited.Contains(neighbor))
                            {
                                stack.Push(neighbor);
                            }
                        }
                        if(pointId % 1000 == 0)
                        {
                            Console.WriteLine("Finished processing " + pointId + " points." + " Current province id: " + provinceId);

                        }
                    }
                }
            }
            Console.WriteLine("Finished INSERTing white points into DB: " + DateTime.UtcNow.ToString());
            //Update the average center of each province in the DB
            Province.calculateProvincesAverages(database);

            Console.WriteLine("Finished calculating province average x & y: " + DateTime.UtcNow.ToString());

            //Will be used for black point logic
            Random r = new Random();

            //Connect each black point to a bordering province
            //If the black point is not connected to a bordering province, put it in a list to be checked again after wards
            //This is to handle the case where a black point is only connected to more black points
            //But once the provinces are expanded, they may be in contact with a province
            //If we find that after running it, the list of unbound black points remains the same
            //Then we can assume that the black points are not connected to any province and can be ignored

            List<(int, int)> unboundBlackPoints = new List<(int, int)>(); //This list will store all the black points were not able to be connected to a province
            int priorBlackPoints = -1;// This will hold how many black points were unbound in the previous iteration. This will be used to make sure we are not getting into an infinite loop.

            //Condition 1 ensures that we are not stuck in an infinite loop
            //Condition 2 ensures that we have not finished connecting all the black points
            while (priorBlackPoints != unboundBlackPoints.Count && priorBlackPoints != 0)
            {
                Console.WriteLine("Current passthrough black points: " + blackPoints.Count());

                priorBlackPoints = unboundBlackPoints.Count;//At the beginning of the loop set this as the current number of unbound black points. This will only be used in the while loop.

                //For each black point that was found in the initial search
                foreach ((int, int) point in blackPoints)
                {
                    //Query possible provinces. If no valid provinces in a plus pattern, try in an x pattern.
                    int possibleProvinces = Point.getNeighborValidPoint(database, point,true);
                    if(possibleProvinces == -1)
                    {
                        possibleProvinces = Point.getNeighborValidPoint(database, point, false);
                    }

                    if(possibleProvinces == -1)
                    {
                        //If there are no valid provinces, add it to the list of unbound black points
                        unboundBlackPoints.Add(point);
                    }
                    else
                    {
                        //If there are valid provinces, add the point to the province
                        pointCmd.Parameters.AddWithValue("@id", pointId++);
                        pointCmd.Parameters.AddWithValue("@x", point.Item1);
                        pointCmd.Parameters.AddWithValue("@y", point.Item2);
                        pointCmd.Parameters.AddWithValue("@provinceId", possibleProvinces);
                        pointCmd.ExecuteNonQuery();
                        pointCmd.Parameters.Clear();

                        Console.WriteLine("Wrote black point " + (pointId - 1) + " at " + point.Item1 + "," + point.Item2 + " to " + possibleProvinces);
                    }
                }
            }

        }

        //This function will populate the borders of each province in the DB
        public static void populateEdgesTable(DBConnection database)
        {
            // Truncate the renderEdges table
            var truncateCmd = new MySqlCommand(truncateRenderEdges, database.Connection);
            truncateCmd.ExecuteNonQuery();

            List<int> provinces = Province.getListOfProvinces(database);

            string borderQuery = "SELECT x, y FROM borderPoints WHERE centerProvince = @provinceId;";
            string provinceQuery = "SELECT x, y FROM Points WHERE provinceId = @provinceId;";

            for(int i = 0; i < provinces.Count; i++)
            {
                var cmd = new MySqlCommand(borderQuery, database.Connection);
                cmd.Parameters.AddWithValue("@provinceId", provinces[i]);
                MySqlDataReader rdr = cmd.ExecuteReader();

                //Store each id, x, and y in a list
                //These are the borders for your province
                List<(int, int)> borderPoints = new List<(int, int)>();
                while (rdr.Read())
                {
                    borderPoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
                }
                rdr.Close();

                var allPointsCmd = new MySqlCommand(provinceQuery, database.Connection);
                allPointsCmd.Parameters.AddWithValue("@provinceId", provinces[i]);
                rdr = allPointsCmd.ExecuteReader();

                //Store all points of a province in a hashset so you can reference it to check if a point is bordered on that side or not
                HashSet<(int, int)> provincePoints = new HashSet<(int, int)>();
                while (rdr.Read())
                {
                    provincePoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
                }
                rdr.Close();

                foreach ((int, int) point in borderPoints)
                {
                    writeProvinceEdges(point, provincePoints, provinces[i], database);
                }
                Console.WriteLine("Finished province " + provinces[i]);
            }

        }

        //point is the point for which you are writing edges to the database
        //province points is a hash set that contains all points in the province
        //provinceid is the id for the province which the point is running in
        //database is the connection to the database
        //This function runs through a point, checks the point on each sides, and calls a function to write the edge to the database
        private static void writeProvinceEdges((int,int)point, HashSet<(int, int)> provincePoints,int provinceId, DBConnection database)
        {
            //Get the neighbors of the point
            //0 - West
            // 1 - East
            // 2 - North
            // 3 - South
            var neighbors = Point.getNeighborsPlus(point);
            //Check the western point
            //x1 y1 vs x2 y2 should be in counter clockwise order
            if (!provincePoints.Contains(neighbors[0]))
            {
                writeEdgeToDB(convertPointToGridPoint(point, gridOrientation.topLeft), convertPointToGridPoint(point, gridOrientation.bottomLeft),provinceId,database);
            }
            if (!provincePoints.Contains(neighbors[1]))
            {
                writeEdgeToDB(convertPointToGridPoint(point, gridOrientation.bottomRight), convertPointToGridPoint(point, gridOrientation.topRight), provinceId, database);
            }
            if (!provincePoints.Contains(neighbors[2]))
            {
                writeEdgeToDB(convertPointToGridPoint(point, gridOrientation.topRight), convertPointToGridPoint(point, gridOrientation.topLeft), provinceId, database);
            }
            if (!provincePoints.Contains(neighbors[3]))
            {
                writeEdgeToDB(convertPointToGridPoint(point, gridOrientation.bottomLeft), convertPointToGridPoint(point, gridOrientation.bottomRight), provinceId, database);
            }
        }

        private static void writeEdgeToDB((decimal, decimal) p1, (decimal, decimal) p2, int provinceId, DBConnection database)
        {

            var edgeCmd = new MySqlCommand(edgeInsertQuery, database.Connection);
            edgeCmd.Parameters.AddWithValue("@x1", p1.Item1);
            edgeCmd.Parameters.AddWithValue("@y1", p1.Item2);

            edgeCmd.Parameters.AddWithValue("@x2", p2.Item1);
            edgeCmd.Parameters.AddWithValue("@y2", p2.Item2);


            edgeCmd.Parameters.AddWithValue("@provinceId", provinceId);

            edgeCmd.ExecuteNonQuery();
        }

        //We want to start looking for patterns at a top left corner
        //This is defined by a point with nothing north and nothing east of it
        //TODO:Handle case where there is no top left corner
        private static (int, int) findStartingPoint(HashSet<(int, int)> borderPoints, HashSet<(int,int)> provincePoints)
        {
            var startingPoint = borderPoints.First();
            Console.WriteLine(startingPoint.ToString() + " is used to start iteration.");
            //While creating the border, start at a point in the province where there is no valid point north of it and no valid point east of it
            bool topRight = false;
            while (!topRight)
            {


                Console.WriteLine("Checking " + startingPoint.ToString() + ".");

                //Get possible neighbors for the borders
                var possiblePoints = Point.getNeighborsPlus((startingPoint.Item1, startingPoint.Item2));
                Console.WriteLine("Center point...\nBorder point: " + borderPoints.Contains(startingPoint) + ", Province point: " + provincePoints.Contains(startingPoint) + "\nOther points...\n" );
                foreach ((int, int) point in possiblePoints)
                {
                    Console.WriteLine("Border point: " + borderPoints.Contains(point) + ", Province point: " + provincePoints.Contains(point));
                }
               

                //Check if the point north of it is not in the province
                if (!borderPoints.Contains(possiblePoints[2]))
                {
                    //Check if the point to the east of it is not in the province
                    if (!borderPoints.Contains(possiblePoints[1]))
                    {
                        //Check if point north of it is in the province and diagonal to the left is a border province
                        if (provincePoints.Contains(possiblePoints[2]) && borderPoints.Contains((startingPoint.Item1 - 1, startingPoint.Item2 - 1))){
                            startingPoint = (startingPoint.Item1 - 1, startingPoint.Item2 - 1);
                            Console.WriteLine("Moving diagonally to the left.\n");
                        }
                        else
                        {
                            //Check if the point east of it is in the province and diagonal to the right is a border provice
                            if (provincePoints.Contains(possiblePoints[1]) && borderPoints.Contains((startingPoint.Item1 + 1, startingPoint.Item2 - 1)))
                            {
                                startingPoint = (startingPoint.Item1 + 1, startingPoint.Item2 - 1);
                                Console.WriteLine("Moving diagonally to the right.\n");
                            }
                            else
                            {
                                //If all possibilities are elimnated we have found the top left.
                                Console.WriteLine("Identified top right.\n");
                                topRight = true;
                            }
                        }
                    }
                    else
                    {
                        //If the point to the right is in the province, then we must move to the east
                        startingPoint = possiblePoints[1];
                        Console.WriteLine("Moving to the right.\n");
                    }
                }
                else
                {
                    //If the point above is in the province, then we must move up north
                    startingPoint = possiblePoints[2];
                    Console.WriteLine("Moving up.\n");
                }
            }
            return startingPoint;
        }

        //Query the DB for provinces and render each as a different color, using the base image as a template
        public static void renderProvinces(DBConnection database, string inputPath,string outputPath)
        {
            Random r = new Random();
            SixLabors.ImageSharp.Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(inputPath);
            List<(int,int,int)> provinces = Point.getListOfAllPointsWithProvinces(database);
            int provinceId = 0;
            int R = r.Next(0, 255);
            int G = r.Next(0, 255);
            int B = r.Next(0, 255);
            for (int i = 0; i < provinces.Count; i++)
            {
                if (provinces[i].Item1 != provinceId)
                {
                    provinceId = provinces[i].Item1;
                    R = r.Next(0, 255);
                    G = r.Next(0, 255);
                    B = r.Next(0, 255);
                }
                image[provinces[i].Item2, provinces[i].Item3] = new Rgba32((byte)R, (byte)G, (byte)B);
                if(i%100 == 0)
                {
                    Console.WriteLine("Finished rendering " + i + " points");
                }
            }
            image.SaveAsPng(outputPath);
        }

        //Convert a point into a grid point
        //A grid point is the cross point of 4 points
        //[0] is the xLeft,[1] is the xRight,[2] is the yUp,[3] is the yDown
        private static (decimal, decimal) convertPointToGridPoint((int, int) point,gridOrientation orientation)
        {

            decimal shift = .5m;
            switch (orientation)
            {
                case gridOrientation.topLeft:
                    return ((Convert.ToDecimal(point.Item1)-shift), (Convert.ToDecimal(point.Item2) - shift));
                    break;
                case gridOrientation.topRight:
                    return  ((Convert.ToDecimal(point.Item1) + shift), (Convert.ToDecimal(point.Item2) - shift));
                    break;
                case gridOrientation.bottomLeft:
                    return  ((Convert.ToDecimal(point.Item1) - shift), (Convert.ToDecimal(point.Item2) + shift));
                    break;
                case gridOrientation.bottomRight:
                    return ((Convert.ToDecimal(point.Item1) + shift), (Convert.ToDecimal(point.Item2) + shift));
                    break;
                case gridOrientation.topMiddle:
                    return ((Convert.ToDecimal(point.Item1)), (Convert.ToDecimal(point.Item2) - shift));
                    break;
                case gridOrientation.bottomMiddle:
                    return ((Convert.ToDecimal(point.Item1)), (Convert.ToDecimal(point.Item2) + shift));
                    break;
                case gridOrientation.leftMiddle:
                    return ((Convert.ToDecimal(point.Item1) - shift), (Convert.ToDecimal(point.Item2)));
                    break;
                case gridOrientation.rightMiddle:
                    return ((Convert.ToDecimal(point.Item1) + shift), (Convert.ToDecimal(point.Item2)));
                    break;
                default:return (-1m,-1m);
            }

        }


        private static string getFormattedCommandText(MySqlCommand command)
        {
            string query = command.CommandText;
            foreach (MySqlParameter parameter in command.Parameters)
            {
                query = query.Replace(parameter.ParameterName, parameter.Value.ToString());
            }
            return query;
        }

       
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FantasyColonialismBackend
{
    class MapProvinceCreate
    {
        private static string pointInsertQuery = "INSERT INTO points (id, x, y,provinceId) VALUES (@id, @x, @y,@provinceId)";
        private static string provinceInsertQuery = "INSERT INTO provinces (id) VALUES (@id)";
        private static string checkIfPointInAProvince = "SELECT provinceId FROM Points WHERE x = @x AND y = @y";
        public static void processImageIntoPoints(string inputPath, DBConnection database)
        {
            Console.WriteLine("Image processing began: " + DateTime.UtcNow.ToString());
            int pointId = 0;// Point ID that will be stored in DB
            int provinceId = 0;//Province ID that will be stored in DB
            List<(int, int)> blackPoints = new List<(int, int)>(); //This list will store all the black points in the image
            HashSet<(int, int)> whitePoints = new HashSet<(int, int)>(); //This hashset will store all the white points in the image. Is a hashset for faster lookup times.

            var pointCmd = new MySqlCommand(pointInsertQuery, database.Connection);
            var provinceCmd = new MySqlCommand(provinceInsertQuery, database.Connection);
            using (Image<Rgba32> image = Image.Load<Rgba32>(inputPath))
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
                            /*//Create a point in the database
                            cmd.Parameters.AddWithValue("@id", pointId++);
                            cmd.Parameters.AddWithValue("@x", x);
                            cmd.Parameters.AddWithValue("@y", y);
                            cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();*/
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
                    }
                }
            }

        }

        //Query the DB for provinces and render each as a different color, using the base image as a template
        public static void renderProvinces(DBConnection database, string inputPath,string outputPath)
        {
            Random r = new Random();
            Image<Rgba32> image = Image.Load<Rgba32>(inputPath);
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

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

            /*
            //This will assign each black point to the closest province's avg'd out center
            //Only exception is it must do a successful search to its center
            List<(decimal, decimal)> provinceAvgCenter = getProvinceAvgs();
            foreach ((int,int) point in blackPoints)
            {

            }*/
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

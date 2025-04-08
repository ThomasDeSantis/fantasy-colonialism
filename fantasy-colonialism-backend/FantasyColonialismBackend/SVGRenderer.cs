using Svg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Xml;

namespace FantasyColonialismBackend
{
    class SVGRenderer
    {
        private static string renderResetEdgeSproc = "CALL `sp_RENDER_RESET_EDGE`();";
        private static string consumeEdgeSproc = "CALL `sp_RENDER_CONSUME_EDGE`(@x_input,@y_input, @pid_input, @x_output, @y_output);";
        private static string startingPointQuery = "SELECT DISTINCT x, y FROM renderNodes WHERE provinceId = @provinceId LIMIT 1;";
        private static string getProvinceCount = "SELECT DISTINCT provinceId FROM renderNodes;";
        private static string getConsumeSprocResults = "SELECT @x_output, @y_output;";
        public static void renderEdges(DBConnection database, string outputPath)
        {
            //Reset the consumption status of all edges
            //Consumption tracks whether or not the edge has been represented in the SVG or not
            resetRenderConsumptions(database);
            List<SvgPointCollection> borderEdgeCollection = new List<SvgPointCollection>();

            List<int> provinces = getListOfRenderProvinces(database);


            //Create the SVG
            SvgDocument svg = new SvgDocument();

            for (int i = 0; i < provinces.Count; i++)
            {
                SvgPointCollection provincePoints = getDistinctProvinceRenderPoints(database, provinces[i]);
                //Console.WriteLine(provincePoints);
                SvgPolygon polygon = new SvgPolygon();

                polygon.Points = provincePoints;
                //Set the color of the polygon to black
                polygon.Fill = new SvgColourServer(System.Drawing.Color.Black);
                polygon.ID = provinces[i].ToString();
                polygon.CustomAttributes.Add("name", provinces[i].ToString());
                svg.Children.Add(polygon);
            }


            //Write all borders to the file
            //renderBorders(database, borderEdgeCollection, svg);

            //Save the svg file
            svg.Write(outputPath);
            Console.WriteLine("Finished SVG file: " + DateTime.UtcNow.ToString());


        }

        private static List<int> getListOfRenderProvinces(DBConnection database)
        {
            List<int> provinces = new List<int>();
            var cmd = new MySqlCommand(getProvinceCount, database.Connection);
            var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                provinces.Add(rdr.GetInt32(0));
            }
            rdr.Close();
            return provinces;
        }


        //Returns an SVG point collection of all points within a province that border the outside
        //A point which we render will be referred to as a node
        //Database is the db connection to be used
        //provinceId is the id of the province to get the points from
        private static SvgPointCollection getDistinctProvinceRenderPoints(DBConnection database, int provinceId)
        {
            SvgPointCollection points = new SvgPointCollection();
            var renderNodeCmd = new MySqlCommand(startingPointQuery, database.Connection);
            renderNodeCmd.Parameters.AddWithValue("@provinceId", provinceId);
            (decimal, decimal) currentPoint = (-1, -1);


            //Get a point to start rendering the province from
            var rdr = renderNodeCmd.ExecuteReader();
            while (rdr.Read())
            {
                currentPoint = (rdr.GetDecimal(0), rdr.GetDecimal(1));
            }
            rdr.Close();


            var consumeEdgeCmd = new MySqlCommand("sp_RENDER_CONSUME_EDGE", database.Connection);
            consumeEdgeCmd.CommandType = System.Data.CommandType.StoredProcedure;

            //Add a point collection that will be used to store the first group of borders
            //borderEdgeCollection.Add(new SvgPointCollection());

            //Continue to get points until we have consumed all edges
            while (currentPoint.Item1 != -1)
            {
                points.Add(new SvgUnit((float)currentPoint.Item1));
                points.Add(new SvgUnit((float)currentPoint.Item2));
                consumeEdgeCmd.Parameters.AddWithValue("@x_input", currentPoint.Item1);
                consumeEdgeCmd.Parameters.AddWithValue("@y_input", currentPoint.Item2);
                consumeEdgeCmd.Parameters.AddWithValue("@pid_input", provinceId);
                consumeEdgeCmd.Parameters.Add(new MySqlParameter("?x_output", MySqlDbType.VarChar));
                consumeEdgeCmd.Parameters["?x_output"].Direction = System.Data.ParameterDirection.Output;
                consumeEdgeCmd.Parameters.Add(new MySqlParameter("?y_output", MySqlDbType.VarChar));
                consumeEdgeCmd.Parameters["?y_output"].Direction = System.Data.ParameterDirection.Output;
                consumeEdgeCmd.ExecuteNonQuery();

                if (consumeEdgeCmd.Parameters["?x_output"].Value != System.DBNull.Value)
                {
                    currentPoint = (Convert.ToDecimal(consumeEdgeCmd.Parameters["?x_output"].Value), Convert.ToDecimal(consumeEdgeCmd.Parameters["?y_output"].Value));/*
                    if(Convert.ToInt32(consumeEdgeCmd.Parameters["?disjointed"].Value) == 0)
                    {
                        borderEdgeCollection[borderEdgeCollection.Count - 1].Add(new SvgUnit((float)currentPoint.Item1) * 5);
                        borderEdgeCollection[borderEdgeCollection.Count - 1].Add(new SvgUnit((float)currentPoint.Item2) * 5);
                    }
                    else
                    {
                        borderEdgeCollection.Add(new SvgPointCollection());
                    }*/
                }
                else
                {
                    currentPoint = ((-1, -1));
                }
                rdr.Close();
                consumeEdgeCmd.Parameters.Clear();
                Console.WriteLine(currentPoint);
            }
            return points;
        }

        private static void renderBorders(DBConnection database, List<SvgPointCollection> borderEdgeCollection, SvgDocument svg)
        {
            foreach (SvgPointCollection border in borderEdgeCollection)
            {
                SvgPolyline polyline = new SvgPolyline();
                polyline.Points = border;
                polyline.Stroke = new SvgColourServer(System.Drawing.Color.Gray);
                polyline.StrokeWidth = new SvgUnit(0.01f);
                svg.Children.Add(polyline);
            }
        }

        private static void resetRenderConsumptions(DBConnection dBConnection)
        {
            var cmd = new MySqlCommand(renderResetEdgeSproc, dBConnection.Connection);
            cmd.ExecuteNonQuery();
        }

        //This function takes an SVG file and converts all polygons to paths
        public static void updatePolygonToPath(string inputPath, string outputPath)
        {
            string svgContent = System.IO.File.ReadAllText(inputPath);
            var svgDoc = new XmlDocument();
            svgDoc.LoadXml(svgContent);

            var nsmgr = new XmlNamespaceManager(svgDoc.NameTable);
            nsmgr.AddNamespace("svg", svgDoc.DocumentElement.NamespaceURI);

            // Set viewBox to 0 0 1300 1000
            var svgRoot = svgDoc.DocumentElement;
            if (svgRoot != null)
            {
                svgRoot.SetAttribute("viewBox", "0 0 1300 1000");
            }

            var polys = svgDoc.SelectNodes("//svg:polygon | //svg:polyline", nsmgr);
            foreach (XmlNode poly in polys)
            {
                var path = svgDoc.CreateElement("path", svgDoc.DocumentElement.NamespaceURI);
                var pathData = "M " + poly.Attributes["points"].Value;
                if (poly.Name == "polygon")
                {
                    pathData += "z";
                }
                path.SetAttribute("d", pathData);

                foreach (XmlAttribute attr in poly.Attributes)
                {
                    if (attr.Name != "points")
                    {
                        path.SetAttribute(attr.Name, attr.Value);
                    }
                }
                path.SetAttribute("fill", "#FFF");
                path.SetAttribute("stroke", "#000");
                path.RemoveAttribute("style");
                poly.ParentNode.ReplaceChild(path, poly);
            }

            svgDoc.Save(outputPath);
        }
    }
}

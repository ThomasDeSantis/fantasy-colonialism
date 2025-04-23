using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class Province
    {
        //Write the avgX and avgY of each province to the DB
        public static void calculateProvincesAverages(DBConnection database)
        {
            //TODO: see if I can update this into a single query
            //UPDATE Provinces p1 JOIN (SELECT AVG(x) as pointsAvgX, AVG(y) as pointsAvgY, provinceId from Points GROUP BY provinceId) p2 ON p1.id = p2.provinceid SET p1.avgX = p2.pointsAvgX AND p1.avgY = p2.pointsAvgY;"

            string getAllProvincesQuery = "SELECT provinceId, FORMAT(AVG(x),0), FORMAT(AVG(y),0) FROM Points WHERE land = true GROUP BY provinceId;";
            string getXYProvinceQuery = "SELECT provinceId FROM Points WHERE x = @x and y = @y and land = true;";
            string provinceUpdateQuery = "UPDATE provinces SET avgX = @avgX, avgY = @avgY WHERE id = @id";
            var queryCmd = new MySqlCommand(getAllProvincesQuery, database.Connection);
            var queryXYCmd = new MySqlCommand(getXYProvinceQuery, database.Connection);
            var updateCmd = new MySqlCommand(provinceUpdateQuery, database.Connection);

            MySqlDataReader rdr = queryCmd.ExecuteReader();

            List<(string, string, string)> provinces = new List<(string, string, string)>();

            //For each province add it to a list in preparation for updating the DB
            while (rdr.Read())
            {
                provinces.Add((rdr[0].ToString(), rdr[1].ToString(), rdr[2].ToString()));
            }

            //Each DBConnection can only have a single connection at once, so we must close it before updating
            rdr.Close();

            foreach ((string, string, string) province in provinces)
            {
                //Check if average x and y are within the province
                queryXYCmd.Parameters.AddWithValue("@x", province.Item2);
                queryXYCmd.Parameters.AddWithValue("@y", province.Item3);
                rdr = queryXYCmd.ExecuteReader();
                queryXYCmd.Parameters.Clear();
                string checkProvinceId = null;

                if (rdr.Read())
                {
                    checkProvinceId = rdr[0].ToString();
                }
                rdr.Close();

                //If province does not contain the average x and y, then we must depth first search to find a point that does
                if (checkProvinceId != province.Item1)
                {
                    var BFSCoord = Point.findClosestPointWithinAProvince(Int32.Parse(province.Item1,System.Globalization.NumberStyles.AllowThousands), Int32.Parse(province.Item2, NumberStyles.AllowThousands), Int32.Parse(province.Item3, NumberStyles.AllowThousands), database);
                    updateCmd.Parameters.AddWithValue("@id", Int32.Parse(province.Item1, NumberStyles.AllowThousands));
                    updateCmd.Parameters.AddWithValue("@avgX", BFSCoord.Item1);
                    updateCmd.Parameters.AddWithValue("@avgY", BFSCoord.Item2);
                    updateCmd.ExecuteNonQuery();
                    updateCmd.Parameters.Clear();
                    Console.Write(province.Item1 + " did not have a direct connection so a BFS was used.\n");
                }
                else //Otherwise just use the average x and y directly
                {
                    updateCmd.Parameters.AddWithValue("@id", Int32.Parse(province.Item1, NumberStyles.AllowThousands));
                    updateCmd.Parameters.AddWithValue("@avgX", Int32.Parse(province.Item2, NumberStyles.AllowThousands));
                    updateCmd.Parameters.AddWithValue("@avgY", Int32.Parse(province.Item3, NumberStyles.AllowThousands));
                    updateCmd.ExecuteNonQuery();
                    updateCmd.Parameters.Clear();
                }
            }
            rdr.Close();
        }

        public static List<int> getListOfProvinces(DBConnection database)
        {
            List<int> provinces = new List<int>();
            var cmd = new MySqlCommand("SELECT DISTINCT id FROM provinces;", database.Connection);
            var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                provinces.Add(rdr.GetInt32(0));
            }
            rdr.Close();
            return provinces;
        }
    }
}

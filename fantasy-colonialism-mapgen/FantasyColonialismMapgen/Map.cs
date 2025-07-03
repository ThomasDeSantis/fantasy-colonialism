using Npgsql;
using Org.BouncyCastle.Math.EC;
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

            width = db.getIntFromQuery("SELECT MAX(absX) FROM \"Points\";") + 1;
            height = db.getIntFromQuery("SELECT MAX(absY) FROM \"Points\";") + 1;


            string query = "SELECT p1.id, p1.worldPointId, p1.x, p1.y, p1.absX, p1.absY, p1.land, p1.waterSalinity, p1.provinceId, p1.latitude, p1.longitude, p1.coastalDistance, p1.width, p1.length, p1.area, p1.height, p1.summerSolsticeAverageTemperature, p1.winterSolsticeAverageTemperature, p1.averageRainfall, p1.type  FROM \"Points\" p1 order by X,Y;";
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
                int absX = rdr.GetInt32(4);
                int absY = rdr.GetInt32(5);
                bool land = rdr.GetBoolean(6);
                float waterSalinity = !rdr.IsDBNull(7) ? rdr.GetFloat(7) : -1.0f;
                int provinceId = rdr.GetInt32(8);
                double latitude = !rdr.IsDBNull(9) ? rdr.GetDouble(9) : -1.0;
                double longitude = !rdr.IsDBNull(10) ? rdr.GetDouble(10) : -1.0;
                double coastalDistance = !rdr.IsDBNull(11) ? rdr.GetInt32(11) : -1.0;
                int widthVal = rdr.IsDBNull(12) ? rdr.GetInt32(12) : -1;
                int length = !rdr.IsDBNull(13) ? rdr.GetInt32(13) : -1;
                int area = !rdr.IsDBNull(14) ? rdr.GetInt32(14) : -1;
                int heightVal = !rdr.IsDBNull(15) ? rdr.GetInt32(15) : -1;
                double summerSolsticeAverageTemperature = !rdr.IsDBNull(16) ? rdr.GetDouble(16) : -1.0 ;
                double winterSolsticeAverageTemperature = !rdr.IsDBNull(17) ? rdr.GetDouble(17) : -1.0;
                double averageRainfall = !rdr.IsDBNull(18) ? rdr.GetDouble(18) : -1.0;
                PointType type = !rdr.IsDBNull(19) ? Point.stringToPointType(rdr.GetString(19)) : PointType.undefined;
                pointMap[absY][absX] = new Point(id, worldPointId, x, y, absX, absY, land, waterSalinity,type, provinceId, latitude, longitude, coastalDistance, widthVal, length, area, heightVal, summerSolsticeAverageTemperature, winterSolsticeAverageTemperature, averageRainfall, -1.0);
                if(absX == 700 && (absY % 100) == 0)
                {
                    Console.WriteLine(pointMap[absY][absX]);
                } 
            }
            rdr.Close();
        }
    }
}

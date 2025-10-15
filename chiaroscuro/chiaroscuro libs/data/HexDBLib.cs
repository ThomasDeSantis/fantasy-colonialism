using Npgsql;
using DatabaseLibraries;

namespace MapData
{
    static class HexDBLib
    {
        public static int initHex(int hexX, int hexY, DBConnection database)
        {
            string insertHexQuery = $"INSERT INTO \"Hexes\" (xOrigin, yOrigin) VALUES ({hexX}, {hexY}) RETURNING id;";
            NpgsqlDataReader rdr = database.runQueryCommand(insertHexQuery);
            int hexId = -1;
            if (rdr.Read())
            {
                hexId = rdr.GetInt32(0);
            }
            rdr.Close();
            return hexId;
        }
    }

}
﻿using MySql.Data;
using MySql.Data.MySqlClient;

namespace FantasyColonialismMapgen
{
    public class DBConnection
    {
        private DBConnection()
        {
        }

        public string Server { get; set; }
        public string DatabaseName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public MySqlConnection Connection { get; set; }

        private static DBConnection _instance = null;
        public static DBConnection Instance()
        {
            if (_instance == null)
                _instance = new DBConnection();
            return _instance;
        }

        public bool IsConnect()
        {
            if (Connection == null)
            {
                if (String.IsNullOrEmpty(DatabaseName))
                    return false;
                string connstring = string.Format("Server={0}; database={1}; UID={2}; password={3}", Server, DatabaseName, UserName, Password);
                Connection = new MySqlConnection(connstring);
                Connection.Open();
            }

            return true;
        }

        public void Close()
        {
            Connection.Close();
        }

        public void setSessionVariables()
        {
            string maxRecursionDepthQuery = "SET SESSION cte_max_recursion_depth = 1000000;";

            var sessionCmd = new MySqlCommand(maxRecursionDepthQuery, Connection);

            sessionCmd.ExecuteNonQuery();
        }

        public void runStringNonQueryCommand(string command)
        {
            MySqlCommand cmd = new MySqlCommand(command, Connection);
            cmd.ExecuteNonQuery();
        }
    }
}
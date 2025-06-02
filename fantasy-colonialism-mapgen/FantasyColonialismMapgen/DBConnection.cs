using MySql.Data;
using MySql.Data.MySqlClient;
using System.Text;

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

        public void runStringNonQueryCommandBatch(string commandPrefix,string commandSuffix, List<string> commands, int batchSize,char joinChar, bool log)
        {
            if (log)
            {
                Console.WriteLine($"Running batch command with prefix: {commandPrefix}");
                Console.WriteLine($"Total commands to process: {commands.Count}");
                Console.WriteLine($"Batch size: {batchSize}");
            }
            List<string> batchCommands = new List<string>();
            for (int i = 0; i < commands.Count; i++)
            {
                batchCommands.Add(commands[i]);
                if (i % batchSize == 0)
                {
                    if (batchCommands.Count > 0)
                    {
                        if (log)
                        {
                            Console.WriteLine($"Finished processing {i} commands at {DateTime.UtcNow.ToString()}");
                        }
                        StringBuilder batchCommandStringBuilder = new StringBuilder(commandPrefix);
                        //Finish the insert statement
                        batchCommandStringBuilder.Append(string.Join(joinChar, batchCommands));
                        batchCommandStringBuilder.Append(commandSuffix);
                        runStringNonQueryCommand(batchCommandStringBuilder.ToString());
                        batchCommands.Clear();
                        Console.WriteLine(batchCommands.Count);
                    }
                }
            }


            StringBuilder batchCommandStringBuilderFinal = new StringBuilder(commandPrefix);
            //Finish the insert statement
            batchCommandStringBuilderFinal.Append(string.Join(joinChar, batchCommands));
            batchCommandStringBuilderFinal.Append(";");
            runStringNonQueryCommand(batchCommandStringBuilderFinal.ToString());
        }
    }
}
using Npgsql;
using System.Text;

namespace FantasyColonialismMapgen
{
    public class DBConnection
    {
        private DBConnection()
        {
        }


        public NpgsqlDataSource dataSource { get; set; }

        private static DBConnection _instance = null;
        public static DBConnection Instance()
        {
            if (_instance == null)
                _instance = new DBConnection();
            return _instance;
        }

        public bool IsConnect(string connectionString)
        {
            try
            {
                if (dataSource == null)
                {
                    dataSource = NpgsqlDataSource.Create(connectionString);
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error connecting to database: {e.Message}");
                return false;
            }
        }

        public void Close()
        {
            dataSource.Clear();
        }

        /*
        public void setSessionVariables()
        {
            string maxRecursionDepthQuery = "SET SESSION max_recursion_depth = 1000000;";

            var sessionCmd = new NpgsqlCommand(maxRecursionDepthQuery, dataSource);

            sessionCmd.ExecuteNonQuery();
        }*/

        public void runStringNonQueryCommand(string command)
        {
            using var npgCommand = dataSource.CreateCommand(command);
            npgCommand.ExecuteNonQuery();
            dataSource.Clear();
        }

        public void runStringNonQueryCommandBatch(string commandPrefix, string commandSuffix, List<string> commands, int batchSize, char joinChar, bool log)
        {
            if (log)
            {
                Console.WriteLine($"Running batch command with prefix: {commandPrefix}");
                Console.WriteLine($"Total commands to process: {commands.Count}");
                Console.WriteLine($"Batch size: {batchSize}");
            }

            using var batch = dataSource.CreateBatch();
            for (int i = 0; i < commands.Count; i++)
            {
                batch.BatchCommands.Add(new NpgsqlBatchCommand(commandPrefix + commands[i] + commandSuffix + joinChar));
                if ((i + 1) % batchSize == 0)
                {
                    if (batch.BatchCommands.Count > 0)
                    {
                        if (log)
                        {
                            Console.WriteLine($"Finished processing {i + 1} commands at {DateTime.UtcNow}");
                        }
                        batch.ExecuteNonQuery();
                        batch.BatchCommands.Clear();
                    }
                }
            }
            if (batch.BatchCommands.Count > 0)
            {
                batch.ExecuteNonQuery();
                batch.BatchCommands.Clear();
            }
            dataSource.Clear();
        }


        public void runStringNonQueryCommandBatchUnformatted(List<string> commands, int batchSize, bool log)
        {
            if (log)
            {
                Console.WriteLine($"Total commands to process: {commands.Count}");
                Console.WriteLine($"Batch size: {batchSize}");
            }

            using var batch = dataSource.CreateBatch();
            for (int i = 0; i < commands.Count; i++)
            {
                batch.BatchCommands.Add(new NpgsqlBatchCommand(commands[i]));
                if (i % batchSize == 0)
                {
                    if (batch.BatchCommands.Count > 0)
                    {
                        if (log)
                        {
                            Console.WriteLine($"Finished processing {i} commands at {DateTime.UtcNow.ToString()}");
                        }
                        batch.ExecuteNonQuery();
                        Console.WriteLine(batch.BatchCommands.Count);
                        batch.BatchCommands.Clear();
                        Console.WriteLine(batch.BatchCommands.Count);
                    }
                }
            }
            if (batch.BatchCommands.Count > 0)
            {
                batch.ExecuteNonQuery();
                batch.BatchCommands.Clear();
            }
            dataSource.Clear();
        }

        public NpgsqlDataReader runQueryCommand(string command)
        {
            var npgCommand = dataSource.CreateCommand(command);
            var reader = npgCommand.ExecuteReader();
            return reader;
        }

        public void runStringNonQueryCommandBatchInsert(string insertStatement, List<string> commands, int batchSize, bool log)
        {
            if (log)
            {
                Console.WriteLine($"Running batch command with prefix: {insertStatement}");
                Console.WriteLine($"Total commands to process: {commands.Count}");
                Console.WriteLine($"Batch size: {batchSize}");
            }

            using var batch = dataSource.CreateBatch();
            List<string> batchCommands = new List<string>();
            for (int i = 0; i < commands.Count; i++)
            {
                if (i % batchSize == 0 && batchCommands.Count > 0)
                {
                    if (log)
                    {
                        Console.WriteLine($"Processing commands from {i - batchCommands.Count} to {i} at {DateTime.UtcNow}");
                    }
                    string batchStatement = insertStatement + batchCommands.Aggregate(new StringBuilder(), (sb, cmd) => sb.Append(cmd).Append(",")).ToString();
                    runStringNonQueryCommand(batchStatement.TrimEnd(','));
                    batchCommands.Clear();
                }

                batchCommands.Add(commands[i]);
            }
            if (batchCommands.Count > 0)
            {
                if (log)
                {
                    Console.WriteLine($"Processing remaining commands at {DateTime.UtcNow}");
                }
                string batchStatement = insertStatement + batchCommands.Aggregate(new StringBuilder(), (sb, cmd) => sb.Append(cmd).Append(",")).ToString();
                runStringNonQueryCommand(batchStatement.TrimEnd(','));
            }
            dataSource.Clear();
        }

        //Returns a one line int from a command
        public int getIntFromQuery(string command)
        {
            using var cmd = dataSource.CreateCommand(command);
            using var reader = cmd.ExecuteReader();
            int val;
            if (reader.Read())
            {
                val = reader.GetInt32(0);
            }
            else
            {
                throw new Exception("No results returned from query.");
            }

            reader.Close();
            return val;
        }
    }
}
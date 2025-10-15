using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Configuration.FileExtensions; - Needed as package
using Microsoft.Extensions.Configuration.Json;
using System.IO;

namespace Libraries
{
    public class AppSettings
    {
        IConfigurationBuilder builder;
        IConfiguration config;
        public AppSettings(string appsettingsBasePath, string fileName)
        {
            builder = new ConfigurationBuilder().SetBasePath(appsettingsBasePath).AddJsonFile("appsettings.json");
            Console.WriteLine(builder);
            config = builder.Build();
        }
        public string getSqlConnectionString(string connectionString)
        {
            return config.GetConnectionString(connectionString);
        }

    }

}
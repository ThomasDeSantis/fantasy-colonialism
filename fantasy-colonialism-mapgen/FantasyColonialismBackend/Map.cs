using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    //The map is a singleton that is an array of points that represents what is loaded in the database.
    //The map is not the source of truth for any point data, the database is.
    public sealed class Map
    {

        private static volatile Map instance = null;
        private static bool populated = false;
        private static object padlock = new object();
        private Point[][] points;

        private Map()
        {

        }

        public static Map Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (padlock)
                    {
                        if(instance == null)
                        {
                            instance = new Map();
                        }
                    }
                }
                return instance;
            }
        }

        public void loadMap(DBConnection database,IConfiguration config)
        {
            lock (padlock)
            {
                if (instance == null)
                {
                }
            }
        }
    }
}

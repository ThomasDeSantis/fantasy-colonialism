using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class RiverErosionGen
    {
        string parentDirectory;
        IConfiguration config;
        Map map;
        public RiverErosionGen(DBConnection db, IConfiguration config, string parentDirectory)
        {
            this.parentDirectory = parentDirectory;
            this.config = config;
            map = new Map(db);
        }
    }
}

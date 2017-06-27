using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using ElementsExplorer.Configuration;
using ElementsExplorer.Logging;

namespace ElementsExplorer
{
    public class Program
    {
        public static void Main(string[] args)
        {
			Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, (a, b) => true, false)));
			try
			{
				var conf = new ExplorerConfiguration();
				conf.LoadArgs(args);
				using(var runtime = conf.CreateRuntime())
				{
					runtime.CreateWebHost().Run();
				}
			}
			catch(ConfigException ex)
			{
				if(!string.IsNullOrEmpty(ex.Message))
					Logs.Configuration.LogError(ex.Message);
			}
			catch(Exception exception)
			{
				Logs.Explorer.LogError("Exception thrown while running the server");
				Logs.Explorer.LogError(exception.ToString());
			}			
        }
    }
}

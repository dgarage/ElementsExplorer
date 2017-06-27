using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElementsExplorer.Controllers
{
	[Route("v1")]
	public class MainController : Controller
    {
		public MainController(ExplorerRuntime runtime)
		{
			if(runtime == null)
				throw new ArgumentNullException("runtime");
			Runtime = runtime;
		}
		public ExplorerRuntime Runtime
		{
			get; set;
		}
		[Route("hello")]
		public string Hello()
		{
			return "hello";
		}
	}
}

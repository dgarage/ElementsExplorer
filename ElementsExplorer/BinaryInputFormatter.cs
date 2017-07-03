using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ElementsExplorer
{
	public class BinaryInputFormatter : InputFormatter
	{
		static readonly List<string> _SupportedTypes = new List<string>() { "application/octet-stream", "application/x-www-form-urlencoded" };
		protected override bool CanReadType(Type type)
		{
			return type == typeof(byte[]);
		}
		public override bool CanRead(InputFormatterContext context)
		{
			return true;
		}
		public override IReadOnlyList<string> GetSupportedContentTypes(string contentType, Type objectType)
		{
			return _SupportedTypes;
		}
		public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
		{
			var ms = new MemoryStream();
			await context.HttpContext.Request.Body.CopyToAsync(ms);
			return InputFormatterResult.Success(ms.ToArray());
		}
	}
}

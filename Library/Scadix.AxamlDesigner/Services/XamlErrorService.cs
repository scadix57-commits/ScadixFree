 

using System.Collections.ObjectModel;
using Scadix.AxamlDom;

namespace Scadix.AxamlDesigner.Services
{
	public class XamlErrorService : IXamlErrorSink
	{
		public XamlErrorService()
		{
			Errors = new ObservableCollection<XamlError>();
		}

		public ObservableCollection<XamlError> Errors { get; private set; }

		public void ReportError(string message, int line, int column)
		{
			Errors.Add(new XamlError() { Message = message, Line = line, Column = column });
		}
	}

	public class XamlError
	{
		public string Message { get; set; }
		public int Line { get; set; }
		public int Column { get; set; }
	}
}

namespace Scadix.AxamlDom
{
	/// <summary>
	/// Interface where errors during XAML loading are reported.
	/// </summary>
	public interface IXamlErrorSink
	{
		/// <summary>
		/// Reports a XAML load error.
		/// </summary>
		void ReportError(string message, int line, int column);
	}
}

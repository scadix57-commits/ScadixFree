using System.Runtime.Serialization;

namespace Scadix.AxamlDesign
{
	/// <summary>
	/// Exception class used for designer failures.
	/// </summary>
	[Serializable]
	public class DesignerException : Exception
	{
		/// <summary>
		/// Create a new DesignerException instance.
		/// </summary>
		public DesignerException()
		{
		}
		
		/// <summary>
		/// Create a new DesignerException instance.
		/// </summary>
		public DesignerException(string message) : base(message)
		{
		}
		
		/// <summary>
		/// Create a new DesignerException instance.
		/// </summary>
		public DesignerException(string message, Exception innerException) : base(message, innerException)
		{
		}
		
		/// <summary>
		/// Create a new DesignerException instance.
		/// </summary>
		protected DesignerException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}

using System.Collections.Specialized;

namespace Scadix.AxamlDesign.Interfaces
{
	/// <summary>
	/// A IList wich implements INotifyCollectionChanged
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface IObservableList<T> : IList<T>, INotifyCollectionChanged
    {
    }
}
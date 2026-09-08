namespace Scadix.AxamlDesign.Services
{
	public interface ISelectionFilterService
	{
		ICollection<DesignItem> FilterSelectedElements(ICollection<DesignItem> items);
	}
}

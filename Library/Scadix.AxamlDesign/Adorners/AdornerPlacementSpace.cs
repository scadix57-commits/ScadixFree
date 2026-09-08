namespace Scadix.AxamlDesign.Adorners
{
	/// <summary>
	/// Describes the space in which an adorner is placed.
	/// </summary>
	public enum AdornerPlacementSpace
	{
		/// <summary>
		/// The adorner is affected by the render transform of the adorned element.
		/// </summary>
		Render,
		/// <summary>
		/// The adorner is affected by the layout transform of the adorned element.
		/// </summary>
		Layout,
		/// <summary>
		/// The adorner is not affected by transforms of designed controls.
		/// </summary>
		Designer
	}
}

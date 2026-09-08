 

namespace Scadix.AxamlDesigner
{
	/// <summary>
	/// Description of Translations.
	/// </summary>
	public class Translations
	{
		private static Translations _instance;
		public static Translations Instance {
			get {
				if (_instance == null)
					_instance = new Translations();
				return _instance;
			} protected set {
				_instance = value;
			}
		}
		
		public virtual string SendToFrontText {
			get {
				return "Bring to front";
			}
		}
		
		public virtual string SendForward {
			get {
				return "Forward";
			}
		}
		
		public virtual string SendBackward {
			get {
				return "Backward";
			}
		}
		
		public virtual string SendToBack {
			get {
				return "Send to back";
			}
		}
		
		public virtual string PressAltText {
			get {
				return "Press \"Alt\" to Enter Container";
			}
		}
		
		public virtual string WrapInCanvas {
			get {
				return "Wrap in Canvas";
			}
		}
		
		public virtual string WrapInGrid {
			get {
				return "Wrap in Grid";
			}
		}
		
		public virtual string WrapInBorder {
			get {
				return "Wrap in Border";
			}
		}

		public virtual string WrapInViewbox {
			get {
				return "Wrap in Viewbox";
			}
		}

		public virtual string Unwrap
		{
			get {
				return "Unwrap";
			}
		}

		public virtual string FormatedTextEditor
		{
			get
			{
				return "Formated Text Editor";
			}
		}

		public virtual string ArrangeLeft
		{
			get
			{
				return "Arrange Left";
			}
		}

		public virtual string ArrangeHorizontalMiddle
		{
			get
			{
				return "Horizontal centered";
			}
		}

		public virtual string ArrangeRight
		{
			get
			{
				return "Arrange Right";
			}
		}

		public virtual string ArrangeTop
		{
			get
			{
				return "Arrange Top";
			}
		}

		public virtual string ArrangeVerticalMiddle
		{
			get
			{
				return "Vertical centered";
			}
		}

		public virtual string ArrangeBottom
		{
			get
			{
				return "Arrange Bottom";
			}
		}

		public virtual string EditStyle
		{
			get
			{
				return "Edit Style";
			}
		}
	}
}

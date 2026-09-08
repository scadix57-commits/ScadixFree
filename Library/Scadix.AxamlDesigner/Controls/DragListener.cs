

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Scadix.AxamlDesigner.Controls
{
	public delegate void DragHandler(DragListener drag);

	public class DragListener
	{
		public Transform Transform { get; set; }

		/// <summary>Last pointer event args — use GetPosition(relativeTo) for coordinate-space conversion.</summary>
		public PointerEventArgs? LastPointerEventArgs { get; private set; }

		public DragListener(IInputElement target)
		{
			Target = target;

            Target.AddHandler(InputElement.PointerPressedEvent, Target_PointerPressed, handledEventsToo: true);
            Target.AddHandler(InputElement.PointerMovedEvent, Target_PointerMoved, handledEventsToo: true);
            Target.AddHandler(InputElement.PointerReleasedEvent, Target_PointerReleased, handledEventsToo: true);
            Target.AddHandler(InputElement.KeyDownEvent, PostProcessInput, handledEventsToo: true);
        }

		public void ExternalStart()
		{
            Target_PointerPressed(null, null);
		}

		public void ExternalPointerMove(PointerEventArgs e)
        {
            Target_PointerMoved(null, e);
		}

		public void ExternalStop()
		{
            Target_PointerReleased(null, null);
		}

		static DragListener CurrentListener;

        private void PostProcessInput(object sender, KeyEventArgs e)
		{
            if (CurrentListener != null && e.Key == Key.Escape)
            {
                CurrentListener.IsDown = false;
                CurrentListener.IsCanceled = true;
                CurrentListener.Complete();
            }
        }

        private void Target_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            LastPointerEventArgs = e;
            StartPoint = e.GetPosition(null);
            CurrentPoint = StartPoint;
            DeltaDelta = new Vector();
            LastKeyModifiers = e.KeyModifiers;
            IsDown = true;
            IsCanceled = false;
            if (MouseDown != null)
                MouseDown(this);
        }

        private void Target_PointerMoved(object sender, PointerEventArgs e)
        {
			if (IsDown) {
				LastPointerEventArgs = e;
				DeltaDelta = e.GetPosition(null) - CurrentPoint;
				CurrentPoint += DeltaDelta;
				LastKeyModifiers = e.KeyModifiers;

				if (!IsActive) {
                    const double MinDragDistance = 4.0;
                    if (Math.Abs(Delta.X) >= MinDragDistance || Math.Abs(Delta.Y) >= MinDragDistance) {
                        IsActive = true;
						CurrentListener = this;
						if (Started != null)
							Started(this);
					}
				}

				if (IsActive && Changed != null)
					Changed(this);
			}
		}

        private void Target_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
			IsDown = false;
			if (IsActive)
				Complete();
		}

		void Complete()
		{
			IsActive = false;
			CurrentListener = null;
			if (Completed != null)
				Completed(this);
		}

		public event DragHandler MouseDown;
		public event DragHandler Started;
		public event DragHandler Changed;
		public event DragHandler Completed;

		public IInputElement Target { get; private set; }
		public Point StartPoint { get; private set; }
		public Point CurrentPoint { get; private set; }
		public Vector DeltaDelta { get; private set; }
		public KeyModifiers LastKeyModifiers { get; private set; }
		public bool IsActive { get; private set; }
		public bool IsDown { get; private set; }
		public bool IsCanceled { get; private set; }

		public Vector Delta {
			get {
				if (Transform != null) {
					var matrix = Transform.Value;
					matrix.Invert();
					return matrix.Transform(CurrentPoint - StartPoint);
				}
				return CurrentPoint - StartPoint;
			}
		}
	}
}

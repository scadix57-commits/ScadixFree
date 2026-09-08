using System;
using System.Windows.Input;
using Avalonia.Input;
using Key = Avalonia.Input.Key;
using KeyGesture = Avalonia.Input.KeyGesture;

namespace Scadix.Designer
{
	public class SimpleCommand : ICommand
	{
		public SimpleCommand(string text)
		{
			Text = text;
		}

		public SimpleCommand(string text, KeyModifiers modifiers, Key key)
		{
			Gesture = new KeyGesture(key, modifiers);
			Text = text;
		}

		public SimpleCommand(string text, Key key) 
			: this(text, KeyModifiers.None, key)
		{
		}

        public string Text { get; set; }
        public KeyGesture? Gesture { get; set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            Executed?.Invoke(this, parameter);
        }

        public event EventHandler<object?>? Executed;
	}
}

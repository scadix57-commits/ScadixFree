
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Scadix.AxamlDesigner.Themes;

namespace Scadix.AxamlDesigner.Controls
{
    public partial class EnumBar : UserControl
    {
        public static readonly StyledProperty<object> ValueProperty =
            AvaloniaProperty.Register<EnumBar, object>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<Panel> ContainerProperty =
            AvaloniaProperty.Register<EnumBar, Panel>(nameof(Container));

        public static readonly StyledProperty<ControlTheme> ButtonThemeProperty =
            AvaloniaProperty.Register<EnumBar, ControlTheme>(nameof(ButtonTheme));

        private Type currentEnumType;



        public EnumBar()
        {
            InitializeComponent();
            ValueProperty.Changed.AddClassHandler<EnumBar>((x, e) => x.OnValueChanged(e));
            ContainerProperty.Changed.AddClassHandler<EnumBar>((x, e) => x.OnContainerChanged(e));
            uxPanel = this.FindControl<StackPanel>("uxPanel");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public Panel Container
        {
            get => GetValue(ContainerProperty);
            set => SetValue(ContainerProperty, value);
        }

        public ControlTheme ButtonTheme
        {
            get => GetValue(ButtonThemeProperty);
            set => SetValue(ButtonThemeProperty, value);
        }




        private void OnValueChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                var type = e.NewValue.GetType();

                if (currentEnumType != type)
                {
                    currentEnumType = type;
                    uxPanel.Children.Clear();
                    foreach (var v in Enum.GetValues(type))
                    {
                        var b = new EnumButton();
                        b.Value = v;
                        b.Content = Enum.GetName(type, v);
                        b.Bind(ButtonThemeProperty, new Binding("ButtonTheme") { Source = this });
                        b.Click += B_Click;
                        uxPanel.Children.Add(b);
                    }
                }

                UpdateButtons();
                UpdateContainer();
            }
        }

        private void B_Click(object sender, RoutedEventArgs e)
        {
            Value = (sender as EnumButton).Value;
            e.Handled = true;
        }

        private void B_IsCheckedChanged(object sender, RoutedEventArgs e)
        {

        }

        private void OnContainerChanged(AvaloniaPropertyChangedEventArgs e)
        {
            UpdateContainer();
        }

        private void UpdateButtons()
        {
            foreach (EnumButton c in uxPanel.Children)
                if (c.Value.Equals(Value))
                    c.IsChecked = true;
                else
                    c.IsChecked = false;
        }

        private void UpdateContainer()
        {
            if (Container != null)
                for (var i = 0; i < uxPanel.Children.Count; i++)
                {
                    var c = uxPanel.Children[i] as EnumButton;
                    if (c.IsChecked == true)
                        Container.Children[i].IsVisible = true;
                    else
                        Container.Children[i].IsVisible = false;
                }
        }

        private void button_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            Value = (sender as EnumButton).Value;
            e.Handled = true;
        }
    }
}

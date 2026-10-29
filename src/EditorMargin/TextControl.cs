using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;

namespace ExtensibilityMargin
{
    class TextControl : DockPanel
    {
		readonly Label _lblName;
        readonly Label _lblValue;

        public TextControl(string name, string value = "pending...")
        {
            VerticalAlignment = VerticalAlignment.Center;

            _lblName = new Label();
            _lblName.Padding = new Thickness(3, 0, 0, 0);
            _lblName.FontWeight = FontWeights.Bold;
            _lblName.Content = name + ": ";
            _lblName.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ComboBoxFocusedTextBrushKey);
            Children.Add(_lblName);

            _lblValue = new Label();
            _lblValue.Padding = new Thickness(0, 0, 10, 0);
            _lblValue.Content = value;
            _lblValue.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ComboBoxFocusedTextBrushKey);
            Children.Add(_lblValue);
        }

        public string Value
        {
            get
            {
                return _lblValue.Content.ToString();
            }
            set
            {
                _lblValue.Content = value;
            }
        }

        public void SetTooltip(string tooltip)
        {
            ToolTip = tooltip;
        }
    }
}

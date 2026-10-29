using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace ExtensibilityMargin
{
    internal sealed class TextControl : Button
    {
        private readonly string _name;
        private readonly TextBlock _text;
        private string _tooltipText;
        private string _value;
        private readonly DispatcherTimer _copyTimer;

        public TextControl(string name, string value = "pending...")
        {
            _name = name;
            _copyTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1.5),
            };
            _copyTimer.Tick += (sender, e) => ResetCopyConfirmation();
            VerticalAlignment = VerticalAlignment.Center;
            Padding = new Thickness(4, 0, 8, 0);
            BorderThickness = new Thickness(0);
            Background = Brushes.Transparent;
            MinWidth = 0;
            MinHeight = 0;
            MaxWidth = 180;
            FontWeight = FontWeights.Normal;
            SetResourceReference(StyleProperty, VsResourceKeys.ButtonStyleKey);
            SetResourceReference(ForegroundProperty, EnvironmentColors.CommandBarTextActiveBrushKey);

            _text = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Content = _text;
            Value = value;
            AutomationProperties.SetHelpText(this, "Copy editor diagnostics without document text.");
        }

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                if (!_copyTimer.IsEnabled && !string.Equals(_text.Text, value, StringComparison.Ordinal))
                {
                    _text.Text = value;
                    AutomationProperties.SetName(this, _name + ": " + value);
                }
            }
        }

        public void ShowCopyConfirmation()
        {
            _copyTimer.Stop();
            _text.Text = "Copied";
            AutomationProperties.SetName(this, "Editor diagnostics copied");
            _copyTimer.Start();
        }

        public void ResetCopyConfirmation()
        {
            _copyTimer.Stop();
            _text.Text = _value;
            AutomationProperties.SetName(this, _name + ": " + _value);
        }

        public void SetTooltip(string tooltip)
        {
            if (!string.Equals(_tooltipText, tooltip, StringComparison.Ordinal))
            {
                var content = new TextBlock { MaxWidth = 400, TextWrapping = TextWrapping.Wrap };
                string[] sections = tooltip.Split(new[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.None);
                for (int i = 0; i < sections.Length; i++)
                {
                    if (i != 0)
                    {
                        content.Inlines.Add(new LineBreak());
                        content.Inlines.Add(new LineBreak());
                    }
                    int headingEnd = sections[i].IndexOf(Environment.NewLine, StringComparison.Ordinal);
                    if (headingEnd >= 0)
                    {
                        content.Inlines.Add(new Bold(new Run(sections[i].Substring(0, headingEnd))));
                        content.Inlines.Add(new Run(sections[i].Substring(headingEnd)));
                    }
                    else
                    {
                        content.Inlines.Add(new Run(sections[i]));
                    }
                }
                ToolTip = content;
                _tooltipText = tooltip;
            }
        }
    }
}

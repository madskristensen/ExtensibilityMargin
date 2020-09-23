using System.ComponentModel;

namespace ExtensibilityMargin
{
    internal class GeneralOptions : BaseOptionModel<GeneralOptions>
    {
        [Category("General")]
        [DisplayName("Enabled")]
        [Description("Specifies whether the margin is visible or not.")]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;
    }
}

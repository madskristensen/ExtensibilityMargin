using System;
using System.ComponentModel.Design;
using Microsoft;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Task = System.Threading.Tasks.Task;

namespace ExtensibilityMargin
{
    internal sealed class MarginToggleCommand
    {
        private static MarginVisibilitySettings _settings;
        private static IServiceProvider _serviceProvider;
        public static bool Enabled { get; private set; }

        internal static void InitializeSettings(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_settings == null)
            {
                _serviceProvider = serviceProvider;
                var manager = new ShellSettingsManager(serviceProvider);
                var settings = new MarginVisibilitySettings(manager.GetWritableSettingsStore(SettingsScope.UserSettings));
                Enabled = settings.Load();
                _settings = settings;
            }
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            InitializeSettings(package);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Assumes.Present(commandService);
            var menuCommandID = new CommandID(PackageGuids.guidExtensibilityMarginPackageCmdSet, PackageIds.MarginToggleCommandId);
            var menuItem = new OleMenuCommand(Execute, menuCommandID);
            menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        private static void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ((OleMenuCommand)sender).Checked = Enabled;
        }

        private static void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            bool enabled = !Enabled;
            try
            {
                _settings.Save(enabled);
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(nameof(MarginToggleCommand), ex.ToString());
                VsShellUtilities.ShowMessageBox(_serviceProvider, "Unable to save margin visibility. See the Visual Studio Activity Log for details.",
                    Vsix.Name, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }
            Enabled = enabled;
            var button = (OleMenuCommand)sender;
            button.Checked = enabled;
            Clicked?.Invoke(button, enabled);
        }

        public static event EventHandler<bool> Clicked;
    }
}

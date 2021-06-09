using System;
using System.ComponentModel.Design;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ExtensibilityMargin
{
    internal sealed class MarginToggleCommand
    {
        public static bool Enabled { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Assumes.Present(commandService);

            var menuCommandID = new CommandID(PackageGuids.guidExtensibilityMarginPackageCmdSet, PackageIds.MarginToggleCommandId);
            var menuItem = new OleMenuCommand(Execute, menuCommandID);
            menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        private static void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            var button = (OleMenuCommand)sender;
            button.Checked = Enabled;
        }

        private static void Execute(object sender, EventArgs e)
        {
            Enabled = !Enabled;

            var button = (OleMenuCommand)sender;
            button.Checked = Enabled;

            Clicked?.Invoke(button, Enabled);
        }

        public static event EventHandler<bool> Clicked;
    }
}

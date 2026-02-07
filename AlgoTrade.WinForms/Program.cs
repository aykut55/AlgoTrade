using AlgoTrade.Core;

namespace AlgoTrade.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        AppSettings.EnsureDirectories();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

namespace SnailCommander;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppPaths.Configure(args);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

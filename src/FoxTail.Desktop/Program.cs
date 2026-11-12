using System.Windows;

namespace FoxTail.Desktop;

public static class Program
{
    public static string[] Arguments { get; private set; } = [];

    [STAThread]
    public static void Main(string[] args)
    {
        Arguments = args;
        var application = new Application();
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/FoxTail.Desktop;component/Theme.xaml", UriKind.Relative)
        });
        application.Run(new MainWindow());
    }
}

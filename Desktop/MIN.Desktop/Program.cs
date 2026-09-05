using System;
using Avalonia;
#if RELEASE
using MIN.Desktop.Infrastructure.Services;
#endif
namespace MIN.Desktop
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => LoadAvalonia(args);

        private static void LoadAvalonia(string[] args)
        {
#if DEBUG
            // Debug: позволяем несколько экземпляров (для теста двух участников).
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
#else
            using var singleInstance = new SingleInstanceService();
            if (!singleInstance.TryAcquire())
            {
                return; // Окно уже показано первой инстанцией, молча выходим.
            }

            singleInstance.ShowRequested += App.RequestShowMainWindow;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
#endif
        }

        public static AppBuilder BuildAvaloniaApp() => App.Create();
    }
}

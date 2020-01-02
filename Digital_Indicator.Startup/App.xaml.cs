using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Digital_Indicator.Startup
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            SplashScreen splashScreen = new SplashScreen("FILABOT_LOGO_2017 splash1.png");
            splashScreen.Show(false, true);

            base.OnStartup(e);

            var bootstrapper = new Bootstrapper(e.Args);
            bootstrapper.Run();
            splashScreen.Close(TimeSpan.FromMilliseconds(5));
        }

        public App() : base()
        {
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Unhandled exception occurred: \n" + e.Exception.Message + "\n\n Stacktrace: " + e.Exception.StackTrace + "\n", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


}

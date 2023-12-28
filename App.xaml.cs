using System;
using System.Threading;
using System.Windows;

namespace SPEN_To_PC_WindowsApp
{
    public partial class App : Application
    {

        // confirming only one instance if the program is running
        private const string UniqueMutexName = "SPEN-TO-PC";
        private Mutex? singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            singleInstanceMutex = new Mutex(true, UniqueMutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show("Another instance of SPEN To PC is already running.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Exit the second instance
                Environment.Exit(0);
            }
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            singleInstanceMutex?.Close();
            base.OnExit(e);
        }
    }

}

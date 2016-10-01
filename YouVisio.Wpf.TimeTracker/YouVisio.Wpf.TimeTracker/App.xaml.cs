using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace YouVisio.Wpf.TimeTracker
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public const string Source = "Application";
        public const string Log = "Application";

        public App()
        {
            Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (!EventLog.SourceExists(Source))
                EventLog.CreateEventSource(Source, Log);
            EventLog.WriteEntry(Source, "Time Tracker Error:"+e.Exception.ToString(), EventLogEntryType.Error);
        }
    }
}

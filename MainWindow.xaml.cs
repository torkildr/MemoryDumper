using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;

namespace MemoryDump
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public ObservableCollection<Process> _processes = new ObservableCollection<Process>();

        private Process _selectedProcess;
        private readonly BackgroundWorker _memoryDumpWorker;

        public MainWindow()
        {
            RefreshProcesses(null, null);

            _memoryDumpWorker = new BackgroundWorker();
            _memoryDumpWorker.DoWork += AsyncMemoryDump;
            _memoryDumpWorker.RunWorkerCompleted += MemoryDumpComplete;
            _memoryDumpWorker.ProgressChanged += MemoryDumpProgress;
            _memoryDumpWorker.WorkerReportsProgress = true;

            InitializeComponent();
        }

        public ObservableCollection<Process> ProcessList
        {
            get { return _processes; }
        }

        private void RefreshProcesses(object sender, RoutedEventArgs e)
        {
            _processes.Clear();
            int sessionId = Process.GetCurrentProcess().SessionId;

            foreach (Process process in Process.GetProcesses())
            {
                if (process.SessionId == sessionId)
                {
                    _processes.Add(process);
                }
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This seems to fire in some cases (like clicking "refresh" toggles deselect)
            if (e.AddedItems.Count > 0)
            {
                _selectedProcess = e.AddedItems[0] as Process;
            }
            else
            {
                _selectedProcess = null;
            }
        }

        private static void ShowDialog(string text, bool error = false)
        {
            MessageBoxImage image = (error) ? MessageBoxImage.Error : MessageBoxImage.Information;
            MessageBox.Show(text, (error) ? "Error" : "Info", MessageBoxButton.OK, image);
        }

        #region Memory dump/worker stuff

        /// <summary>
        /// Memory dump initiated, starts async background thread
        /// </summary>
        private void MemoryDumpProcess(object sender, RoutedEventArgs e)
        {
            dumpButton.Visibility = Visibility.Hidden;
            refreshButton.Visibility = Visibility.Hidden;
            progressBar.Visibility = Visibility.Visible;
            progressText.Visibility = Visibility.Visible;

            _memoryDumpWorker.RunWorkerAsync(_selectedProcess);
        }

        /// <summary>
        /// Cleans up GUI after memory dump is complete
        /// </summary>
        private void MemoryDumpComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            dumpButton.Visibility = Visibility.Visible;
            refreshButton.Visibility = Visibility.Visible;
            progressBar.Visibility = Visibility.Hidden;
            progressText.Visibility = Visibility.Hidden;
            progressText.Content = "";
        }

        /// <summary>
        /// Updates information label with info from the background worker
        /// </summary>
        private void MemoryDumpProgress(object sender, ProgressChangedEventArgs e)
        {
            string text = e.UserState as string;

            progressText.Content = text;
        }

        /// <summary>
        /// Does the actual memory dump work.
        /// 
        /// This will perform a user dump, compress the dump data and move it to where the application currently is
        /// being run from.
        /// </summary>
        private static void AsyncMemoryDump(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            Process dumpProcess = e.Argument as Process;

            if (worker == null || dumpProcess == null)
                return;

            string baseName = CreateBaseName(dumpProcess.ProcessName);
            string dumpFile = Path.Combine(Path.GetTempPath(), baseName + ".dmp");
            string zipFile = Path.Combine(Path.GetTempPath(), baseName + ".zip");

            try
            {
                worker.ReportProgress(0, "Performing memory dump");
                if (!ExternalApplications.UserDump(dumpProcess, dumpFile))
                {
                    ShowDialog("Could not perform memory dump.", true);
                    return;
                }

                worker.ReportProgress(0, "Compressing data");
                if (!ExternalApplications.CompressFile(dumpFile, zipFile))
                {
                    ShowDialog("Could not compress data.", true);
                    return;
                }

                worker.ReportProgress(0, "Moving into place");
                string zipName = baseName + ".zip";
                File.Move(zipFile, zipName);

                worker.ReportProgress(0, "Done");
                ShowDialog(string.Format("Process dumped to {0}", zipName));
            }
            finally
            {
                if (File.Exists(dumpFile))
                    File.Delete(dumpFile);

                if (File.Exists(zipFile))
                    File.Delete(zipFile);
            }
        }

        #endregion

        #region Private helpers

        /// <summary>
        /// Create a "unique" base name from the given process name
        /// </summary>
        private static string CreateBaseName(string processName)
        {
            string dateString = string.Format("{0:yyyy.MM.dd_HH.mm.ss}", DateTime.Now);
            string username = WindowsIdentity.GetCurrent().Name.Replace('\\', '-');

            return string.Format("{0}_{1}_{2}", processName, username, dateString);
        }

        #endregion
    }
}

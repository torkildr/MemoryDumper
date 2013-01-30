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
            _selectedProcess = e.AddedItems[0] as Process;
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

            string dumpFile = Path.GetTempFileName();
            string zipFile = dumpFile + ".zip";

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
                string currentDir = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName;
                string dateString = string.Format("{0:yyyy.MM.dd_H.mm.ss}", DateTime.Now);
                string username = WindowsIdentity.GetCurrent().Name.Replace('\\', '-');
                string fileName = Path.Combine(currentDir,
                                               string.Format("{0}_{1}_{2}.zip",
                                                             dumpProcess.ProcessName,
                                                             username,
                                                             dateString));
                File.Move(zipFile, fileName);

                worker.ReportProgress(0, "Done");
                ShowDialog(string.Format("Process dumped to {0}", fileName));
            }
            finally
            {
                File.Delete(dumpFile);
                File.Delete(zipFile);
            }
        }

        #endregion
    }
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MemoryDump
{
    public static class ExternalApplications
    {
        private const string userDumpDir = "userdump8.1";
        private const string userDumpExe = "userdump.exe";

        private const string zipExe = @"zip\7za.exe";

        private const string logFile = "log.txt";
        private const string errorLogFile = "error.txt";

        private static string RunCommand(string command, string arguments)
        {
            Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            FileName = command,
                            Arguments = arguments,
                            CreateNoWindow = true,
                        }
                };

            p.Start();

            string output = p.StandardOutput.ReadToEnd();

            LogToFileFile(logFile, string.Format("{2}RunCommand: {0} {1}{2}", command, arguments, Environment.NewLine));
            LogToFileFile(logFile, output);
            LogToFileFile(errorLogFile, p.StandardError.ReadToEnd());

            p.WaitForExit();

            return output;
        }

        private static void LogToFileFile(string file, string text)
        {
            using (StreamWriter writer = new StreamWriter(file, true))
            {
                writer.Write(text);
            }
        }

        /// <summary>
        /// Create a zip file
        /// </summary>
        /// <param name="input">File to compress</param>
        /// <param name="output">Path to compressed file</param>
        /// <returns>Whether or not the command completed successfully</returns>
        public static bool CompressFile(string input, string output)
        {
            FileInfo inputFile = new FileInfo(input);
            if (!inputFile.Exists)
                return false;

            // add to archive, don't show progress, "normal" compression ratio, zip-file
            // we use full name here to avoid creating a bunch of subdirectories
            string stdout = RunCommand(zipExe, string.Format("a -bd -mx5 -tzip \"{0}\" \"{1}\" ", output, inputFile.FullName));

            return stdout.Contains("Everything is Ok") && File.Exists(output);
        }

        /// <summary>
        /// Creates a userdump of a given process
        /// </summary>
        /// <param name="process">Process to dump</param>
        /// <param name="dumpFile">Where to write dump file</param>
        /// <returns>Whether or not the command completed successfully</returns>
        public static bool UserDump(Process process, string dumpFile)
        {
            string arch = (process.Is64BitProcess()) ? "x64" : "x86";

            string userdump = Path.Combine(userDumpDir, arch, userDumpExe);

            if (!File.Exists(userdump))
                return false;

            string stdout = RunCommand(userdump, string.Format("{0} \"{1}\"", process.Id, dumpFile));

            return stdout.Contains("The process was dumped successfully.");
        }

        #region 64 detection
        
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs((UnmanagedType.Bool))]
        public static extern bool IsWow64Process([In] IntPtr processHandle,
                                                 [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

        private static bool Is64BitProcess(this Process process)
        {
            if (!Environment.Is64BitOperatingSystem)
                return false;

            bool isWow64Process;
            if (!IsWow64Process(process.Handle, out isWow64Process))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return !isWow64Process;
        }

        #endregion
    }
}

using Autodesk.AutoCAD.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConsoleApp
{
    internal class Program
    {
        public const string PROG_ID = "AutoCAD.Application.22";
        public static readonly string EXTENSION_PATH =
            $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/ExploderCommands.dll"
            .Replace(@"\", @"/");

        public static readonly string LOG_DIRECTORY =
            $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/Logs/"
            .Replace(@"\", @"/");

        public static readonly string LOGDLL_PATH =
            $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}/LogDll.txt"
            .Replace(@"\", @"/");

        private static string callBackUrl;

        private const int SUSPEND_PERIOD = 500; // ms

        private AcadApplication LaunchNewInstance()
        {
            var acType = Type.GetTypeFromProgID(PROG_ID);
            return (AcadApplication)Activator.CreateInstance(acType, true);
        }

        private AcadApplication ConnectToInstance()
        {
            try
            {
                return (AcadApplication)Marshal.GetActiveObject(PROG_ID);
            }
            catch
            {
                return LaunchNewInstance();
            }
        }

        private void Run(IEnumerable<string> args)
        {
            var files = Enumerable.Zip(
                args.Where((str, index) => index % 2 == 0).Select(path => path.Replace(@"\", @"/")),
                args.Where((str, index) => index % 2 == 1),
                (even, odd) => (filepath: even, fid: odd));

            try
            {
                var acApp = ConnectToInstance();

                Directory.CreateDirectory(LOG_DIRECTORY);
                var logPath = $"{LOG_DIRECTORY}/Reports-{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.log";
                using (var logFile = File.CreateText(logPath))
                {

                    acApp.Visible = true;
                    acApp.Documents.Close();
                    Thread.Sleep(SUSPEND_PERIOD);

                    var emptyDoc = acApp.Documents.Add();
                    emptyDoc.Activate();
                    Thread.Sleep(SUSPEND_PERIOD);
                    emptyDoc.Close(false);
                    Thread.Sleep(SUSPEND_PERIOD);

                    ProcessDocuments(acApp, files, logFile);
                    logFile.WriteLine($"Finish, {DateTime.Now}");
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
                foreach (var (_, fid) in files)
                {
                    CallBack(fid, false);
                }
            }
        }

        private void ProcessDocuments(AcadApplication acApp, IEnumerable<(string filepath, string fid)> files, StreamWriter logFile)
        {
            foreach (var (filepath, fid) in files)
            {
                try
                {
                    acApp.ActiveDocument = acApp.Documents.Open(filepath.Replace(@"\", @"/"));
                    var activeDoc = acApp.ActiveDocument;

                    activeDoc.SendCommand($"(command \"NETLOAD\" \"{EXTENSION_PATH}\") ");
                    activeDoc.SendCommand("ExplodeTypes ");
                    activeDoc.Close(true);
                    Thread.Sleep(SUSPEND_PERIOD);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.Message);
                    CallBack(fid, false);
                    logFile.WriteLine($"Error, {DateTime.Now}, {filepath}, fail to load: {exception.Message}");
                    continue;
                }

                if (File.Exists(LOGDLL_PATH))
                {
                    var errorMsg = File.ReadAllText(LOGDLL_PATH);
                    if (errorMsg.Length > 0)
                    {
                        CallBack(fid, false);
                        logFile.WriteLine($"Error, {DateTime.Now}, {filepath}, {errorMsg}");

                        File.Create(LOGDLL_PATH).Close();
                        continue;
                    }
                }

                CallBack(fid, true);
                logFile.WriteLine($"Success, {DateTime.Now}, {filepath}");
            }
        }

        private async void CallBack(string fid, bool isSucceed)
        {
            var stateCode = isSucceed ? 4 : 5;

            var client = new HttpClient();
            try
            {
                var request = client.GetAsync($"{callBackUrl}?fid={fid}&state={stateCode}");
                await request;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                // keep executing
            }
        }

        static void Main(string[] args)
        {
            var prog = new Program();
            callBackUrl = args[0];

            if (args.Length % 2 != 1)
            {
                Console.WriteLine("Wrong arguments count");
                return;
            }

            prog.Run(args.Skip(1));
        }
    }
}

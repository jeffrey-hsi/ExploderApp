using Autodesk.AutoCAD.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ConsoleApp
{
    internal class Program
    {
        public const string PROG_ID = "AutoCAD.Application.22";
        public readonly string EXTENSION_PATH =
            (Directory.GetCurrentDirectory() + "/ExploderCommands.dll")
            .Replace(@"\", @"/");

        public readonly string LOG_PATH =
            (Directory.GetCurrentDirectory() + "/Log.txt")
            .Replace(@"\", @"/");

        public readonly string LOGDLL_PATH =
            (Directory.GetCurrentDirectory() + "/LogDll.txt")
            .Replace(@"\", @"/");

        private AcadApplication LaunchNewInstance()
        {
            AcadApplication acApp = null;
            try
            {
                var acType = Type.GetTypeFromProgID(PROG_ID);
                acApp = (AcadApplication)Activator.CreateInstance(acType, true);
            }
            catch
            {
                Console.WriteLine($"Cannot launch \"{PROG_ID}\"");
            }
            return acApp;
        }

        private AcadApplication ConnectToInstance()
        {
            AcadApplication acApp;
            try
            {
                acApp = (AcadApplication)Marshal.GetActiveObject(PROG_ID);
            }
            catch
            {
                acApp = LaunchNewInstance();
            }
            return acApp;
        }

        private void Run(string[] files)
        {
            var acApp = ConnectToInstance();
            if (acApp == null)
            {
                return;
            }

            using (var logFile = File.Exists(LOG_PATH) ? File.AppendText(LOG_PATH) : File.CreateText(LOG_PATH))
            {
                try
                {
                    acApp.Visible = true;
                    acApp.Documents.Close();

                    var emptyDoc = acApp.Documents.Add();
                    emptyDoc.Activate();
                    emptyDoc.Close(false);

                    ProcessDocuments(acApp, files, logFile);
                    logFile.WriteLine($"Finish, {DateTime.Now}\n");
                }
                catch (Exception ex)
                {
                    Console.Write(ex);
                }
            }
        }

        private void ProcessDocuments(AcadApplication acApp, string[] files, StreamWriter logFile)
        {
            foreach (var file in files)
            {
                try
                {
                    acApp.ActiveDocument = acApp.Documents.Open(file.Replace(@"\", @"/"));
                    var activeDoc = acApp.ActiveDocument;

                    activeDoc.SendCommand($"(command \"NETLOAD\" \"{EXTENSION_PATH}\") ");
                    activeDoc.SendCommand("ExplodeTypes ");
                    activeDoc.Close(true);
                }
                catch
                {
                    logFile.WriteLine($"Error, {DateTime.Now}, {file}, failed to load");
                    continue;
                }

                if (File.Exists(LOGDLL_PATH))
                {
                    var errorMsg = File.ReadAllText(LOGDLL_PATH);
                    if (errorMsg.Length > 0)
                    {
                        logFile.WriteLine($"Error, {DateTime.Now}, {file}, {errorMsg}");
                        File.Create(LOGDLL_PATH).Close();
                        continue;
                    }
                }

                logFile.WriteLine($"Success, {DateTime.Now}, {file}");
            }
        }

        static void Main(string[] args)
        {
            var prog = new Program();

            prog.Run(args);
        }
    }
}

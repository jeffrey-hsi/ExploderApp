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

            try
            {
                acApp.Visible = true;

                var emptyDoc = acApp.Documents.Add();
                emptyDoc.Activate();
                emptyDoc.Close(false);

                foreach (var file in files)
                {
                    acApp.ActiveDocument = acApp.Documents.Open(file.Replace(@"\", @"/"));
                    var activeDoc = acApp.ActiveDocument;

                    activeDoc.SendCommand($"(command \"NETLOAD\" \"{EXTENSION_PATH}\") ");
                    activeDoc.SendCommand("ExplodeTypes ");
                    activeDoc.Close(true);
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex);
            }
        }

        static void Main(string[] args)
        {
            var prog = new Program();
            prog.Run(args);
        }
    }
}

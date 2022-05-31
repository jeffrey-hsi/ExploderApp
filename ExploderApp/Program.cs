using Autodesk.AutoCAD.Interop;
using System;
using System.Runtime.InteropServices;

namespace ConsoleApp
{
    internal class Program
    {
        public const string PROG_ID = "AutoCAD.Application.22";
        public const string EXTENSION_PATH = @"D:/AutoDesk/HelloWorld/HelloWorld.dll"; //TODO dynamic path

        private AcadApplication launchNewInstance()
        {
            AcadApplication acApp = null;
            try
            {
                Type acType = Type.GetTypeFromProgID(PROG_ID);
                acApp = (AcadApplication)Activator.CreateInstance(acType, true);
            }
            catch
            {
                System.Console.WriteLine("Cannot create object of type \"" + PROG_ID + "\"");
            }
            return acApp;
        }

        private AcadApplication connectToInstance()
        {
            AcadApplication acApp = null;
            try
            {
                acApp = (AcadApplication)Marshal.GetActiveObject(PROG_ID);
            }
            catch
            {
                acApp = launchNewInstance();
            }
            return acApp;
        }

        private void Run(string filePath)
        {
            var acApp = connectToInstance();
            if (acApp != null)
            {
                try
                {
                    acApp.Visible = true;
                    acApp.ActiveDocument.SendCommand("(command \"NETLOAD\" \"" + EXTENSION_PATH + "\") ");
                    acApp.ActiveDocument.SendCommand("HelloForm ");
                }
                catch (Exception ex)
                {
                    System.Console.Write(ex);
                }
            }
        }

        static void Main(string[] args)
        {
            var prog = new Program();
            if (args.Length > 1)
            {
                prog.Run(args[1]);
            }
            else
            {
                prog.Run("");
            }
        }
    }
}

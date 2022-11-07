using Autodesk.AutoCAD.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

class ExplodeApp
{
    public const string PROG_ID = "AutoCAD.Application.24";

    public static readonly string EXTENSION_PATH =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        .Replace(@"\", @"/") + "/ExplodeCommand.dll";

    public static readonly string LOG_DIRECTORY =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        .Replace(@"\", @"/") + "/Logs";

    public static readonly string LOGDLL_PATH =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        .Replace(@"\", @"/") + "/.tmp/plugin.log";

    private static string callBackUrl;

    private static int suspensionPeriod; // ms

    private static readonly HttpClient httpClient = new HttpClient();

    private static AcadApplication LaunchNewInstance()
    {
        try
        {
            var acType = Type.GetTypeFromProgID(PROG_ID);
            return (AcadApplication)Activator.CreateInstance(acType, true);
        }
        catch
        {
            Console.WriteLine($@"Cannot launch ""{PROG_ID}""");
            throw;
        }
    }

    private static AcadApplication ConnectToInstance()
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

    private static void Run(IEnumerable<string> args)
    {
        var files = Enumerable.Zip(
            args.Where((str, index) => index % 2 == 0).Select(path => path.Replace(@"\", @"/")),
            args.Where((str, index) => index % 2 == 1),
            (even, odd) => (filepath: even, fid: odd));

        AcadApplication acApp;
        try
        {
            acApp = ConnectToInstance();

            acApp.Visible = true;
            acApp.Documents.Close();
            Thread.Sleep(suspensionPeriod);
        }
        catch (Exception ex)
        {
            var callback = Task.WhenAll(files.Select(
                file => CallBack(file.fid, false)));
            Console.WriteLine(ex.Message);
            callback.Wait();
            return;
        }

        Task callbacks;
        Directory.CreateDirectory(LOG_DIRECTORY);
        var logPath = $"{LOG_DIRECTORY}/Reports-{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.log";
        using (var logFile = File.CreateText(logPath))
        {
            callbacks = Task.WhenAll(files.Select(
                file => DoForFile(acApp, file, logFile)));
            logFile.WriteLine($"Finish, {DateTime.Now}");
        }
        callbacks.Wait();
    }

    private static async Task DoForFile(AcadApplication acApp, (string filepath, string fid) file, StreamWriter logFile)
    {
        var (filepath, fid) = file;
        try
        {
            acApp.ActiveDocument = acApp.Documents.Open(filepath);
            var activeDoc = acApp.ActiveDocument;

            activeDoc.SendCommand($@"(command ""NETLOAD"" ""{EXTENSION_PATH}"") ");
            activeDoc.SendCommand("ExplodeTypes ");
            activeDoc.Close(true);
            Thread.Sleep(suspensionPeriod);
        }
        catch (Exception exception)
        {
            var failedCallBack = CallBack(fid, false);
            Console.WriteLine(exception.Message);
            logFile.WriteLine($"Error, {DateTime.Now}, {filepath}, fail to load: {exception.Message}");
            await failedCallBack;
            return;
        }

        if (File.Exists(LOGDLL_PATH))
        {
            var errorMsg = File.ReadAllText(LOGDLL_PATH);
            if (errorMsg.Length > 0)
            {
                var failedCallBack = CallBack(fid, false);
                logFile.WriteLine($"Error, {DateTime.Now}, {filepath}, {errorMsg}");

                File.Create(LOGDLL_PATH).Close();
                await failedCallBack;
                return;
            }
        }

        var sucessCallBack = CallBack(fid, true);
        logFile.WriteLine($"Success, {DateTime.Now}, {filepath}");
        await sucessCallBack;
    }

    private static async Task CallBack(string fid, bool isSucceed)
    {
        var stateCode = isSucceed ? 4 : 5;

        try
        {
            await httpClient.GetAsync($"{callBackUrl}?fid={fid}&state={stateCode}");
        }
        catch (HttpRequestException exception)
        {
            // .NET 5 : exception.StatusCode
            Console.WriteLine(exception.Message);
            // keep executing
        }
    }

    static void Main(string[] args)
    {
        callBackUrl = args[0];
        suspensionPeriod = int.Parse(args[1]);

        if (args.Length % 2 != 0)
        {
            Console.WriteLine("Wrong arguments count");
            return;
        }

        Run(args.Skip(2));
    }
}

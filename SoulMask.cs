using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Linq;
using System.Collections.Generic;

namespace WindowsGSM.Plugins
{
    public class SoulMask : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.SoulMask", // WindowsGSM.XXXX
            author = "Illidan",
            description = "WindowsGSM plugin for supporting SoulMask Dedicated Server",
            version = "1.2",
            url = "https://github.com/JTNeXuS2/WindowsGSM.SoulMask", // Github repository link (Best practice)
            color = "#8802db" // Color Hex
        };

        // - Standard Constructor and properties
        public SoulMask(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "3017310"; /* taken via https://steamdb.info/app/3017310/info/ */

        // - Game server Fixed variables
        public override string StartPath => @"WS\Binaries\Win64\WSServer-Win64-Shipping.exe"; // Game server start path
        public string FullName = "SoulMask Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 3; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        public static string ConfigServerName = RandomNumberGenerator.Generate12DigitRandomNumber();

        // - Game server default values
        public string ServerName = "SoulMask Dedicated Server";
        public string Defaultmap = "Level01_Main"; // Original (MapName)
        public string Maxplayers = "50"; // WGSM reads this as string but originally it is number or int (MaxPlayers)
        public string Port = "20700"; // WGSM reads this as string but originally it is number or int
        public string QueryPort = "20701"; // WGSM reads this as string but originally it is number or int (SteamQueryPort)
        public string EchoPort;
        public string Additional => GetAdditional();

        private string GetAdditional()
        {
            string EchoPort = (int.Parse(_serverData.ServerQueryPort) + 1).ToString();
            return $" -log -UTF8Output -MultiHome=0.0.0.0 -EchoPort=\"{EchoPort}\" -forcepassthrough -serverid=1 -initbackup -saving=600 -backupinterval=900 -adminpsw=\"adminpass\" -pvp ";
        }

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {

        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);

            // Prepare start parameter
            var param = new StringBuilder();
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMap) ? string.Empty : $"{_serverData.ServerMap} -server %*");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -port={_serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? string.Empty : $" -QueryPort={_serverData.ServerQueryPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerName) ? string.Empty : $" -SteamServerName=\"{_serverData.ServerName}\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" -MaxPlayers={_serverData.ServerMaxPlayer}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
				// Модификация для вызова batch перед стартом сервера
                //await RunBatchScript();
				var scriptPath = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID), "OnStart.bat");
				await RunExternalScriptAsync(scriptPath);
				// END
                p.Start();
                if (AllowsEmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }

                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }
		// END
///////////////////////////////////////////////////////
    public async Task RunExternalScriptAsync(string scriptPath)
    {
        using (Process batch = new Process())
        {
            batch.StartInfo.FileName = "cmd.exe";
            batch.StartInfo.Arguments = $"/c \"{scriptPath}\""; // /c flag runs the command and terminates
			batch.StartInfo.WorkingDirectory = Path.Combine(ServerPath.GetServersServerFiles(_serverData.ServerID));
            //process.StartInfo.UseShellExecute = false;
            if (AllowsEmbedConsole)
            {
                batch.StartInfo.CreateNoWindow = true;
                batch.StartInfo.UseShellExecute = false;
                batch.StartInfo.RedirectStandardInput = true;
                batch.StartInfo.RedirectStandardOutput = true;
                batch.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                batch.OutputDataReceived += serverConsole.AddOutput;
                batch.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start the process asynchronously
            batch.Start();
            if (AllowsEmbedConsole)
            {
                batch.BeginOutputReadLine();
                batch.BeginErrorReadLine();
            }
            // Wait asynchronously for the script to complete
            await WaitForExitAsync(batch);
            // Call the closeProcess function with the process as an argument
            //closeProcess?.Invoke(process);
            // Script completed
            Console.WriteLine("Script exited");
        }
    }
    static Task WaitForExitAsync(Process batch)
    {
        var tcs = new TaskCompletionSource<object>();

        batch.EnableRaisingEvents = true;
        batch.Exited += (sender, e) => tcs.TrySetResult(null);

        if (batch.HasExited)
        {
            tcs.TrySetResult(null);
        }

        return tcs.Task;
    }
///////////////////////////////////////////////////////
        // - Stop server function
        public async Task Stop(Process p)
        {
			await Task.Run(() =>
			{
				 Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
				 Functions.ServerConsole.SendWaitToMainWindow("^c");
			});
			await Task.Delay(20000);
        }

        // - Update server function
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            await Task.Run(() => { p.WaitForExit(); });
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, "PackageInfo.bin");
            Error = $"Invalid Path! Fail to find {Path.GetFileName(exePath)}";
            return File.Exists(exePath);
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }

    public class RandomNumberGenerator
    {
        public static string Generate12DigitRandomNumber()
        {
            Random random = new Random();
            string twelveDigitNumber = GenerateRandom12Digits(random);
            return twelveDigitNumber;
        }

        private static string GenerateRandom12Digits(Random random)
        {
            string result = "";
            for (int i = 0; i < 12; i++)
            {
                result += random.Next(0, 10).ToString(); // Generates a random digit between 0 and 9
            }
            return result;
        }
    }
}

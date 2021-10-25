using NLog;
using System;
using System.Diagnostics;

namespace GenshinAudioExportLib
{
    class WemToWav
    {
        private readonly string vgmstreamPath;

        public WemToWav(string vgmstreamPath)
        {
            this.vgmstreamPath = vgmstreamPath;
        }

        public void StartWemToWav(string inputFile, string outputFilePath)
        {
            Process wemToWavProcess;
            var startInfo = new ProcessStartInfo(vgmstreamPath)
            {
                Arguments = $"-o \"{outputFilePath}\" \"{inputFile}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            using (wemToWavProcess = new Process())
            {
                try
                {
                    wemToWavProcess.StartInfo = startInfo;
                    wemToWavProcess.Start();
                    wemToWavProcess.WaitForExit();
                }
                catch (Exception ex)
                {
                    LogManager.GetCurrentClassLogger().Error($"Could not start \"quickbms.exe\" or \"vgmstream-cli.exe\":\n\n{ex.Message}\n\nIn case of a permissions issue try running this program as Administrator.");
                }
            }
        }
    }
}

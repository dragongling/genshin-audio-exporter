using NLog;
using System;
using System.Diagnostics;

namespace GenshinAudioExportLib
{
    internal class WemToWav
    {
        private readonly string _vgmstreamPath;

        public WemToWav(string vgmstreamPath)
        {
            _vgmstreamPath = vgmstreamPath;
        }

        public void StartWemToWav(string inputFile, string outputFilePath)
        {
            Process wemToWavProcess;
            var startInfo = new ProcessStartInfo(_vgmstreamPath)
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

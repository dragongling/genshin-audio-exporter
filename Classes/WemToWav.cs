using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace genshin_audio_exporter
{
    public class WemToWav
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
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
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
                    LogManager.GetCurrentClassLogger().Error($"Could not start quickbms.exe process:\n\n{ex.Message}\n\nIn case of a permissions issue try running this program as Administrator.");
                    MessageBox.Show($"Could not start vgmstream-cli.exe process:\n\n{ex.Message}\n\nIn case of a permissions issue try running this program as Administrator.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

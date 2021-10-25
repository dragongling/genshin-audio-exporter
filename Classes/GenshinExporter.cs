using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace genshin_audio_exporter.Classes
{
    class GenshinExporter : IDisposable
    {
        public int exportedAudioFiles = 0;
        readonly Logger logger = LogManager.GetCurrentClassLogger();
        readonly PckToWem pckToWem;
        readonly WemToWav wemToWav;
        readonly WavConverter wavConverter;

        private static readonly string LibsDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "libs");
        public string OutputDir;
        public string ProcessingDir;
        
        public GenshinExporter()
        {
            string quickBmsPath = Path.Combine(LibsDir, "quickbms.exe");
            string waveScanBms = Path.Combine(LibsDir, "wavescan.bms");
            pckToWem = new PckToWem(quickBmsPath, waveScanBms);
            string vgmstreamPath = Path.Combine(LibsDir, "vgmstream-cli.exe");
            wemToWav = new WemToWav(vgmstreamPath);
            string ffmpegPath = Path.Combine(LibsDir, "ffmpeg.exe");
            wavConverter = new WavConverter(ffmpegPath);
        }

        public async Task<int> ExportPcksToWem(ICollection<string> pckFiles, string wemFolder, IProgress<int> progress, CancellationToken? ct = null)
        {
            Directory.CreateDirectory(wemFolder);
            logger.Info("Exporting PCK  =>  WEM  (Required)");
            logger.Info("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string pckFile in pckFiles)
                {
                    ct?.ThrowIfCancellationRequested();
                    pckToWem.StartPckToWem(pckFile, wemFolder);
                    logger.Debug($"{Path.GetFileName(pckFile)}  =>  {Path.GetFileNameWithoutExtension(pckFile)}.wem");
                    index += 1;
                    progress?.Report(index);
                }
            });
            return index;
        }

        public async Task<int> ExportPcksToWem(ICollection<string> pckFiles, IProgress<int> progress, CancellationToken? ct = null)
        {
            
            string wemFolder = Path.Combine(ProcessingDir, "wem");
            Directory.CreateDirectory(wemFolder);
            return await ExportPcksToWem(pckFiles, wemFolder, progress, ct);
        }

        public async Task<int> ExportWemsToWavs(ICollection<string> wemFiles, string wavFolder, int overallIndex, IProgress<int> progress, CancellationToken? ct = null)
        {
            Directory.CreateDirectory(wavFolder);
            logger.Info("");
            logger.Info("Exporting WEM  =>  WAV  (Required)");
            logger.Info("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string wemFile in wemFiles)
                {
                    ct?.ThrowIfCancellationRequested();
                    string outputFilePath = Path.Combine(ProcessingDir, "wav", Path.GetFileNameWithoutExtension(wemFile) + ".wav");
                    wemToWav.StartWemToWav(wemFile, outputFilePath);
                    logger.Debug($"{Path.GetFileName(wemFile)}  =>  {Path.GetFileNameWithoutExtension(wemFile)}.wav");
                    index += 1;
                    overallIndex += 1;
                    progress.Report(index);
                }
            });
            return index;
        }

        public async Task<int> ExportWemsToWavs(ICollection<string> wemFiles, int overallIndex, IProgress<int> progress, CancellationToken? ct = null)
        {
            string wavFolder = Path.Combine(ProcessingDir, "wav");
            Directory.CreateDirectory(wavFolder);
            return await ExportWemsToWavs(wemFiles, wavFolder, overallIndex, progress, ct);
        }

        public async Task ExportAudioFormat(ICollection<string> wavFiles, string outputDir, string format, IProgress<int> progress, CancellationToken? ct = null)
        {
            Directory.CreateDirectory(Path.Combine(ProcessingDir, format));
            Directory.CreateDirectory(Path.Combine(outputDir, format));
            int index = 0;

            logger.Info("");
            if (format == "wav")
                logger.Info("Copying WAV Files to destination directory");
            else
                logger.Info($"Exporting WAV  =>  {format.ToUpper()}");
            logger.Info("");

            await Task.Run(() =>
            {
                foreach (string wavFile in wavFiles)
                {
                    ct?.ThrowIfCancellationRequested();
                    string processedFile = Path.Combine(ProcessingDir, format, Path.GetFileNameWithoutExtension(wavFile) + $".{format}");
                    if (format != "wav")                    
                        wavConverter.ConvertWav(wavFile, processedFile, format);
                    string destFile = Path.Combine(outputDir, format, Path.GetFileNameWithoutExtension(wavFile) + $".{format}");

                    File.Copy(processedFile, destFile, true);
                    logger.Debug($"{Path.GetFileName(wavFile)}  =>  {Path.GetFileName(processedFile)}");
                    exportedAudioFiles += 1;
                    index += 1;
                    progress.Report(index);
                }
            });
        }

        public async Task ExportAudioFormat(ICollection<string> wavFiles, string format, IProgress<int> progress, CancellationToken? ct = null)
        {
            await ExportAudioFormat(wavFiles, OutputDir, format, progress, ct);
        }

        public void KillProcesses()
        {
            string[] processesToKill = new string[] { "quickbms", "vgmstream-cli", "ffmpeg" };
            foreach (var processName in processesToKill)
            {
                try
                {
                    while (Process.GetProcessesByName(processName).Length > 0)
                        foreach (var process in Process.GetProcessesByName(processName))
                            process.Kill();
                }
                catch (Win32Exception)
                {
                    logger.Warn($"Couldn't stop process \"{processName}\", please stop it manually.");
                }
            }
        }

        public void ClearTempDirectories()
        {
            DirectoryInfo processingDir = new DirectoryInfo(ProcessingDir);
            foreach (FileInfo file in processingDir.GetFiles()) file.Delete();
            foreach (DirectoryInfo subDirectory in processingDir.GetDirectories()) subDirectory.Delete(true);
            processingDir.Delete();
        }

        public void CleanFromMissingFiles(ref List<string> pckFiles)
        {
            List<string> missingFiles = new List<string>();
            foreach (string filePath in pckFiles)
            {
                if (!File.Exists(filePath))
                {
                    logger.Warn($"\"{filePath}\" is missing");
                    missingFiles.Add(filePath);
                }
            }
            foreach (string missingFile in missingFiles)
            {
                pckFiles.Remove(missingFile);
            }
        }

        public async Task UnpackLibs()
        {
            Directory.CreateDirectory(LibsDir);
            if (!AppResources.IsUnpacked)
            {
                logger.Info("Unpacking libraries");
                logger.Info("");
                await Task.Run(() =>
                {
                    AppResources.UnpackResources();
                });
            }
        }

        public void Dispose()
        {
            try
            {
                KillProcesses();
                ClearTempDirectories();
                Directory.Delete(LibsDir, true);
            }
            catch { }
        }
    }
}

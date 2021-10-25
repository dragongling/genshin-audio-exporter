using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenshinAudioExportLib
{
    /// <summary>
    /// Main class for exporting Genshin Impact audio data
    /// </summary>
    public class GenshinExporter : IDisposable
    {
        /// <summary>
        /// Total amount of exported files
        /// </summary>
        public int AudioFilesExported = 0;

        /// <summary>
        /// Directory where export result output is stored
        /// </summary>
        public string OutputDir;

        /// <summary>
        /// Temp directory for storing intermediate file formats
        /// </summary>
        public string ProcessingDir;

        /// <summary>
        /// <see cref="CancellationToken"/> for 
        /// <see href="https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-cancel-a-task-and-its-children">cancelling export tasks</see>
        /// </summary>
        public CancellationToken? CancelToken;

        /// <summary>
        /// IProgress&lt;int&gt; instance for reporting current task progress.
        /// Define a new callback before every export task
        /// </summary>
        public IProgress<int> progress;

        readonly Logger logger = LogManager.GetCurrentClassLogger();
        readonly PckToWem pckToWem;
        readonly WemToWav wemToWav;
        readonly WavConverter wavConverter;
        private static string LibsDir;


        /// <summary>
        /// Initializes a new instance of the <see cref="GenshinExporter"/> class.
        /// </summary>
        /// <param name="libsDir">Directory that contains quickbms, vgmstream and ffmpeg executables and wavescan.bms script</param>
        public GenshinExporter(string libsDir)
        {
            LibsDir = libsDir;
            string quickBmsPath = Path.Combine(LibsDir, "quickbms");
            string waveScanBms = Path.Combine(LibsDir, "wavescan.bms");
            pckToWem = new PckToWem(quickBmsPath, waveScanBms);
            string vgmstreamPath = Path.Combine(LibsDir, "vgmstream-cli");
            wemToWav = new WemToWav(vgmstreamPath);
            string ffmpegPath = Path.Combine(LibsDir, "ffmpeg");
            wavConverter = new WavConverter(ffmpegPath);
        }

        /// <summary>
        /// Converts PCK containers to <see href="https://en.wikipedia.org/wiki/Audiokinetic_Wwise">WEM</see> files
        /// </summary>
        /// <param name="pckFiles">Collection of input PCK file pathes</param>
        /// <param name="wemFolder">Output directory path</param>
        /// <returns>Amount of files converted</returns>
        public async Task<int> ExportPcksToWem(ICollection<string> pckFiles, string wemFolder)
        {
            Directory.CreateDirectory(wemFolder);
            logger.Info("Exporting PCK  =>  WEM  (Required)");
            logger.Info("");
            int index = 0;
            await Task.Run(() =>
            {
                foreach (string pckFile in pckFiles)
                {
                    CancelToken?.ThrowIfCancellationRequested();
                    pckToWem.StartPckToWem(pckFile, wemFolder);
                    logger.Debug($"{Path.GetFileName(pckFile)}  =>  {Path.GetFileNameWithoutExtension(pckFile)}.wem");
                    index += 1;
                    progress?.Report(index);
                }
            });
            return index;
        }

        /// <summary>
        /// Converts PCK containers to <see href="https://en.wikipedia.org/wiki/Audiokinetic_Wwise">WEM</see> files into <see cref="ProcessingDir"/> directory
        /// </summary>
        /// <param name="pckFiles">Collection of input PCK file pathes</param>
        /// <returns>Amount of PCK files processed</returns>
        public async Task<int> ExportPcksToWem(ICollection<string> pckFiles)
        {
            
            string wemFolder = Path.Combine(ProcessingDir, "wem");
            Directory.CreateDirectory(wemFolder);
            return await ExportPcksToWem(pckFiles, wemFolder);
        }

        /// <summary>
        /// Unpacks <see href="https://en.wikipedia.org/wiki/Audiokinetic_Wwise">WEM</see> containers 
        /// to <see href="https://en.wikipedia.org/wiki/WAV">WAV</see> files
        /// </summary>
        /// <param name="wemFiles">Collection of input WEM file pathes</param>
        /// <param name="wavFolder">Output directory path</param>
        /// <param name="overallIndex">Index of an overall export process</param>
        /// <returns>Amount of PCK files processed</returns>
        public async Task<int> ExportWemsToWavs(ICollection<string> wemFiles, string wavFolder, int overallIndex)
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
                    CancelToken?.ThrowIfCancellationRequested();
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

        /// <summary>
        /// Unpacks <see href="https://en.wikipedia.org/wiki/Audiokinetic_Wwise">WEM</see> containers 
        /// to <see href="https://en.wikipedia.org/wiki/WAV">WAV</see> files into <see cref="OutputDir"/> directory
        /// </summary>
        /// <param name="wemFiles">Collection of input WEM file paths</param>
        /// <param name="overallIndex">Index of an overall export process</param>
        /// <returns>Amount of </returns>
        public async Task<int> ExportWemsToWavs(ICollection<string> wemFiles, int overallIndex)
        {
            string wavFolder = Path.Combine(ProcessingDir, "wav");
            Directory.CreateDirectory(wavFolder);
            return await ExportWemsToWavs(wemFiles, wavFolder, overallIndex);
        }

        /// <summary>
        /// Unpacks <see href="https://en.wikipedia.org/wiki/Audiokinetic_Wwise">WEM</see> containers 
        /// to <see href="https://en.wikipedia.org/wiki/WAV">WAV</see> files into <see cref="OutputDir"/> directory
        /// </summary>
        /// <param name="overallIndex">Index of an overall export process</param>
        /// <returns>Amount of </returns>
        public async Task<int> ExportWemsToWavs(int overallIndex)
        {
            List<string> wemFiles = Directory.GetFiles(Path.Combine(ProcessingDir, "wem"), "*.wem").ToList();
            return await ExportWemsToWavs(wemFiles, overallIndex);
        }

        /// <summary>
        /// Converts WAV files into audio format defined in <paramref name="format"/> param
        /// </summary>
        /// <param name="wavFiles">Collection of input WAV file paths</param>
        /// <param name="outputDir">Output directory path</param>
        /// <param name="format">Audio format that is supported by <see cref="WavConverter"/></param>
        /// <returns></returns>
        public async Task ExportAudioFormat(ICollection<string> wavFiles, string outputDir, string format)
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
                    CancelToken?.ThrowIfCancellationRequested();
                    string processedFile = Path.Combine(ProcessingDir, format, Path.GetFileNameWithoutExtension(wavFile) + $".{format}");
                    if (format != "wav")                    
                        wavConverter.ConvertWav(wavFile, processedFile, format);
                    string destFile = Path.Combine(outputDir, format, Path.GetFileNameWithoutExtension(wavFile) + $".{format}");

                    File.Copy(processedFile, destFile, true);
                    logger.Debug($"{Path.GetFileName(wavFile)}  =>  {Path.GetFileName(processedFile)}");
                    AudioFilesExported += 1;
                    index += 1;
                    progress.Report(index);
                }
            });
        }

        /// <summary>
        /// Converts WAV files into audio format defined in <paramref name="format"/> param into <see cref="OutputDir"/> directory
        /// </summary>
        /// <param name="wavFiles">Collection of input WAV file paths</param>
        /// <param name="format">Audio format that is supported by <see cref="WavConverter"/></param>
        /// <returns></returns>
        public async Task ExportAudioFormat(ICollection<string> wavFiles, string format)
        {
            await ExportAudioFormat(wavFiles, OutputDir, format);
        }


        /// <summary>
        /// Converts WAV files into audio format defined in <paramref name="format"/> param 
        /// from <see cref="ProcessingDir"/> directory into <see cref="OutputDir"/> directory
        /// </summary>
        /// <param name="format">Audio format that is supported by <see cref="WavConverter"/></param>
        public async Task ExportAudioFormat(string format)
        {
            List<string> wavFiles = Directory.GetFiles(Path.Combine(ProcessingDir, "wav"), "*.wav").ToList();
            await ExportAudioFormat(wavFiles, OutputDir, format);
        }

        /// <summary>
        /// Kills all instances of converter processes
        /// </summary>
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
                catch (Win32Exception ex)
                {
                    logger.Warn($"An error occured while stopping \"{processName}\":");
                    logger.Warn(ex.Message);
                    logger.Warn($"Please stop \"{processName}\" manually.");
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

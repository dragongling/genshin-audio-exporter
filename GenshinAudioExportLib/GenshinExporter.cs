using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenshinAudioExportLib
{
    /// <summary>
    /// Main class for exporting Genshin Impact audio data
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class GenshinExporter : IDisposable
    {
        /// <summary>
        /// Total amount of exported files
        /// </summary>
        public int AudioFilesExported { get; set; }

        /// <summary>
        /// Directory where export result output is stored
        /// </summary>
        public string OutputDir { get; set; }

        /// <summary>
        /// Temp directory for storing intermediate file formats
        /// </summary>
        public string ProcessingDir { get; set; }

        /// <summary>
        /// <see cref="CancellationToken"/> for 
        /// <see href="https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-cancel-a-task-and-its-children">cancelling export tasks</see>
        /// </summary>
        public CancellationToken? CancelToken { get; set; }

        /// <summary>
        /// IProgress&lt;int&gt; instance for reporting current task progress.
        /// Define a new callback before every export task
        /// </summary>
        public IProgress<int> Progress { get; set; }

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly PckToWem _pckToWem;
        private readonly WemToWav _wemToWav;
        private readonly WavConverter _wavConverter;
        private readonly string _libsDir;


        /// <summary>
        /// Initializes a new instance of the <see cref="GenshinExporter"/> class.
        /// </summary>
        /// <param name="libsDir">Directory that contains quickbms, vgmstream and ffmpeg executables and wavescan.bms script</param>
        public GenshinExporter(string libsDir)
        {
            _libsDir = libsDir;
            var quickBmsPath = Path.Combine(_libsDir, "quickbms");
            var waveScanBms = Path.Combine(_libsDir, "wavescan.bms");
            _pckToWem = new PckToWem(quickBmsPath, waveScanBms);
            var vgmstreamPath = Path.Combine(_libsDir, "vgmstream-cli");
            _wemToWav = new WemToWav(vgmstreamPath);
            var ffmpegPath = Path.Combine(_libsDir, "ffmpeg");
            _wavConverter = new WavConverter(ffmpegPath);
        }

        /// <summary>
        /// Converts PCK containers to <see href="https://en.wikipedia.org/wiki/Audiokinetic_Wwise">WEM</see> files
        /// </summary>
        /// <param name="pckFiles">Collection of input PCK file paths</param>
        /// <param name="wemFolder">Output directory path</param>
        /// <returns>Amount of files converted</returns>
        public async Task<int> ExportPcksToWem(ICollection<string> pckFiles, string wemFolder)
        {
            Directory.CreateDirectory(wemFolder);
            _logger.Info("Exporting PCK  =>  WEM  (Required)");
            _logger.Info("");
            var index = 0;
            await Task.Run(() =>
            {
                foreach (var pckFile in pckFiles)
                {
                    CancelToken?.ThrowIfCancellationRequested();
                    _pckToWem.StartPckToWem(pckFile, wemFolder);
                    _logger.Debug($"{Path.GetFileName(pckFile)}  =>  {Path.GetFileNameWithoutExtension(pckFile)}.wem");
                    index += 1;
                    Progress?.Report(index);
                }
            });
            return index;
        }

        /// <summary>
        /// Converts PCK containers to <see href="https://en.wikipedia.org/wiki/Audiokinetic_Wwise">WEM</see> files into <see cref="ProcessingDir"/> directory
        /// </summary>
        /// <param name="pckFiles">Collection of input PCK file paths</param>
        /// <returns>Amount of PCK files processed</returns>
        public async Task<int> ExportPcksToWem(ICollection<string> pckFiles)
        {
            
            var wemFolder = Path.Combine(ProcessingDir, "wem");
            Directory.CreateDirectory(wemFolder);
            return await ExportPcksToWem(pckFiles, wemFolder);
        }

        /// <summary>
        /// Unpacks <see href="https://en.wikipedia.org/wiki/Audiokinetic_Wwise">WEM</see> containers 
        /// to <see href="https://en.wikipedia.org/wiki/WAV">WAV</see> files
        /// </summary>
        /// <param name="wemFiles">Collection of input WEM file paths</param>
        /// <param name="wavFolder">Output directory path</param>
        /// <param name="overallIndex">Index of an overall export process</param>
        /// <returns>Amount of PCK files processed</returns>
        public async Task<int> ExportWemsToWavs(ICollection<string> wemFiles, string wavFolder, int overallIndex)
        {
            Directory.CreateDirectory(wavFolder);
            _logger.Info("");
            _logger.Info("Exporting WEM  =>  WAV  (Required)");
            _logger.Info("");
            var index = 0;
            await Task.Run(() =>
            {
                foreach (var wemFile in wemFiles)
                {
                    CancelToken?.ThrowIfCancellationRequested();
                    var outputFilePath = Path.Combine(ProcessingDir, "wav", Path.GetFileNameWithoutExtension(wemFile) + ".wav");
                    _wemToWav.StartWemToWav(wemFile, outputFilePath);
                    _logger.Debug($"{Path.GetFileName(wemFile)}  =>  {Path.GetFileNameWithoutExtension(wemFile)}.wav");
                    index += 1;
                    overallIndex += 1;
                    Progress.Report(index);
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
            var wemFiles = Directory.GetFiles(Path.Combine(ProcessingDir, "wem"), "*.wem").ToList();
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
            var index = 0;

            _logger.Info("");
            _logger.Info(format == "wav"
                ? "Copying WAV Files to destination directory"
                : $"Exporting WAV  =>  {format.ToUpper()}");
            _logger.Info("");

            await Task.Run(() =>
            {
                foreach (var wavFile in wavFiles)
                {
                    CancelToken?.ThrowIfCancellationRequested();
                    var processedFile = Path.Combine(ProcessingDir, format, Path.GetFileNameWithoutExtension(wavFile) + $".{format}");
                    if (format != "wav")                    
                        _wavConverter.ConvertWav(wavFile, processedFile, format);
                    var destFile = Path.Combine(outputDir, format, Path.GetFileNameWithoutExtension(wavFile) + $".{format}");

                    File.Copy(processedFile, destFile, true);
                    _logger.Debug($"{Path.GetFileName(wavFile)}  =>  {Path.GetFileName(processedFile)}");
                    AudioFilesExported += 1;
                    index += 1;
                    Progress.Report(index);
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
                    _logger.Warn($"An error occured while stopping \"{processName}\":");
                    _logger.Warn(ex.Message);
                    _logger.Warn($"Please stop \"{processName}\" manually.");
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
                    _logger.Warn($"\"{filePath}\" is missing");
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) 
                return;
            try
            {
                KillProcesses();
                ClearTempDirectories();
                Directory.Delete(_libsDir, true);
            }
            catch
            {
                // We can't handle other processes, empty here
            }
        }
    }
}

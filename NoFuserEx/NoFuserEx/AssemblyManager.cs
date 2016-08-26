using System;
using System.Globalization;
using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace NoFuserEx {
    internal class AssemblyManager {
        readonly string inputFile;
        readonly string outputFile;

        internal AssemblyManager(string inputFile) {
            this.inputFile = inputFile;
            outputFile =
                $"{Path.GetDirectoryName(inputFile)}\\NoFuserEx_Output\\{Path.GetFileName(inputFile)}";
            Logger.VeryVerbose($"Output assembly: {outputFile}.");

            if (Utils.CreateDirectory(Path.GetDirectoryName(outputFile)))
                Logger.VeryVerbose("Created output directory.");
            else
                Logger.Exception(new Exception("Error creating directory..."));
        }

        internal ModuleDefMD Module { get; set; }
        internal ModuleWriterOptions ModuleWriterOptions { get; private set; }
        internal NativeModuleWriterOptions NativeModuleWriterOptions { get; private set; }

        void ShowAssemblyInfo() {
            Logger.Info($"Module Name: {Module.Name}");
            using (var stream = Module.MetaData.PEImage.CreateFullStream())
                Logger.Info($"Module Size: {GetSize(stream.Length)}");
            Logger.Info($"CLR Version: {Module.RuntimeVersion.Substring(0, 4)}");
        }

        static string GetSize(long size) {
            string[] sizeType = { " B", " KB", " MB", " GB", " TB", " PB", " EB" };
            if (size == 0)
                return "0 B";
            var bytes = Math.Abs(size);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(size) * num).ToString(CultureInfo.InvariantCulture) + sizeType[place];
        }

        internal void LoadAssembly() {
            try {
                Logger.Verbose("Loading module...");
                Module = ModuleDefMD.Load(inputFile);
                ModuleWriterOptions = new ModuleWriterOptions(Module);
                NativeModuleWriterOptions = new NativeModuleWriterOptions(Module);
                ShowAssemblyInfo();
            }
            catch (Exception ex) {
                Logger.Exception(ex);
            }
        }

        internal void SaveAssembly() {
            Logger.Verbose($"Module IsILOnly: {Module.IsILOnly}");
            try {
                if (Module.IsILOnly)
                    Module.Write(outputFile);
                else
                    Module.NativeWrite(outputFile);
                Logger.Info($"Saved successfully in {outputFile}");
            }
            catch (ModuleWriterException) {
                Logger.Verbose("Trying with \"DummyLogger.NoThrowInstance\"...");
                ModuleWriterOptions.Logger = DummyLogger.NoThrowInstance;
                NativeModuleWriterOptions.Logger = DummyLogger.NoThrowInstance;

                if (Module.IsILOnly)
                    Module.Write(outputFile, ModuleWriterOptions);
                else
                    Module.NativeWrite(outputFile, NativeModuleWriterOptions);
                Logger.Exclamation($"Saved with errors in {outputFile}");
            }
            catch (Exception ex) {
                Logger.Error("Error saving assembly.");
                Logger.Exception(ex);
            }
        }
    }
}

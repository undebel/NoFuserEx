using System.Collections.Generic;
using dnlib.DotNet;
using NoFuserEx.Deobfuscator.Deobfuscators;
using NoFuserEx.Deobfuscator.Deobfuscators.Constants;

namespace NoFuserEx.Deobfuscator {
    internal class DeobfuscatorManager {
        readonly AssemblyManager assemblyManager;

        readonly List<IDeobfuscator> deobfuscators;

        internal DeobfuscatorManager(AssemblyManager assemblyManager) {
            this.assemblyManager = assemblyManager;
            deobfuscators = new List<IDeobfuscator>();
        }

        void SelectDeobfuscators() {
            Logger.Verbose("Adding deobfuscators...");

            if (!Options.NoUnpack) {
                deobfuscators.Add(new CompressorDeobfuscator());
                Logger.VeryVerbose("Added compressor deobfuscator.");
            }
            
            if (!Options.NoTamper) {
                deobfuscators.Add(new AntiTamperDeobfuscator());
                Logger.VeryVerbose("Added anti-tamper deobfuscator.");
            }

            if (!Options.NoResources) {
                deobfuscators.Add(new ResourcesDeobfuscator());
                Logger.VeryVerbose("Added resources deobfuscator.");
            }

            if (!Options.NoConstants) {
                deobfuscators.Add(new ConstantsDeobfuscator());
                Logger.VeryVerbose("Added constants deobfuscator.");
            }

            if (!Options.NoProxyCalls) {
                deobfuscators.Add(new ProxyDeobfuscator());
                Logger.VeryVerbose("Added proxy deobfuscator.");
            }

            deobfuscators.Add(new AntiDumperDeobfuscator());
            Logger.VeryVerbose("Added anti-dumper deobfuscator.");

            deobfuscators.Add(new AntiDebuggerDeobfuscator());
            Logger.VeryVerbose("Added anti-debugger deobfuscator.");
        }

        internal void Start() {
            SelectDeobfuscators();
            DetectConfuserVersion();

            foreach (var deobfuscator in deobfuscators) {
                var deobfuscated = deobfuscator.Deobfuscate(assemblyManager);
                if (deobfuscated)
                    deobfuscator.Log();
            }
            Logger.WriteLine(string.Empty);
        }

        void DetectConfuserVersion() {
            Logger.Verbose("Detecting ConfuserEx version...");
            var versions = new List<string>();
            var module = assemblyManager.Module;
            foreach (var attribute in module.CustomAttributes) {
                if (attribute.TypeFullName != "ConfusedByAttribute")
                    continue;
                foreach (var argument in attribute.ConstructorArguments) {
                    if (argument.Type.ElementType != ElementType.String)
                        continue;
                    var value = argument.Value.ToString();
                    if (!value.Contains("ConfuserEx"))
                        continue;
                    Logger.Info($"Detected: {value}");
                    versions.Add(value);
                }
            }

            if (versions.Count >= 1)
                return;
            if (Options.ForceDeobfuscation) {
                Logger.Info("Forced deobfuscation.");
                return;
            }

            Logger.Exclamation("ConfuserEx doesn't detected. Use de4dot.");
            Logger.Exit();
        }
    }
}

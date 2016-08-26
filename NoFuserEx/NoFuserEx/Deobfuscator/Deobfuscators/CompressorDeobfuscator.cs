using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace NoFuserEx.Deobfuscator.Deobfuscators {
    internal class CompressorDeobfuscator : IDeobfuscator {
        readonly List<string> ModuleNames = new List<string>();

        string moduleName;

        public void Log() {
            Logger.Success($"Unpacked:      Module \"{moduleName}\".");
        }

        void PosibleModules() {
            // Add here all posible modules if you found a modded ConfuserEx
            var modules = new[] {
                "koi",
                "netmodule"
            };
            ModuleNames.AddRange(modules);
        }

        public bool Deobfuscate(AssemblyManager assemblyManager) {
            PosibleModules();

            var module = assemblyManager.Module;

            if (!IsPacked(module))
                return false;

            var resources = module.Resources;

            var newEntryPoint = ResolveEntryPoint(assemblyManager.Module);
            if (newEntryPoint == 0)
                Logger.Exception(
                    new Exception(
                        "Error searching entry point token, maybe the file is protected.\nOpen the NoFuserEx.exe without arguments for see all help."));

            ModifyMethod(module.EntryPoint);

            using (var stream = new MemoryStream()) {
                module.Write(stream, new ModuleWriterOptions { Logger = DummyLogger.NoThrowInstance });
                var asm = Assembly.Load(stream.ToArray());
                var method = asm.ManifestModule.ResolveMethod(module.EntryPoint.MDToken.ToInt32());
                var moduleDecrypted = (byte[])method.Invoke(null, new object[1]);
                assemblyManager.Module = ModuleDefMD.Load(moduleDecrypted);
                Logger.Verbose($"Module decrypted: {assemblyManager.Module.Name}.");
            }

            Logger.Verbose("Adding resources to new module...");
            foreach (var resource in resources) {
                assemblyManager.Module.Resources.Add(resource);
                Logger.VeryVerbose($"Resource \"{resource.Name}\" added to new module.");
            }

            Logger.Verbose("Setting new entry point...");
            assemblyManager.Module.EntryPoint = assemblyManager.Module.ResolveMethod(new MDToken(newEntryPoint).Rid);

            return true;
        }

        bool IsPacked(ModuleDefMD module) {
            // Thanks to 0xd4d https://github.com/0xd4d/dnlib/issues/72
            for (uint rid = 1; rid <= module.MetaData.TablesStream.FileTable.Rows; rid++) {
                var row = module.TablesStream.ReadFileRow(rid);
                var name = module.StringsStream.ReadNoNull(row.Name);
                if (!ModuleNames.Contains(name)) continue;
                moduleName = name;
                Logger.Verbose($"Is packed with ConfuserEx, module packed: {name}.");
                return true;
            }
            Logger.Verbose("Compressor not detected.");
            return false;
        }

        static uint ResolveEntryPoint(ModuleDefMD module) {
            Logger.Verbose("Resolving entry point of module encrypted...");
            var instructions = module.EntryPoint.Body.Instructions;

            for (var i = 0; i < instructions.Count; i++) {
                if (instructions[i].OpCode != OpCodes.Callvirt)
                    continue;

                var operand = instructions[i].Operand.ToString();
                if (!operand.Contains("System.Reflection.Module::ResolveSignature(System.Int32)"))
                    continue;

                for (var ii = i; ii >= 0; ii--) {
                    if (!instructions[ii].IsLdcI4())
                        continue;
                    var signatureToken = (uint)instructions[ii].GetLdcI4Value();
                    var signature = module.ReadBlob(signatureToken);
                    var entryPoint = (uint)(signature[0] | signature[1] << 8 | signature[2] << 16 | signature[3] << 24);
                    Logger.Verbose($"Entry point of module decrypted: {entryPoint}");
                    return entryPoint;
                }
            }
            Logger.Exception(new Exception("Error resolving entry point."));
            return 0;
        }

        static void ModifyMethod(MethodDef method) {
            var corLib = method.Module.Import(typeof(byte[]));

            method.ReturnType = corLib.ToTypeSig();

            var instructions = method.Body.Instructions;
            for (var i = 0; i < instructions.Count; i++) {
                if (instructions[i].OpCode != OpCodes.Call)
                    continue;

                var operand = instructions[i].Operand.ToString();
                if (!operand.Contains("System.Runtime.InteropServices.GCHandle::get_Target()"))
                    continue;

                instructions.Insert(i + 1, Instruction.Create(OpCodes.Castclass, corLib));
                instructions.Insert(i + 2, Instruction.Create(OpCodes.Ret));
            }
            Logger.Verbose($"Method \"{method.Name}\" adjusted to invoke.");
        }
    }
}

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using MethodAttributes = dnlib.DotNet.MethodAttributes;

namespace NoFuserEx.Deobfuscator.Deobfuscators {
    internal class AntiTamperDeobfuscator : IDeobfuscator {
        public void Log() {
            Logger.Success("Removed:       Anti-tamper protection.");
        }

        public bool Deobfuscate(AssemblyManager assemblyManager) {
            var module = assemblyManager.Module;

            var isTampered = IsTampered(module);
            if (isTampered == null)
                Logger.Exception(new Exception("Oh oh.. I can't check if the assembly has anti-tamper protection."));

            if (!(bool)isTampered)
                return false;
            
            using (var stream = module.MetaData.PEImage.CreateFullStream()) {
                var moduleArray = new byte[stream.Length];
                stream.Read(moduleArray, 0, moduleArray.Length);
                var assembly = Assembly.Load(moduleArray);
                var entryPoint =
                    assembly.ManifestModule.ResolveMethod(module.GlobalType.FindStaticConstructor().MDToken.ToInt32());

                Logger.Verbose("Decrypting methods....");
                // Thanks alot to Alcatraz3222 for help me in load cctor without invoke :P
                RuntimeHelpers.PrepareMethod(entryPoint.MethodHandle);

                var hinstance = Marshal.GetHINSTANCE(assembly.ManifestModule);
                var tableDecrypted = new byte[stream.Length];
                Marshal.Copy(hinstance, tableDecrypted, 0, tableDecrypted.Length);

                var realEntryPoint = assemblyManager.Module.EntryPoint.MDToken.Rid;

                assemblyManager.Module = ModuleDefMD.Load(tableDecrypted);
                Logger.Verbose("Have been decrypted all methods.");

                assemblyManager.Module.EntryPoint = assemblyManager.Module.ResolveMethod(realEntryPoint);

                RemoveCall(assemblyManager.Module.GlobalType.FindStaticConstructor());
            }

            return true;
        }

        static bool? IsTampered(ModuleDefMD module) {
            var sections = module.MetaData.PEImage.ImageSectionHeaders;

            if (sections.Count == 3) {
                Logger.Verbose("Anti-tamper not detected.");
                return false;
            }

            foreach (var section in sections) {
                switch (section.DisplayName) {
                    case ".text":
                    case ".rsrc":
                    case ".reloc":
                        continue;
                    default:
                        Logger.Verbose($"Anti-tamper detected in section: {section.DisplayName}.");
                        return true;
                }
            }
            return null;
        }

        static void RemoveCall(MethodDef method) {
            var instructions = method.Body.Instructions;
            foreach (var instr in instructions) {
                if (instr.OpCode != OpCodes.Call)
                    continue;

                var tamperMethod = instr.Operand as MethodDef;
                if (tamperMethod == null)
                    continue;

                const MethodAttributes attributes = MethodAttributes.Assembly | MethodAttributes.Static |
                                                    MethodAttributes.HideBySig;
                if (tamperMethod.Attributes != attributes)
                    continue;
                if (tamperMethod.CodeType != dnlib.DotNet.MethodImplAttributes.IL)
                    continue;

                if (tamperMethod.ReturnType.ElementType != ElementType.Void)
                    continue;

                instr.OpCode = OpCodes.Nop;
                instr.Operand = null;
                return;
            }
        }
    }
}

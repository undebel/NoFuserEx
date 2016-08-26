using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;

namespace NoFuserEx.Deobfuscator.Deobfuscators {
    internal class ResourcesDeobfuscator : IDeobfuscator {
        int resourcesDecrypted;
        int totalResources;

        public void Log() {
            Logger.Success($"Decrypted:     {resourcesDecrypted}/{totalResources} resource(s).");
        }

        public bool Deobfuscate(AssemblyManager assemblyManager) {
            var module = assemblyManager.Module;

            var decrypterMethod = GetDecrypterMethod(module);
            if (decrypterMethod == null)
                return false;
            var cctor = module.GlobalType.FindStaticConstructor();
            foreach (var instr in cctor.Body.Instructions) {
                if (instr.OpCode != OpCodes.Call || instr.Operand as MethodDef != decrypterMethod)
                    continue;
                instr.OpCode = OpCodes.Nop;
                instr.Operand = null;
            }

            ModifyMethod(decrypterMethod);

            using (var stream = new MemoryStream()) {
                module.Write(stream, new ModuleWriterOptions { Logger = DummyLogger.NoThrowInstance });
                var asm = Assembly.Load(stream.ToArray());
                var method = asm.ManifestModule.ResolveMethod(decrypterMethod.MDToken.ToInt32());
                var moduleDecrypted = (byte[])method.Invoke(null, null);
                var resources = ModuleDefMD.Load(moduleDecrypted).Resources;

                totalResources = module.Resources.Count;

                foreach (var resource in resources) {
                    if (!module.Resources.Remove(module.Resources.Find(resource.Name)))
                        continue;
                    module.Resources.Add(resource);
                    resourcesDecrypted++;
                }

                RemoveMethod(decrypterMethod);
            }

            return totalResources > 0;
        }

        static MethodDef GetDecrypterMethod(ModuleDef module) {
            var cctor = module.GlobalType.FindStaticConstructor();
            var instructions = cctor.Body.Instructions;
            foreach (var instruction in instructions) {
                if (instruction.OpCode != OpCodes.Call)
                    continue;

                var initializeMethod = instruction.Operand as MethodDef;
                if (initializeMethod == null)
                    continue;
                if (!initializeMethod.DeclaringType.IsGlobalModuleType)
                    continue;

                const MethodAttributes attributes =
                    MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig;
                if (initializeMethod.Attributes != attributes)
                    continue;

                if (initializeMethod.ReturnType.ElementType != ElementType.Void)
                    continue;
                if (initializeMethod.HasParamDefs)
                    continue;

                if (initializeMethod.FindInstructionsNumber(OpCodes.Call,
                        "System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(System.Array,System.RuntimeFieldHandle)") != 1)
                    continue;

                var field = FindAssemblyField(module);
                var operand = (from instr in initializeMethod.Body.Instructions
                               where instr.OpCode == OpCodes.Stsfld
                               let fieldArray = instr.Operand as FieldDef
                               where fieldArray != null
                               where field == fieldArray
                               select instr.Operand.ToString()).FirstOrDefault();

                if (initializeMethod.FindInstructionsNumber(OpCodes.Stsfld, operand) != 1)
                    continue;

                return initializeMethod;
            }
            return null;
        }

        static FieldDef FindAssemblyField(ModuleDef module) {
            foreach (var field in module.GlobalType.Fields) {
                const FieldAttributes attributes = FieldAttributes.Assembly | FieldAttributes.Static;
                if (field.Attributes != attributes)
                    continue;
                if (!field.DeclaringType.IsGlobalModuleType)
                    continue;
                if (field.FieldType.ElementType != ElementType.Class)
                    continue;
                if (field.FieldType.FullName != "System.Reflection.Assembly")
                    continue;

                return field;
            }
            return null;
        }

        static void ModifyMethod(MethodDef method) {
            var corLib = method.Module.Import(typeof(byte[]));

            method.ReturnType = corLib.ToTypeSig();

            var instructions = method.Body.Instructions;
            foreach (var instruction in instructions) {
                if (instruction.OpCode != OpCodes.Call)
                    continue;

                var operand = instruction.Operand.ToString();
                if (!operand.Contains("System.Reflection.Assembly::Load(System.Byte[])"))
                    continue;
                
                instruction.OpCode = OpCodes.Ret;
                instruction.Operand = null;
            }
            Logger.Verbose($"Method \"{method.Name}\" adjusted to invoke.");
        }

        static void RemoveMethod(MethodDef method) {
            var body = new CilBody {
                Instructions = { Instruction.Create(OpCodes.Ldnull), Instruction.Create(OpCodes.Ret) }
            };
            method.Body = body;
        }
    }
}

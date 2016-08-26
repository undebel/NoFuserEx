using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using MethodAttributes = dnlib.DotNet.MethodAttributes;

namespace NoFuserEx.Deobfuscator.Deobfuscators.Constants {
    internal class ConstantsDeobfuscator : IDeobfuscator {
        readonly List<MethodDef> decrypterMethods = new List<MethodDef>();
        int decryptedConstants;
        int detectedConstants;

        public void Log() {
            Logger.Success($"Decrypted:     {decryptedConstants}/{detectedConstants} constant(s).");
        }

        void FindDecrypterMethods(ModuleDef module) {
            foreach (var method in module.GlobalType.Methods) {
                const MethodAttributes attributes =
                    MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig;
                if (method.Attributes != attributes)
                    continue;

                if (method.GenericParameters.Count != 1)
                    continue;
                if (method.Parameters.Count != 1)
                    continue;
                if (method.Parameters[0].Type.ElementType != ElementType.U4)
                    continue;
                if (method.FindInstructionsNumber(OpCodes.Call,
                        "System.Buffer::BlockCopy(System.Array,System.Int32,System.Array,System.Int32,System.Int32)") != 2)
                    continue;
                if (method.FindInstructionsNumber(OpCodes.Call,
                        "System.Array::CreateInstance(System.Type,System.Int32)") != 1)
                    continue;
                if (method.FindInstructionsNumber(OpCodes.Callvirt,
                        "System.Text.Encoding::GetString(System.Byte[],System.Int32,System.Int32)") != 1)
                    continue;

                decrypterMethods.Add(method);
                Logger.Verbose($"Constant decrypter method detected: {method.FullName}.");
            }
            Logger.Verbose($"Decrypter methods detected: {decrypterMethods.Count}.");
        }

        public bool Deobfuscate(AssemblyManager assemblyManager) {
            var module = assemblyManager.Module;
            FindDecrypterMethods(module);

            if (decrypterMethods.Count == 0) {
                Logger.Verbose("Constants protection not detected.");
                return false;
            }

            foreach (var type in module.GetTypes()) {
                foreach (var method in type.Methods) {
                    if (!method.HasBody)
                        continue;

                    var instructions = method.Body.Instructions;
                    for (var i = 0; i < instructions.Count; i++) {
                        if (instructions[i].OpCode != OpCodes.Call)
                            continue;

                        var decrypterMethod = (instructions[i].Operand as MethodSpec).ResolveMethodDef();
                        if (decrypterMethod == null)
                            continue;

                        if (!decrypterMethod.DeclaringType.IsGlobalModuleType)
                            continue;
                        if (!decrypterMethods.Contains(decrypterMethod))
                            continue;
                        detectedConstants++;

                        DecrypterBase decrypter = null;
                        if (instructions[i].Operand.ToString().Contains("System.String"))
                            decrypter = new StringDecrypter(method, decrypterMethod, i);

                        // I know that this is a shit but for now I not found another way :(
                        else if (instructions[i].Operand.ToString().Contains("System.Byte[]"))
                            decrypter = new ByteArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.SByte[]"))
                            decrypter = new SByteArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.Char[]"))
                            decrypter = new CharArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.Int16[]"))
                            decrypter = new ShortArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.UInt16[]"))
                            decrypter = new UShortArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.Int32[]"))
                            decrypter = new IntArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.UInt32[]"))
                            decrypter = new UIntArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.Int64[]"))
                            decrypter = new LongArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.UInt64[]"))
                            decrypter = new ULongArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.Single[]"))
                            decrypter = new FloatArrayDecrypter(method, decrypterMethod, i);
                        else if (instructions[i].Operand.ToString().Contains("System.Double[]"))
                            decrypter = new DoubleArrayDecrypter(method, decrypterMethod, i);

                        if (decrypter != null && decrypter.Decrypt())
                            Logger.Verbose($"Constants decrypted: {decryptedConstants++}");

                    }
                }
            }
            if (detectedConstants == decryptedConstants)
                RemoveInitializeCall(module);

            return detectedConstants > 0;
        }

        void RemoveInitializeCall(ModuleDef module) {
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

                var field = FindArrayField(module);
                var operand = (from instr in initializeMethod.Body.Instructions
                    where instr.OpCode == OpCodes.Stsfld
                    let fieldArray = instr.Operand as FieldDef
                    where fieldArray != null
                    where field == fieldArray
                    select instr.Operand.ToString()).FirstOrDefault();
                
                if (initializeMethod.FindInstructionsNumber(OpCodes.Stsfld, operand) != 1)
                    continue;

                instruction.OpCode = OpCodes.Nop;
                instruction.Operand = null;
                Logger.Verbose("Removed constans initialize call.");
            }
        }

        static FieldDef FindArrayField(ModuleDef module) {
            foreach (var field in module.GlobalType.Fields) {
                const FieldAttributes attributes = FieldAttributes.Assembly | FieldAttributes.Static;
                if (field.Attributes != attributes)
                    continue;
                if (!field.DeclaringType.IsGlobalModuleType)
                    continue;
                if (field.FieldType.ElementType != ElementType.SZArray)
                    continue;
                if (field.FieldType.FullName != "System.Byte[]")
                    continue;

                return field;
            }
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using MethodAttributes = dnlib.DotNet.MethodAttributes;

namespace NoFuserEx.Deobfuscator.Deobfuscators {
    class ConstantsDeobfuscation : IDeobfuscator {
        readonly List<MethodDef> DecrypterMethods = new List<MethodDef>();
        int decryptedConstants;
        int constantsCount;

        public void Log() {
            Logger.Success($"Decrypted:     {decryptedConstants}/{constantsCount} constant(s).");
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
                if (method.FindInstructionsNumber(OpCodes.Call, "System.Buffer::BlockCopy(System.Array,System.Int32,System.Array,System.Int32,System.Int32)") != 2)
                    continue;
                if (method.FindInstructionsNumber(OpCodes.Call, "System.Array::CreateInstance(System.Type,System.Int32)") != 1)
                    continue;
                if (method.FindInstructionsNumber(OpCodes.Callvirt, "System.Text.Encoding::GetString(System.Byte[],System.Int32,System.Int32)") != 1)
                    continue;

                DecrypterMethods.Add(method);
            }
        }

        public bool Deobfuscate(AssemblyManager assemblyManager) {
            var module = assemblyManager.Module;
            FindDecrypterMethods(module);

            if (DecrypterMethods.Count == 0)
                return false;

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
                        if (!DecrypterMethods.Contains(decrypterMethod))
                            continue;
                        constantsCount++;

                        if (instructions[i].Operand.ToString().Contains("System.String")) {
                            var valueDecrypter = GetValueDecrypter(instructions, i);
                            var result = MethodInvoker<string>(module, decrypterMethod,
                                (uint)valueDecrypter.GetLdcI4Value());

                            valueDecrypter.OpCode = OpCodes.Nop;
                            valueDecrypter.Operand = null;
                            instructions[i].OpCode = OpCodes.Ldstr;
                            instructions[i].Operand = result;
                            decryptedConstants++;
                        }
                        else if (instructions[i].Operand.ToString().Contains("System.Byte[]")) {
                            var valueDecrypter = GetValueDecrypter(instructions, i);
                            var result = MethodInvoker<byte[]>(module, decrypterMethod,
                                (uint)valueDecrypter.GetLdcI4Value());
                            if (result == null)
                                continue;
                            valueDecrypter.OpCode = OpCodes.Nop;
                            valueDecrypter.Operand = null;
                            var decrypterInstruction = instructions[i];
                            var local = CreateArray(method, i, result);
                            decrypterInstruction.OpCode = OpCodes.Ldloc;
                            decrypterInstruction.Operand = local;
                            decryptedConstants++;
                        }

                        //TODO: Add support for all encryptions
                    }
                }
            }
            return constantsCount > 0;
        }

        //TODO: Organize code

        static Instruction GetValueDecrypter(IList<Instruction> instructions, int index) {
            for (var i = index; i >= 0; i--) {
                if (!instructions[i].IsLdcI4())
                    continue;
                return instructions[i];
            }
            return null;
        }

        Assembly assembly;
        T MethodInvoker<T>(ModuleDef module, IMDTokenProvider decrypterMethod, uint value) {
            if (assembly == null)
                LoadAssembly(module);

            var method = (MethodInfo)assembly.ManifestModule.ResolveMethod(decrypterMethod.MDToken.ToInt32());

            var result = method.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { value });
            return (T)result;
        }

        void LoadAssembly(ModuleDef module) {
            using (var stream = new MemoryStream()) {
                module.Write(stream, new ModuleWriterOptions { Logger = DummyLogger.NoThrowInstance });
                assembly = Assembly.Load(stream.ToArray());
                if (assembly == null)
                    Logger.Exception(new Exception("Error loading assembly for invoke the constants."));
            }
        }

        static Local CreateArray(MethodDef method, int index, IList<byte> array) {
            var corLib = method.Module.ImportAsTypeSig(typeof(byte[]));
            var local = method.Body.Variables.Add(new Local(corLib));
            var instructions = method.Body.Instructions;

            var list = new List<Instruction> {
                Instruction.CreateLdcI4(array.Count),
                Instruction.Create(OpCodes.Newarr, method.Module.CorLibTypes.Byte),
                Instruction.Create(OpCodes.Stloc, local)
            };
            for (var i = 0; i < array.Count; i++) {
                list.Add(Instruction.Create(OpCodes.Ldloc, local));
                list.Add(Instruction.CreateLdcI4(i));
                list.Add(Instruction.CreateLdcI4(array[i]));
                list.Add(Instruction.Create(OpCodes.Stelem_I1));
            }

            for (var i = 0; i < list.Count; i++) {
                instructions.Insert(index + i, list[i]);
            }
            return local;
        }
    }
}

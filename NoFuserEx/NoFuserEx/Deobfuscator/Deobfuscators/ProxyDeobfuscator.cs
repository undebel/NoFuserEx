using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace NoFuserEx.Deobfuscator.Deobfuscators {
    internal class ProxyDeobfuscator : IDeobfuscator {
        readonly List<MethodDef> junkMethods = new List<MethodDef>();
        int proxyFixed;
        int removedJunkMethods;

        public void Log() {
            Logger.Success($"Fixed:         {proxyFixed} proxy(s).");
            if (removedJunkMethods > 0)
                Logger.Success($"Removed:       {removedJunkMethods} junk method(s).");
        }

        public bool Deobfuscate(AssemblyManager assemblyManager) {
            foreach (var typeDef in assemblyManager.Module.GetTypes())
                foreach (var methodDef in typeDef.Methods) {
                    if (!methodDef.HasBody)
                        continue;

                    var instructions = methodDef.Body.Instructions;
                    foreach (var instruction in instructions) {
                        if (!instruction.OpCode.Equals(OpCodes.Call))
                            continue;

                        var operandAsMethodDef = instruction.Operand as MethodDef;
                        if (operandAsMethodDef == null)
                            continue;

                        const MethodAttributes attributes = MethodAttributes.PrivateScope | MethodAttributes.Static;
                        if (operandAsMethodDef.Attributes != attributes)
                            continue;
                        if (operandAsMethodDef.DeclaringType != typeDef)
                            continue;

                        OpCode opCodeProxy;
                        var operandProxy = GetProxyValues(operandAsMethodDef, out opCodeProxy);
                        if (opCodeProxy == null || operandProxy == null)
                            continue;
                        instruction.OpCode = opCodeProxy;
                        instruction.Operand = operandProxy;
                        proxyFixed++;
                        if (!junkMethods.Contains(operandAsMethodDef))
                            junkMethods.Add(operandAsMethodDef);
                    }
                }
            if (proxyFixed == 0)
                return false;

            if (!Options.NoRemoveJunkMethods)
                RemoveJunkMethods();

            return true;
        }

        static object GetProxyValues(MethodDef method, out OpCode opCode) {
            var instructions = method.Body.Instructions.ToArray();
            var validInstruction = instructions.Length - 2;
            opCode = null;
            if (!(instructions[validInstruction].OpCode.Equals(OpCodes.Newobj) ||
                instructions[validInstruction].OpCode.Equals(OpCodes.Call) ||
                instructions[validInstruction].OpCode.Equals(OpCodes.Callvirt)))
                return null;
            if (instructions[validInstruction + 1].OpCode.Code != Code.Ret)
                return null;
            if (instructions.Length != method.Parameters.Count + 2)
                return null;
            opCode = instructions[validInstruction].OpCode;
            return instructions[validInstruction].Operand;
        }

        void RemoveJunkMethods() {
            for (var i = 0; i < junkMethods.Count; i++) {
                junkMethods[i].DeclaringType.Remove(junkMethods[i]);
                removedJunkMethods++;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace NoFuserEx.Deobfuscator.Deobfuscators {
    class AntiDebuggerDeobfuscator : IDeobfuscator {
        public void Log() {
            
        }

        public bool Deobfuscate(AssemblyManager assemblyManager) {
            var instructions = assemblyManager.Module.GlobalType.FindStaticConstructor().Body.Instructions;

            foreach (var instruction in instructions) {
                if (instruction.OpCode != OpCodes.Call)
                    continue;

                var debuggerMethod = instruction.Operand as MethodDef;
                if (debuggerMethod == null)
                    continue;
                if (!debuggerMethod.DeclaringType.IsGlobalModuleType)
                    continue;

                if (debuggerMethod.FindInstructionsNumber(OpCodes.Ldstr, "ENABLE_PROFILING") == 0)
                    continue;

                return true;
            }
            return false;
        }
    }
}

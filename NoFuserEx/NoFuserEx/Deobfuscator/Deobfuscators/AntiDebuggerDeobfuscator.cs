using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace NoFuserEx.Deobfuscator.Deobfuscators {
    class AntiDebuggerDeobfuscator : IDeobfuscator {
        public void Log() {
            Logger.Success("Removed:       Anti-debugger protection.");
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

                if (debuggerMethod.FindInstructionsNumber(OpCodes.Ldstr, "ENABLE_PROFILING") != 1)
                    continue;
                if (debuggerMethod.FindInstructionsNumber(OpCodes.Ldstr, "GetEnvironmentVariable") != 1)
                    continue;
                if (debuggerMethod.FindInstructionsNumber(OpCodes.Ldstr, "COR") != 1)
                    continue;

                if (debuggerMethod.FindInstructionsNumber(OpCodes.Call, "System.Environment::FailFast(System.String)") != 1)
                    continue;

                instruction.OpCode = OpCodes.Nop;
                instruction.Operand = null;
                return true;
            }
            return false;
        }
    }
}

using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace NoFuserEx.Deobfuscator.Deobfuscators {
    internal class AntiDumperDeobfuscator : IDeobfuscator {
        public void Log() {
            Logger.Success("Removed:       Anti-dumper protection.");
        }

        public bool Deobfuscate(AssemblyManager assemblyManager) {
            var instructions = assemblyManager.Module.GlobalType.FindStaticConstructor().Body.Instructions;
            foreach (var instr in instructions) {
                if (instr.OpCode != OpCodes.Call)
                    continue;

                var dumperMethod = instr.Operand as MethodDef;
                if (dumperMethod == null)
                    continue;
                if (!dumperMethod.DeclaringType.IsGlobalModuleType)
                    continue;

                const MethodAttributes attributes = MethodAttributes.Assembly | MethodAttributes.Static |
                                                    MethodAttributes.HideBySig;
                if (dumperMethod.Attributes != attributes)
                    continue;
                if (dumperMethod.CodeType != MethodImplAttributes.IL)
                    continue;

                if (dumperMethod.ReturnType.ElementType != ElementType.Void)
                    continue;

                // Anti-dumper method have 14 calls to VirtualProtect
                if (dumperMethod.FindInstructionsNumber(OpCodes.Call, "(System.Byte*,System.Int32,System.UInt32,System.UInt32&)") != 14)
                    continue;
                instr.OpCode = OpCodes.Nop;
                instr.Operand = null;
                Logger.Verbose("Anti-dumper call was removed from .cctor.");
                return true;
            }
            return false;
        }
    }
}

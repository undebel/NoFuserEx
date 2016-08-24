using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace NoFuserEx.Deobfuscator {
    internal static class Utils {
        internal static int FindInstructionsNumber(this MethodDef method, OpCode opCode, object operand) {
            var num = 0;
            foreach (var instruction in method.Body.Instructions) {
                if (instruction.OpCode != opCode)
                    continue;
                if (operand is int) {
                    var value = instruction.GetLdcI4Value();
                    if (value == (int)operand)
                        num++;
                }
                else if (operand is string) {
                    var value = instruction.Operand.ToString();
                    if (value.Contains(operand.ToString()))
                        num++;
                }
            }
            return num;
        }
    }
}

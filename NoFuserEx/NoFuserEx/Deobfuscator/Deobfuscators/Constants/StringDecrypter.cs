using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace NoFuserEx.Deobfuscator.Deobfuscators.Constants {
    class StringDecrypter : DecrypterBase {
        readonly MethodDef method;
        readonly MethodDef decrypterMethod;
        readonly int index;
        internal StringDecrypter(MethodDef method, MethodDef decrypterMethod, int index) {
            this.method = method;
            this.decrypterMethod = decrypterMethod;
            this.index = index;
        }

        internal override bool Decrypt() {
            var instructions = method.Body.Instructions;
            var valueDecrypter = GetValueDecrypter(instructions, index);
            var result = MethodInvoker<string>(decrypterMethod, (uint)valueDecrypter.GetLdcI4Value());

            if (result == null)
                return false;

            Logger.VeryVerbose($"String decrypted: {result}");

            valueDecrypter.OpCode = OpCodes.Nop;
            valueDecrypter.Operand = null;
            instructions[index].OpCode = OpCodes.Ldstr;
            instructions[index].Operand = result;
            return true;
        }
    }
}

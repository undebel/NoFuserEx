using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace NoFuserEx.Deobfuscator.Deobfuscators.Constants {
    abstract class DecrypterBase {
        internal abstract bool Decrypt();

        static Assembly assembly;
        protected T MethodInvoker<T>(MethodDef decrypterMethod, uint value) {
            if (assembly == null)
                LoadAssembly(decrypterMethod.Module);
            if (assembly.ManifestModule.ScopeName != decrypterMethod.Module.FullName)
                LoadAssembly(decrypterMethod.Module);

            if (assembly == null)
                Logger.Exception(new Exception("Error loading assembly."));

            var method = (MethodInfo)assembly.ManifestModule.ResolveMethod(decrypterMethod.MDToken.ToInt32());

            var result = method.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { value });
            return (T)result;
        }

        static void LoadAssembly(ModuleDef module) {
            using (var stream = new MemoryStream()) {
                module.Write(stream, new ModuleWriterOptions { Logger = DummyLogger.NoThrowInstance });
                assembly = Assembly.Load(stream.ToArray());
                if (assembly == null)
                    Logger.Exception(new Exception("Error loading assembly for invoke the constants."));
            }
        }

        protected static Instruction GetValueDecrypter(IList<Instruction> instructions, int index) {
            for (var i = index; i >= 0; i--) {
                if (!instructions[i].IsLdcI4())
                    continue;
                return instructions[i];
            }
            return null;
        }
    }
}

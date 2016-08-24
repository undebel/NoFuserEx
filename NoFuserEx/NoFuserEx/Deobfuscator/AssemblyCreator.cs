using System;
using System.IO;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace NoFuserEx.Deobfuscator {
    class AssemblyCreator {
        readonly TypeDef typeDef;
        readonly MethodDef methodDef;
        int methodToInvoke;

        internal AssemblyCreator(TypeDef typeDef, MethodDef methodDef) {
            Logger.Verbose($"Cloning type {typeDef.Name}");
            this.typeDef = MemberCloner.CloneTypeDef(typeDef);
            this.methodDef = methodDef;
        }

        internal object Invoke(object[] parameters) {
            var assemblyDef = new AssemblyDefUser("NoFuserExAssembly");
            Logger.Verbose($"Assembly created: {assemblyDef.Name}");
            var moduleDef = new ModuleDefUser("NoFuserExModule");
            Logger.Verbose($"Module created: {moduleDef.Name}");
            assemblyDef.Modules.Add(moduleDef);
            Logger.VeryVerbose("Module added to the assembly.");

            moduleDef.Types.Add(typeDef);
            Logger.VeryVerbose("Type injected to module.");

            foreach (var type in moduleDef.GetTypes()) {
                foreach (var method in type.Methods) {
                    if (method.DeclaringType.Name != methodDef.DeclaringType.Name)
                        continue;
                    if (method.Name != methodDef.Name)
                        continue;
                    methodToInvoke = method.MDToken.ToInt32();
                    Logger.VeryVerbose($"Token found: {methodToInvoke}");
                }
            }
            
            if (methodToInvoke == 0)
                Logger.Exception(new Exception("Error searching the token."));

            using (var stream = new MemoryStream()) {
                assemblyDef.Write(stream, new ModuleWriterOptions { Logger = DummyLogger.NoThrowInstance });
                Logger.VeryVerbose("Assembly writed.");

                var assembly = Assembly.Load(stream.ToArray());
                Logger.VeryVerbose("Created assembly loaded.");

                var module = assembly.ManifestModule;
                var method = module.ResolveMethod(methodToInvoke);
                Logger.VeryVerbose($"Method to invoke: {method.Name}");

                if (method.IsStatic)
                    return method.Invoke(null, parameters);

                Logger.Verbose("Method is not static, creating instance...");
                var instance = Activator.CreateInstance(method.DeclaringType);
                return method.Invoke(instance, parameters);
            }
        }
    }
}

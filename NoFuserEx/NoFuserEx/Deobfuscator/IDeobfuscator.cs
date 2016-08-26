namespace NoFuserEx.Deobfuscator {
    internal interface IDeobfuscator {
        void Log();
        bool Deobfuscate(AssemblyManager assemblyManager);
    }
}

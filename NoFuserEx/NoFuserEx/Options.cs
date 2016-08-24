namespace NoFuserEx {
    internal class Options {
        internal static bool ForceDeobfuscation;
        internal static bool NoUnpack;
        internal static bool NoTamper;
        internal static bool NoStrings;
        internal static bool NoControlFlow;
        internal static bool NoProxyCalls;
        internal static bool NoRemoveJunkMethods;
        internal static bool NoResources;
        internal static bool NoRename;
        internal static bool Verbose;
        internal static bool VeryVerbose;

        internal static void RestoreOptions() {
            ForceDeobfuscation = false;
            NoUnpack = false;
            NoTamper = false;
            NoStrings = false;
            NoControlFlow = false;
            NoProxyCalls = false;
            NoRemoveJunkMethods = false;
            NoResources = false;
            NoRename = false;
            Verbose = false;
            VeryVerbose = false;
        }
    }
}

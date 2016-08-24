using System;
using System.Collections.Generic;
using System.Deployment.Internal;
using System.Linq;
using System.Text;

namespace NoFuserEx.Deobfuscator {
    internal interface IDeobfuscator {
        void Log();
        bool Deobfuscate(AssemblyManager assemblyManager);
    }
}

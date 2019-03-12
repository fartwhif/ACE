using System;

namespace ACE.Server.Managers.PluginManager
{
    public class ACEPluginType
    {
        public Type Type { get; set; } = null;
        public IACEPlugin Instance { get; set; } = null;
        public bool StartupComplete { get; set; } = false;
        public Exception StartupException { get; set; } = null;
    }
}

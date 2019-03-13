using System;
using System.Threading.Tasks;

namespace ACE.Server.Managers.PluginManager
{
    public class ACEPluginType
    {
        public Type Type { get; set; } = null;
        public IACEPlugin Instance { get; set; } = null;
        public bool StartupCalled { get; set; } = false;
        public TaskCompletionSource<bool> PluginInitComplete { get; set; } = null;
        public Exception StartupException { get; set; } = null;
    }
}

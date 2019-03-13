using System.Collections.Generic;
using System.Reflection;

namespace ACE.Server.Managers.PluginManager
{
    public class ACEPluginReferences
    {
        public Assembly PluginAssembly { get; set; } = null;
        public List<ACEPluginType> Types { get; set; } = new List<ACEPluginType>();
    }
}

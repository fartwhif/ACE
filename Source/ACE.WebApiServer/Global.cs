using System;
using System.Threading.Tasks;

namespace ACE.WebApiServer
{
    public sealed class Global
    {
        private static readonly Lazy<Global> lazy =
            new Lazy<Global>(() => new Global());
        public static Global Instance => lazy.Value;
        private Global() { _PluginInitComplete = null; }
        private TaskCompletionSource<bool> _PluginInitComplete;
        public static TaskCompletionSource<bool> PluginInitComplete { get => Instance._PluginInitComplete; set => Instance._PluginInitComplete = value; }
    }
}

using System.Threading.Tasks;

namespace ACE.Server.Managers.PluginManager
{
    public interface IACEPlugin
    {
        void Start(TaskCompletionSource<bool> tsc);
    }
}

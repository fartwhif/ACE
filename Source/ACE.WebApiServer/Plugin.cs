using ACE.Common;
using ACE.Server.Managers.PluginManager;
using ACE.WebApiServer.Managers;
using log4net;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ACE.WebApiServer
{

    public class Plugin : IACEPlugin
    {

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public void Start(TaskCompletionSource<bool> DoneStartingSignal)
        {
            Global.PluginInitComplete = DoneStartingSignal;
            AssemblyName callerN = Assembly.GetCallingAssembly().GetName();
            if (!callerN.FullName.StartsWith("ACE.Server, Version=1.0.0.0, Culture=neutral, PublicKeyToken="))
            {
                log.Fatal("Invalid startup method.  This is an ACEmulator plugin.");
                return;
            }

            if (!ConfigManager.Config.WebApi.Enabled)
            {
                log.Fatal("WebApi is disabled in configuration.  Exiting WebApi.");
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            // Init our text encoding options. This will allow us to use more than standard ANSI text, which the client also supports.
            //System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            //log4net.Repository.ILoggerRepository logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            //XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            log.Info("Initializing WebManager...");
            WebManager.Initialize();

            
        }
        private static void OnProcessExit(object sender, EventArgs e)
        {
            WebManager.Shutdown();
        }
    }
}

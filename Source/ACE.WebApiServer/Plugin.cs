using ACE.Common;
using ACE.Server.Managers.PluginManager;
using ACE.WebApiServer.Managers;
using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Reflection;

namespace ACE.WebApiServer
{
    public class Plugin: IACEPlugin
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Plugin()
        {
        }

        public static void Main(string[] args)
        {
            var p = new Plugin();
            p.Start();
        }

        public void Start()
        {
            var caller = Assembly.GetCallingAssembly().GetName().Name;
            if (caller != "ACE.Server")
                Server.Program.Start();

            if (!ConfigManager.Config.WebApi.Enabled)
            {
                log.Fatal("WebApi is disabled in configuration.  Exiting WebApi.");
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            // Init our text encoding options. This will allow us to use more than standard ANSI text, which the client also supports.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

           
            var txtCalledFrom = (caller == "ACE.WebApiServer") ? "" : $" Caller is {caller}";

            Console.WriteLine();
            log.Info($"Starting ACE.WebApiServer.{txtCalledFrom}");
            Console.Title = @"ACEmulator + WebApi";

            log.Info("Initializing WebManager...");
            WebManager.Initialize();
        }
        private static void OnProcessExit(object sender, EventArgs e)
        {
            WebManager.Shutdown();
        }
    }
}

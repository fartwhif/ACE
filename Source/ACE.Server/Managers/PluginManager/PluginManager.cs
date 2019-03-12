using ACE.Common;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace ACE.Server.Managers.PluginManager
{
    public static class PluginManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static List<ACEPluginReferences> Plugins = new List<ACEPluginReferences>();
        private static string currentlyStartingPluginPath = "";
        private static string currentlyStartingPlugin = "";
        private static readonly List<string> PluginDlls = new List<string>();
        private static readonly List<string> ACEDlls = new List<string>();
        private static string DpACEBase { get; } = new FileInfo(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath).Directory.FullName;
        private static string DpPlugins { get; } = Path.Combine(DpACEBase, "Plugins");

        public static void Initialize()
        {
            try
            {
                if (ConfigManager.Config.Plugins.Enabled && DpPlugins != null)
                {
                    FindDomainDlls();
                    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                    foreach (string addon in ConfigManager.Config.Plugins.Plugins)
                    {
                        string addonName = Path.GetFileName(addon);
                        string fpPluginDll = addonName + ".dll";
                        string dpPlugin = Path.Combine(DpPlugins, addonName);
                        string fp = Path.Combine(dpPlugin, fpPluginDll);

                        if (File.Exists(fp))
                        {
                            currentlyStartingPluginPath = dpPlugin;
                            currentlyStartingPlugin = addonName;
                            log.Info($"Loading ACE Plugin: {currentlyStartingPlugin}");
                            log.Info($"Plugin path: {DpPlugins}");
                            ACEPluginReferences add = new ACEPluginReferences();
                            try
                            {
                                Assembly assem = Assembly.LoadFile(fp);
                                add.PluginAssembly = assem;

                                IEnumerable<Type> types =
                                    from typ in assem.GetTypes()
                                    where typeof(IACEPlugin).IsAssignableFrom(typ)
                                    select typ;

                                foreach (Type type in types)
                                {
                                    ACEPluginType atyp = new ACEPluginType() { Type = type };
                                    add.Types.Add(atyp);
                                    IACEPlugin instance = (IACEPlugin)Activator.CreateInstance(type);
                                    log.Info($"Created instance of {type}");
                                    atyp.Instance = instance;
                                    try
                                    {
                                        log.Info($"Plugin {currentlyStartingPlugin}:{type} Starting.");
                                        instance.Start();
                                        log.Info($"Plugin {currentlyStartingPlugin}:{type} Started.");
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error($"Plugin {currentlyStartingPlugin}:{type} startup failed.", ex);
                                        atyp.StartupException = ex;
                                    }
                                    finally { atyp.StartupComplete = true; }
                                }
                                Plugins.Add(add);
                            }
                            catch (Exception ex)
                            {
                                log.Warn($"Unable to load Plugin: {addon}", ex);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Fatal("Plugin manager failed to initialize.", ex);
            }
            currentlyStartingPlugin = null;
            currentlyStartingPluginPath = null;
            while (Plugins.Any(k => k.Types.Any(r => !r.StartupComplete)))
            {
                Thread.Sleep(1000);
            }
        }
        private static void FindDomainDlls()
        {
            ACEDlls.AddRange(new DirectoryInfo(DpACEBase).GetFiles("*.dll", SearchOption.TopDirectoryOnly).Select(k => k.FullName));
            PluginDlls.AddRange(new DirectoryInfo(Path.Combine(DpACEBase, "Plugins")).GetFiles("*.dll", SearchOption.TopDirectoryOnly).Select(k => k.FullName));
        }
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.RequestingAssembly == null)
            {
                return null;
            }
            string assemblyFile = (args.Name.Contains(','))
                ? args.Name.Substring(0, args.Name.IndexOf(','))
                : args.Name;
            assemblyFile += ".dll";

            // first, locate all copies of the required library
            // favor ACE dlls
            List<string> foundDlls = ACEDlls.Where(k => Path.GetFileNameWithoutExtension(k) == assemblyFile).ToList();
            if (foundDlls.Count < 1)
            {
                // fallback to and favor this plugin's dlls
                foundDlls = PluginDlls.Where(k => Path.GetFileNameWithoutExtension(k) == assemblyFile).ToList();
                if (foundDlls.Count < 1)
                {
                    // finally, fallback to the dlls in the other plugins' directories and sort by created, updated time
                    foundDlls = PluginDlls.Where(k => Path.GetFileNameWithoutExtension(k) == assemblyFile).ToList();

                }
            }
            string targetPath = Path.Combine(currentlyStartingPluginPath, assemblyFile);
            if (!File.Exists(targetPath))
            {
                log.Error($"Required dependency is missing: {targetPath}");
                return null;
            }
            try
            {
                Assembly assem = Assembly.LoadFile(targetPath);
                log.Info($"{currentlyStartingPlugin} Loaded assembly: {assem.FullName}");
                return assem;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

using ACE.Common;
using log4net;
using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

namespace ACE.Server.Managers.PluginManager
{
    public static class PluginManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static List<ACEPluginReferences> Plugins = new List<ACEPluginReferences>();
        private static string curPlugPath = "";
        private static string curPlugNam = "";
        private static readonly List<Tuple<string, Assembly>> PluginDlls = new List<Tuple<string, Assembly>>();
        private static readonly List<Tuple<string, Assembly>> ACEDlls = new List<Tuple<string, Assembly>>();
        private static readonly List<Assembly> ReferencedAssemblies = new List<Assembly>();
        private static string DpACEBase { get; } = new FileInfo(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath).Directory.FullName;
        private static string DpPlugins { get; } = Path.Combine(DpACEBase, "Plugins");

        private static void FindDomainDlls()
        {

        }

        public static void Initialize()
        {
            try
            {
                if (ConfigManager.Config.Plugins.Enabled && DpPlugins != null)
                {
                    // make lists of all the DLLs in the main ACE dir and the plugin dirs
                    ACEDlls.AddRange(new DirectoryInfo(DpACEBase).GetFiles("*.dll", SearchOption.TopDirectoryOnly).Select(k => new Tuple<string, Assembly>(k.FullName, null)));
                    PluginDlls.AddRange(new DirectoryInfo(Path.Combine(DpACEBase, "Plugins")).GetFiles("*.dll", SearchOption.AllDirectories).Select(k => new Tuple<string, Assembly>(k.FullName, null)));


                    AssemblyLoadContext.Default.Resolving += Default_Resolving;

                    foreach (string addon in ConfigManager.Config.Plugins.Plugins)
                    {
                        string addonName = Path.GetFileName(addon);
                        string fpPluginDll = addonName + ".dll";
                        string dpPlugin = Path.Combine(DpPlugins, addonName);
                        string fp = Path.Combine(dpPlugin, fpPluginDll);

                        if (File.Exists(fp))
                        {
                            curPlugPath = dpPlugin;
                            curPlugNam = addonName;
                            log.Info($"Loading ACE Plugin: {curPlugNam}");
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
                                        log.Info($"Plugin {curPlugNam}:{type} Starting.");
                                        instance.Start(); // non blocking!
                                        //log.Info($"Plugin {curPlugNam}:{type} Started.");
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error($"Plugin {curPlugNam}:{type} startup failed.", ex);
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
            curPlugNam = null;
            curPlugPath = null;
            while (Plugins.Any(k => k.Types.Any(r => !r.StartupComplete)))
            {
                Thread.Sleep(1000);
            }
        }

        // https://stackoverflow.com/questions/40908568/assembly-loading-in-net-core
        private static Assembly Default_Resolving(AssemblyLoadContext context, AssemblyName name)
        {
            // avoid loading *.resources dlls, because of: https://github.com/dotnet/coreclr/issues/8416
            if (name.Name.EndsWith("resources"))
            {
                return null;
            }
            IReadOnlyList<RuntimeLibrary> dependencies = DependencyContext.Default.RuntimeLibraries;
            foreach (RuntimeLibrary library in dependencies)
            {
                if (library.Name == name.Name || library.Dependencies.Any(d => d.Name.StartsWith(name.Name)))
                {
                    return context.LoadFromAssemblyName(new AssemblyName(library.Name));
                }
            }
            List<Tuple<string, Assembly>> filList = null;
            Tuple<string, Assembly> fil = GetFavoredDependencyDll(name.Name, ref filList);
            if (fil != null && !string.IsNullOrWhiteSpace(fil.Item1))
            {
                Assembly assem = context.LoadFromAssemblyPath(fil.Item1);
                filList[filList.IndexOf(fil)] = new Tuple<string, Assembly>(filList[filList.IndexOf(fil)].Item1, assem);
                log.Info($"{curPlugNam} Loaded {fil.Item1}");
                return assem;
            }
            return context.LoadFromAssemblyName(name);
        }
        private static Tuple<string, Assembly> GetFavoredDependencyDll(string DLLFileName, ref List<Tuple<string, Assembly>> fileList)
        {
            // first, locate all copies of the required library in ACE folder
            List<Tuple<string, Assembly>> foundDlls = ACEDlls.Where(k => Path.GetFileNameWithoutExtension(k.Item1) == DLLFileName).ToList();
            if (foundDlls.Count < 1)
            {
                // fallback and favor this plugin's folder
                foundDlls = PluginDlls.Where(k => Path.GetDirectoryName(k.Item1) == curPlugPath && Path.GetFileNameWithoutExtension(k.Item1) == DLLFileName).ToList();
                if (foundDlls.Count < 1)
                {
                    // finally, fallback to the dlls in the other plugins' directories
                    foundDlls = PluginDlls.Except(foundDlls).Where(k => Path.GetDirectoryName(k.Item1) != curPlugPath && Path.GetFileNameWithoutExtension(k.Item1) == DLLFileName).ToList();
                    if (foundDlls.Count < 1)
                    {
                        return null;
                    }
                    else { fileList = PluginDlls; }
                }
                else { fileList = PluginDlls; }
            }
            else { fileList = ACEDlls; }
            return foundDlls.First();  // sort this?
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using System.Linq;
using System.Diagnostics;

using NDesk.Options;
using Newtonsoft.Json;
using Dependencies.ClrPh;

namespace Dependencies
{

    interface IPrettyPrintable
    {
        void PrettyPrint();
    }

    /// <summary>
    /// Printable KnownDlls object
    /// </summary>
    class NtKnownDlls : IPrettyPrintable
    {
        public NtKnownDlls()
        {
            x64 = Phlib.GetKnownDlls(false);
            x86 = Phlib.GetKnownDlls(true);
        }

        public void PrettyPrint()
        {
            Console.WriteLine("[-] 64-bit KnownDlls :");

            foreach (String KnownDll in this.x64)
            {
                string System32Folder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                Console.WriteLine("  {0:s}\\{1:s}", System32Folder, KnownDll);
            }

            Console.WriteLine();

            Console.WriteLine("[-] 32-bit KnownDlls :");

            foreach (String KnownDll in this.x86)
            {
                string SysWow64Folder = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
                Console.WriteLine("  {0:s}\\{1:s}", SysWow64Folder, KnownDll);
            }


            Console.WriteLine();
        }

        public List<String> x64;
        public List<String> x86;
    }

    /// <summary>
    /// Printable ApiSet schema object
    /// </summary>
    class NtApiSet : IPrettyPrintable
    {
        public NtApiSet()
        {
            Schema = Phlib.GetApiSetSchema();
        }

        public NtApiSet(PE ApiSetSchemaDll)
        {
            Schema = ApiSetSchemaDll.GetApiSetSchema();
        }

        public void PrettyPrint()
        {
            Console.WriteLine("[-] Api Sets Map :");

            foreach (var ApiSetEntry in this.Schema.GetAll())
            {
                ApiSetTarget ApiSetImpl = ApiSetEntry.Value;
                string ApiSetName = ApiSetEntry.Key;
                string ApiSetImplStr = (ApiSetImpl.Count > 0) ? String.Join(",", ApiSetImpl.ToArray()) : "";

                Console.WriteLine("{0:s} -> [ {1:s} ]", ApiSetName, ApiSetImplStr);
            }

            Console.WriteLine();
        }

        public ApiSetSchema Schema;
    }


    class PEManifest : IPrettyPrintable
    {

        public PEManifest(PE _Application)
        {
            Application = _Application;
            Manifest = Application.GetManifest();
            XmlManifest = null;
            Exception = "";

            if (Manifest.Length != 0)
            {
                try
                {
                    // Use a memory stream to correctly handle BOM encoding for manifest resource
                    using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(Manifest)))
                    {
                        XmlManifest = SxsManifest.ParseSxsManifest(stream);
                    }


                }
                catch (System.Xml.XmlException e)
                {
                    //Console.Error.WriteLine("[x] \"Malformed\" pe manifest for file {0:s} : {1:s}", Application.Filepath, PeManifest);
                    //Console.Error.WriteLine("[x] Exception : {0:s}", e.ToString());
                    XmlManifest = null;
                    Exception = e.ToString();
                }
            }
        }


        public void PrettyPrint()
        {
            Console.WriteLine("[-] Manifest for file : {0}", Application.Filepath);

            if (Manifest.Length == 0)
            {
                Console.WriteLine("[x] No embedded pe manifest for file {0:s}", Application.Filepath);
                return;
            }

            if (Exception.Length != 0)
            {
                Console.Error.WriteLine("[x] \"Malformed\" pe manifest for file {0:s} : {1:s}", Application.Filepath, Manifest);
                Console.Error.WriteLine("[x] Exception : {0:s}", Exception);
                return;
            }

            Console.WriteLine(XmlManifest);
        }

        public string Manifest;
        public XDocument XmlManifest;

        // stays private in order not end up in the json output
        private PE Application;
        private string Exception;
    }

    class PEImports : IPrettyPrintable
    {
        public PEImports(PE _Application, bool _showFunctions)
        {
            Application = _Application;
            Imports = Application.GetImports();
            showFunctions = _showFunctions;
        }

        public void PrettyPrint()
        {
            Console.WriteLine("[-] Import listing for file : {0}", Application.Filepath);

            foreach (PeImportDll DllImport in Imports)
            {
                Console.WriteLine("Import from module {0:s} :", DllImport.Name);
                if (showFunctions)
                {
                    foreach (PeImport Import in DllImport.ImportList)
                    {
                        if (Import.ImportByOrdinal)
                        {
                            Console.Write("\t Ordinal_{0:d}", Import.Ordinal);
                        }
                        else
                        {
                            Console.Write("\t Function {0:s}", Import.Name);
                        }
                        if (Import.DelayImport)
                            Console.WriteLine(" (Delay Import)");
                        else
                            Console.WriteLine("");
                    }
                }
            }

            Console.WriteLine("[-] Import listing done");
        }

        public List<PeImportDll> Imports;
        private bool showFunctions = true;
        private PE Application;
    }

    class PEExports : IPrettyPrintable
    {
        public PEExports(PE _Application)
        {
            Application = _Application;
            Exports = Application.GetExports();
        }

        public void PrettyPrint()
        {
            Console.WriteLine("[-] Export listing for file : {0}", Application.Filepath);

            foreach (PeExport Export in Exports)
            {
                Console.WriteLine("Export {0:d} :", Export.Ordinal);
                Console.WriteLine("\t Name : {0:s}", Export.Name);
                Console.WriteLine("\t VA : 0x{0:X}", (int)Export.VirtualAddress);
                if (Export.ForwardedName.Length > 0)
                    Console.WriteLine("\t ForwardedName : {0:s}", Export.ForwardedName);
            }

            Console.WriteLine("[-] Export listing done");
        }

        public List<PeExport> Exports;
        private PE Application;
    }


    class SxsDependencies : IPrettyPrintable
    {
        public SxsDependencies(PE _Application)
        {
            Application = _Application;
            SxS = SxsManifest.GetSxsEntries(Application);
        }

        public void PrettyPrint()
        {
            Console.WriteLine("[-] sxs dependencies for executable : {0}", Application.Filepath);
            foreach (var entry in SxS)
            {
                if (entry.Path.Contains("???"))
                {
                    Console.WriteLine("  [x] {0:s} : {1:s}", entry.Name, entry.Path);
                }
                else
                {
                    Console.WriteLine("  [+] {0:s} : {1:s}", entry.Name, Path.GetFullPath(entry.Path));
                }
            }
        }

        public SxsEntries SxS;
        private PE Application;

    }


    // Basic custom exception used to be able to differentiate between a "native" exception
    // and one that has been already catched, processed and rethrown
    public class RethrownException : Exception
    {
        public RethrownException(Exception e)
        : base(e.Message, e.InnerException)
        {
        }

    }


    class PeDependencyItem : IPrettyPrintable
    {
        public PeDependencyItem(PeDependencies _Root, string _ModuleName, string ModuleFilepath, ModuleSearchStrategy Strategy, int Level)
        {
            Root = _Root;
            ModuleName = _ModuleName;

            Imports = new List<PeImportDll>();
            Filepath = ModuleFilepath;
            SearchStrategy = Strategy;
            RecursionLevel = Level;

            DependenciesResolved = false;
            FullDependencies = new List<PeDependencyItem>();
            ResolvedImports = new List<PeDependencyItem>();
        }

        public void LoadPe()
        {
            if (Filepath != null)
            {
                PE Module = BinaryCache.LoadPe(Filepath);
                Imports = Module.GetImports();
            }
        }

        public void ResolveDependencies()
        {
            if (DependenciesResolved)
            {
                return;
            }

            List<PeDependencyItem> NewDependencies = new List<PeDependencyItem>();

            foreach (PeImportDll DllImport in Imports)
            {
                string ModuleFilepath = null;

                // Find Dll in "paths"
                Tuple<ModuleSearchStrategy, PE> ResolvedModule = Root.ResolveModule(DllImport.Name);
                ModuleSearchStrategy Strategy = ResolvedModule.Item1;

                switch (Strategy)
                {
                    case ModuleSearchStrategy.ApplicationDirectory:
                    case ModuleSearchStrategy.ROOT:
                    case ModuleSearchStrategy.WorkingDirectory:
                    case ModuleSearchStrategy.Environment:
                    case ModuleSearchStrategy.AppInitDLL:
                    case ModuleSearchStrategy.Fullpath:
                    case ModuleSearchStrategy.ClrAssembly:
                    case ModuleSearchStrategy.UserDefined:
                        ModuleFilepath = ResolvedModule.Item2?.Filepath;
                        break;
                    case ModuleSearchStrategy.SxS:
                    case ModuleSearchStrategy.ApiSetSchema:
                    case ModuleSearchStrategy.WellKnownDlls:
                    case ModuleSearchStrategy.System32Folder:
                    case ModuleSearchStrategy.WindowsFolder:
                        break;
                    case ModuleSearchStrategy.NOT_FOUND:
                        break;
                    default:
                        break;
                }

                bool isAlreadyCached = Root.IsModuleCached(DllImport.Name, ModuleFilepath);

                PeDependencyItem DependencyItem = Root.GetModuleItem(DllImport.Name, ModuleFilepath, Strategy, RecursionLevel + 1);

                // do not add twice the same imported module
                if (ResolvedImports.Find(ri => ri.ModuleName == DllImport.Name) == null)
                {
                    ResolvedImports.Add(DependencyItem);
                }

                // Do not process twice a dependency. It will be displayed only once
                if (!isAlreadyCached)
                {
                    Debug.WriteLine("[{0:d}] [{1:s}] Adding dep {2:s}", RecursionLevel, ModuleName, ModuleFilepath);
                    NewDependencies.Add(DependencyItem);
                }

                FullDependencies.Add(DependencyItem);
            }

            DependenciesResolved = true;
            if ((Root.MaxRecursion > 0) && ((RecursionLevel + 1) >= Root.MaxRecursion))
            {
                return;
            }

            // Recursively resolve dependencies
            foreach (var Dep in NewDependencies)
            {
                Dep.LoadPe();
                Dep.ResolveDependencies();
            }
        }

        public bool IsNewModule()
        {
            return Root.VisitModule(this.ModuleName, this.Filepath);
        }

        public void PrettyPrint()
        {
            string Tabs = string.Concat(Enumerable.Repeat("|  ", RecursionLevel));
            Console.WriteLine("{0:s}├ {1:s} ({2:s}) : {3:s}", Tabs, ModuleName, SearchStrategy.ToString(), Filepath);

            foreach (var Dep in ResolvedImports)
            {
                bool NeverSeenModule = Dep.IsNewModule();
                Dep.RecursionLevel = RecursionLevel + 1;

                if (NeverSeenModule)
                {
                    Dep.PrettyPrint();
                }
                else
                {
                    Dep.BasicPrettyPrint();
                }

            }
        }

        public void BasicPrettyPrint(int? OverrideRecursionLevel = null)
        {
            int localRecursionLevel = RecursionLevel;
            if (OverrideRecursionLevel != null)
            {
                localRecursionLevel = (int)OverrideRecursionLevel;
            }

            string Tabs = string.Concat(Enumerable.Repeat("|  ", localRecursionLevel));
            Console.WriteLine("{0:s}├ {1:s} ({2:s}) : {3:s}", Tabs, ModuleName, SearchStrategy.ToString(), Filepath);
        }

        // Json exportable
        public string ModuleName;
        public string Filepath;
        public ModuleSearchStrategy SearchStrategy;
        public List<PeDependencyItem> Dependencies
        {
            get { return IsNewModule() ? FullDependencies : new List<PeDependencyItem>(); }
        }

        // not Json exportable
        private List<PeDependencyItem> FullDependencies;
        private List<PeDependencyItem> ResolvedImports;
        private List<PeImportDll> Imports;
        private PeDependencies Root;
        private int RecursionLevel;
        private bool DependenciesResolved;
    }


    class ModuleCacheKey : Tuple<string, string>
    {
        public ModuleCacheKey(string Name, string Filepath)
        : base(Name, Filepath)
        {
        }
    }

    class ModuleEntries : Dictionary<ModuleCacheKey, PeDependencyItem>, IPrettyPrintable
    {
        public void PrettyPrint()
        {
            foreach (var item in this.Values.OrderBy(module => module.SearchStrategy))
            {
                Console.WriteLine("[{0:s}] {1:s} : {2:s}", item.SearchStrategy.ToString(), item.ModuleName, item.Filepath);
            }

        }
    }

    class PeDependencies : IPrettyPrintable
    {
        public PeDependencies(PE Application, int recursion_depth)
        {
            string RootFilename = Path.GetFileName(Application.Filepath);

            RootPe = Application;
            SxsEntriesCache = SxsManifest.GetSxsEntries(RootPe);
            ModulesCache = new ModuleEntries();
            MaxRecursion = recursion_depth;

            ModulesVisited = new Dictionary<ModuleCacheKey, bool>();

            Root = GetModuleItem(RootFilename, Application.Filepath, ModuleSearchStrategy.ROOT, 0);
            Root.LoadPe();
            Root.ResolveDependencies();
        }

        public Tuple<ModuleSearchStrategy, PE> ResolveModule(string ModuleName)
        {
            return BinaryCache.ResolveModule(
                RootPe,
                ModuleName /*DllImport.Name*/
            );
        }

        public bool IsModuleCached(string ModuleName, string ModuleFilepath)
        {
            // Do not process twice the same item
            ModuleCacheKey ModuleKey = new ModuleCacheKey(ModuleName, ModuleFilepath);
            return ModulesCache.ContainsKey(ModuleKey);
        }

        public PeDependencyItem GetModuleItem(string ModuleName, string ModuleFilepath, ModuleSearchStrategy SearchStrategy, int RecursionLevel)
        {
            // Do not process twice the same item
            ModuleCacheKey ModuleKey = new ModuleCacheKey(ModuleName, ModuleFilepath);
            if (!ModulesCache.ContainsKey(ModuleKey))
            {
                ModulesCache[ModuleKey] = new PeDependencyItem(this, ModuleName, ModuleFilepath, SearchStrategy, RecursionLevel);
            }

            return ModulesCache[ModuleKey];
        }

        public void PrettyPrint()
        {
            ModulesVisited = new Dictionary<ModuleCacheKey, bool>();
            Root.PrettyPrint();
        }

        public bool VisitModule(string ModuleName, string ModuleFilepath)
        {
            //ModuleCacheKey ModuleKey = new ModuleCacheKey(ModuleName, ModuleFilepath);
            ModuleCacheKey ModuleKey = new ModuleCacheKey("", ModuleFilepath);

            // do not visit recursively the same node (in order to prevent stack overflow)
            if (ModulesVisited.ContainsKey(ModuleKey))
            {
                return false;
            }

            ModulesVisited[ModuleKey] = true;
            return true;
        }

        public ModuleEntries GetModules
        {
            get { return ModulesCache; }
        }

        public PeDependencyItem Root;
        public int MaxRecursion;

        private PE RootPe;
        private SxsEntries SxsEntriesCache;
        private ModuleEntries ModulesCache;
        private Dictionary<ModuleCacheKey, bool> ModulesVisited;
    }



    class Program
    {
        public static void PrettyPrinter(IPrettyPrintable obj)
        {
            obj.PrettyPrint();
        }

        public static void JsonPrinter(IPrettyPrintable obj)
        {
            JsonSerializerSettings Settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                //PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            };

            Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented, Settings));
        }

        static PE CreatePe(string filename)
        {
            if (!NativeFile.Exists(filename))
                throw new Exception(String.Format("[x] Could not find file {0:s} on disk", filename));

            Debug.WriteLine("[-] Loading file {0:s}", filename);
            PE pe = new PE(filename);
            if (!pe.Load())
                throw new Exception(String.Format("[x] Could not load file {0:s} as a PE", filename));
            return pe;
        }

        public static void DumpKnownDlls(Action<IPrettyPrintable> Printer)
        {
            NtKnownDlls KnownDlls = new NtKnownDlls();
            Printer(KnownDlls);
        }

        public static void DumpApiSets(Action<IPrettyPrintable> Printer)
        {
            NtApiSet ApiSet = new NtApiSet();
            Printer(ApiSet);
        }

        public static void DumpApiSets(Action<IPrettyPrintable> Printer, string filename)
        {
            var pe = CreatePe(filename);
            NtApiSet ApiSet = new NtApiSet(pe);
            Printer(ApiSet);
        }

        public static void DumpManifest(Action<IPrettyPrintable> Printer, string filename)
        {
            var pe = CreatePe(filename);
            PEManifest Manifest = new PEManifest(pe);
            Printer(Manifest);
        }

        public static void DumpSxsEntries(Action<IPrettyPrintable> Printer, string filename)
        {
            var pe = CreatePe(filename);
            SxsDependencies SxsDeps = new SxsDependencies(pe);
            Printer(SxsDeps);
        }

        public static void DumpExports(Action<IPrettyPrintable> Printer, string filename)
        {
            var pe = CreatePe(filename);
            PEExports Exports = new PEExports(pe);
            Printer(Exports);
        }

        public static void DumpImports(Action<IPrettyPrintable> Printer, string filename, bool showFunctions)
        {
            var pe = CreatePe(filename);
            PEImports Imports = new PEImports(pe, showFunctions);
            Printer(Imports);
        }

        public static void DumpDependencyChain(Action<IPrettyPrintable> Printer, string filename, int recursion_depth)
        {
            PE root = CreatePe(filename);
            PeDependencies Deps = new PeDependencies(root, recursion_depth);
            Printer(Deps);
        }

        public static void DumpModules(Action<IPrettyPrintable> Printer, string filename, int recursion_depth = 0)
        {
            var pe = CreatePe(filename);
            if (Printer == JsonPrinter)
            {
                Console.Error.WriteLine("Json output is not currently supported when dumping the dependency chain.");
                return;
            }

            PeDependencies Deps = new PeDependencies(pe, recursion_depth);
            Printer(Deps.GetModules);
        }

        public static void DumpUsage()
        {
            string Usage = String.Join(Environment.NewLine,
                "Dependencies.exe : command line tool for dumping dependencies and various utilities.",
                "",
                "Usage : Dependencies.exe [OPTIONS] <FILE>",
                "",
                "Options :",
                "  -h -help : display this help",
                "  -json : activate json output.",
                "  -depth : limit recursion depth when analysing loaded modules or dependency chain. Default value is infinite.",
                "  -apisets : dump the system's ApiSet schema (api set dll -> host dll)",
                "  -apisetsdll : dump the ApiSet schema from apisetschema <FILE> (api set dll -> host dll)",
                "  -knowndll : dump all the system's known dlls (x86 and x64)",
                "  -manifest : dump <FILE> embedded manifest, if it exists.",
                "  -sxsentries : dump all of <FILE>'s sxs dependencies.",
                "  -imports : dump <FILE> imports",
                "  -exports : dump <FILE> exports",
                "  -modules : dump <FILE> resolved modules",
                "  -chain : dump <FILE> whole dependency chain"

            );

            Console.WriteLine(Usage);
        }

        static Action<IPrettyPrintable> GetObjectPrinter(bool export_as_json)
        {
            if (export_as_json)
                return JsonPrinter;

            return PrettyPrinter;
        }

        static void Main(string[] args)
        {
            // always the first call to make
            Phlib.InitializePhLib();

            int recursion_depth = 0;
            bool show_help = false;
            bool export_as_json = false;
            bool showFunctions = false;
            Action command = null;
            string filename = null;

            OptionSet opts = new OptionSet() {
                            { "h|help",  "show this message and exit", v => show_help = v != null },
                            { "json",  "Export results in json format", v => export_as_json = v != null },
                            { "f|functions",  "Show functions and thingies", v => export_as_json = v != null },
                            { "d|depth=",  "limit recursion depth when analysing loaded modules or dependency chain. Default value is infinite", (int v) =>  recursion_depth = v },
                            { "knowndll", "List all known dlls", v => command = () => DumpKnownDlls(GetObjectPrinter(export_as_json)) },
                            { "apisets", "List apisets redirections", v => command = () => DumpApiSets(GetObjectPrinter(export_as_json)) },
                            { "apisetsdll", "List apisets redirections from apisetschema <FILE>", v => command = () => DumpApiSets(GetObjectPrinter(export_as_json), filename) },
                            { "manifest", "show manifest information embedded in <FILE>", v => command = () => DumpManifest(GetObjectPrinter(export_as_json), filename) },
                            { "sxsentries", "dump all of <FILE>'s sxs dependencies", v => command = () => DumpSxsEntries(GetObjectPrinter(export_as_json), filename) },
                            { "imports", "dump <FILE> imports", v => command = () => DumpImports(GetObjectPrinter(export_as_json), filename, showFunctions) },
                            { "exports", "dump <FILE> exports", v => command = () => DumpExports(GetObjectPrinter(export_as_json), filename) },
                            { "chain", "dump <FILE> whole dependency chain", v => command = () => DumpDependencyChain(GetObjectPrinter(export_as_json), filename, recursion_depth) },
                            { "modules", "dump <FILE> resolved modules", v => command = () => DumpModules(GetObjectPrinter(export_as_json), filename, recursion_depth) },
                        };

            List<string> eps = opts.Parse(args);

            if ((show_help) || (args.Length == 0) || (command == null))
            {
                DumpUsage();
                return;
            }

            if (eps.Count == 0)
            {
                Console.Error.WriteLine("[x] Command {0:s} needs to have a PE <FILE> argument", command.Method.Name);
                Console.Error.WriteLine("");

                DumpUsage();
                return;
            }

            filename = eps[0];

            BinaryCache.Instance = new BinaryNoCacheImpl();
            BinaryCache.Instance.Load();

            command();
        }
    }
}

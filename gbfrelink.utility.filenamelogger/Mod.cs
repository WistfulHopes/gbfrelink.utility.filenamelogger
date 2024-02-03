using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Reloaded.Mod.Interfaces;
using gbfrelink.utility.filenamelogger.Template;
using gbfrelink.utility.filenamelogger.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace gbfrelink.utility.filenamelogger;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    private Config _configuration;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    private static IStartupScanner? _startupScanner = null!;
    private IHook<FileHashDelegate> _fileHashHook = null!;
    private string _filesToAdd = "";
    private HashSet<string> _existingLines = null!;
    
    private unsafe delegate ulong FileHashDelegate(byte* name, ulong length, ulong key);
    
    private static void SigScan(string pattern, string name, Action<nint> action)
    {
        var baseAddress = Process.GetCurrentProcess().MainModule!.BaseAddress;
        _startupScanner?.AddMainModuleScan(pattern, result =>
        {
            if (!result.Found)
            {
                return;
            }
            action(result.Offset + baseAddress);
        });
    }

    private void AppendIfNotExist(string val)
    {
        if (_existingLines.Contains(val) || _filesToAdd.Contains(val)) return;
        _filesToAdd += val + "\n";
    }
    
    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;
        
        var startupScannerController = _modLoader.GetController<IStartupScanner>();
        if (startupScannerController == null || !startupScannerController.TryGetTarget(out _startupScanner))
        {
            return;
        }

        var fileNames = File.Exists(Directory.GetParent(Assembly.GetExecutingAssembly().Location) + @"\filelist.txt")
            ? File.ReadAllText(Directory.GetParent(Assembly.GetExecutingAssembly().Location) + @"\filelist.txt")
            : "";
        _existingLines = new HashSet<string>(fileNames.Split(new string[] { Environment.NewLine },
            StringSplitOptions.RemoveEmptyEntries));

        SigScan("41 57 41 56 41 55 41 54 56 57 55 53 49 BB 4F EB D4 27 3D AE B2 C2", "FileHasher", address =>
        {
            unsafe
            {
                _fileHashHook = _hooks!.CreateHook<FileHashDelegate>(FileHash, address).Activate();
            }
        });
        
        var thread = new Thread(WriteFileNames);
        thread.Start();
    }

    private void WriteFileNames()
    {
        while (true)
        {
            foreach (var path in _filesToAdd.Split(new[] { Environment.NewLine },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                _existingLines.Add(path);
            }
            File.AppendAllText(Directory.GetParent(Assembly.GetExecutingAssembly().Location) + @"\filelist.txt", _filesToAdd);
            _filesToAdd = "";
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }
    
    private unsafe ulong FileHash(byte* name, ulong length, ulong key)
    {
        if (length > 260) return _fileHashHook.OriginalFunction(name, length, key);
        try
        {
            var buffer = new byte[length];
        
            for (ulong i = 0; i < length; i++)
            {
                buffer[i] = name[i];
            }
        
            var filename = buffer.BytesToString();
            if (!filename.All(char.IsAscii)) return _fileHashHook.OriginalFunction(name, length, key);
            AppendIfNotExist(filename);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return _fileHashHook.OriginalFunction(name, length, key);
        }
        
        return _fileHashHook.OriginalFunction(name, length, key);
    }
    
    #region Standard Overrides

    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    #endregion

    #region For Exports, Serialization etc.

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod()
    {
    }
#pragma warning restore CS8618

    #endregion
}
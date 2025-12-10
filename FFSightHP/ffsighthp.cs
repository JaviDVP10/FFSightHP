using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFSightHP.Windows;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System;
using System.IO;
using System.Linq;
using System.Transactions;

namespace FFSightHP;

public sealed class ffsighthp : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    // IFramework se usa para suscribirse a actualizaciones por frame
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CommandName = "/ffsighthp";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("ffsighthp");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }


    // Declarar variables para llamar a la ventana
    public string clase { get; private set; } = string.Empty;
    public string hpstring { get; private set; } = string.Empty;

    public ffsighthp()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // You might normally want to embed resources and load them from the manifest stream

        // Creo una ventana de configuración llamando a la CofingWindow
        ConfigWindow = new ConfigWindow(this);

        // Creo una ventana principal llamando a la ConfigWindow y la cargo en el sistema de ventanas
        WindowSystem.AddWindow(ConfigWindow);

       





        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "To access config"
        });

        // Tell the UI system that we want our windows to be drawn throught he window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        // Suscríbete al update del framework para hacer polling del objetivo actual
        

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Log.Information($"===PLugin {PluginInterface.Manifest.Name} Loaded.===");

        Framework.Update += OnFrameworkUpdate;

    }





    private void OnFrameworkUpdate(IFramework framework){

        var player = ClientState.LocalPlayer;

        this.clase = ClientState.LocalPlayer.ClassJob.Value.Abbreviation.ToString();

        var hpstringS = ClientState.LocalPlayer.TargetObject as Dalamud.Game.ClientState.Objects.Types.ICharacter;

        this.hpstring = hpstringS.CurrentHp.ToString() + "/" + hpstringS.MaxHp.ToString();


        return;
    }





    public void Dispose()
    {
        // Unregister all actions to not leak anythign during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        // Anular la suscripción al Update
        Framework.Update -= OnFrameworkUpdate;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        ConfigWindow.Toggle();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}

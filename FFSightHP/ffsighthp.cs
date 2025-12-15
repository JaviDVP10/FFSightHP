using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFSightHP.Windows;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FFSightHP;

public sealed class ffsighthp : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/ffsighthp";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("ffsighthp");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public string clase { get; private set; } = string.Empty;
    public string hpstring { get; private set; } = string.Empty;

    public string mensaje_log { get; private set; } = "Esperando mensajes...";
    public string mensaje_log2 { get; private set; } = string.Empty;
    public string mensaje_log3 { get; private set; } = "Daño Total: 0";
    public string mensaje_log4 { get; private set; } = "DPS: 0.00";

    // Contador de daño
    private ulong totalDamage;
    private DateTime sessionStartTime;
    private DateTime lastDPSUpdate;
    private DateTime lastDamageTime;
    private const double DPSUpdateIntervalSeconds = 2.0;
    private const double InactivityTimeoutSeconds = 20.0;

    public ulong TotalDamage => totalDamage;
    public string FormattedDPS => CalculateDPS();

    public ffsighthp()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "To access config"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"===Plugin {PluginInterface.Manifest.Name} Loaded.===");

        Framework.Update += OnFrameworkUpdate;
        ChatGui.ChatMessage += OnChatMessage;

        sessionStartTime = DateTime.Now;
        lastDPSUpdate = DateTime.Now;
        lastDamageTime = DateTime.Now;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        var messageText = message.TextValue.Trim();



        // Guardar el mensaje completo para debug
        this.mensaje_log = messageText;

        // Regex mejorado para capturar TODOS los mensajes de daño
        // Patrones:
        // 1. "You hit the X for XXXX damage"
        // 2. "The X takes XXXX damage"
        // 3. "You deal XXXX damage"
        // 4. Con modificadores: "Critical!", "Direct hit!", etc.
        // 5. Con porcentajes: "The X takes XXXX (+57%) damage"
        
        var damageMatch = Regex.Match(messageText, @"(?:You\s+(?:hit|deal).*?for\s+|takes\s+)(\d+)\s*(?:\([^)]*\))?\s*damage");

        if (damageMatch.Success && ulong.TryParse(damageMatch.Groups[1].Value, out var damage))
        {
            // Evitar contar daño que recibes
            if (!messageText.Contains("hits you"))
            {
                totalDamage += damage;
                lastDamageTime = DateTime.Now;
                this.mensaje_log2 = DateTime.Now.ToString("HH:mm:ss");
                this.mensaje_log3 = $"Daño Total: {totalDamage}";
                Log.Information($"Daño capturado: {damage} | Total: {totalDamage}");
            }
        }
    }

    private string CalculateDPS()
    {
        var elapsed = DateTime.Now - sessionStartTime;
        if (elapsed.TotalSeconds > 0)
        {
            var dps = totalDamage / elapsed.TotalSeconds;
            return $"{dps:F2}";
        }
        return "0.00";
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var player = ClientState.LocalPlayer;
        if (player == null)
            return;

        this.clase = player.ClassJob.Value.Abbreviation.ToString();

        var hpstringS = ClientState.LocalPlayer.TargetObject as ICharacter;
        if (hpstringS != null)
        {
            this.hpstring = hpstringS.CurrentHp.ToString() + "/" + hpstringS.MaxHp.ToString();
        }

        // Actualizar DPS solo cada 2 segundos
        var elapsedDPS = DateTime.Now - lastDPSUpdate;
        if (elapsedDPS.TotalSeconds >= DPSUpdateIntervalSeconds)
        {
            this.mensaje_log4 = FormattedDPS;
            lastDPSUpdate = DateTime.Now;
        }

        // Verificar si han pasado 20 segundos sin daño y reiniciar si es necesario
        var elapsedInactivity = DateTime.Now - lastDamageTime;
        if (elapsedInactivity.TotalSeconds >= InactivityTimeoutSeconds && totalDamage > 0)
        {
            Log.Information($"Inactividad detectada ({elapsedInactivity.TotalSeconds:F1}s). Reiniciando contador de daño.");
            ResetDamageCounter();
        }
    }

    public void ResetDamageCounter()
    {
        totalDamage = 0;
        sessionStartTime = DateTime.Now;
        lastDPSUpdate = DateTime.Now;
        lastDamageTime = DateTime.Now;
        this.mensaje_log3 = "Daño Total: 0";
        this.mensaje_log4 = "DPS: 0.00";
        Log.Information("Contador de daño reiniciado");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        Framework.Update -= OnFrameworkUpdate;
        ChatGui.ChatMessage -= OnChatMessage;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        ConfigWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}


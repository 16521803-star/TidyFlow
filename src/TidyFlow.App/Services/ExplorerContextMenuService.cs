using System.Diagnostics;
using Microsoft.Win32;

namespace TidyFlow.App.Services;

/// <summary>
/// Registers per-user (HKCU, no admin required) Windows Explorer context menu entries for TidyFlow.
/// </summary>
public static class ExplorerContextMenuService
{
    private const string DirShellKey = @"Software\Classes\Directory\shell\TidyFlowOrganize";
    private const string DirBgShellKey = @"Software\Classes\Directory\Background\shell\TidyFlowOrganize";
    private const string FileShellKey = @"Software\Classes\*\shell\TidyFlowRename";

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(DirShellKey, writable: false);
        return key is not null;
    }

    public static void Register()
    {
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new InvalidOperationException("Unable to determine TidyFlow executable path.");
        }

        SetMenuEntry(DirShellKey, "Organize folder with TidyFlow", exePath, $"\"{exePath}\" --organize \"%1\"");
        SetMenuEntry(DirBgShellKey, "Organize this folder with TidyFlow", exePath, $"\"{exePath}\" --organize \"%V\"");
        SetMenuEntry(FileShellKey, "Batch Rename with TidyFlow", exePath, $"\"{exePath}\" --rename \"%1\"");
    }

    public static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(DirShellKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(DirBgShellKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(FileShellKey, throwOnMissingSubKey: false);
    }

    private static void SetMenuEntry(string keyPath, string label, string iconPath, string commandLine)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue(null, label);
        key.SetValue("Icon", iconPath);

        using var commandKey = key.CreateSubKey("command");
        commandKey.SetValue(null, commandLine);
    }
}

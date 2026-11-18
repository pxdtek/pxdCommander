using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace pxdCommander.Core;

/// <summary>
/// "Default file manager": folders and drives opened from the desktop, shortcuts, Run, other apps and Win+E open in
/// pxd Commander instead of Explorer. Per user (HKCU), no administrator rights. The folder and drive defaults that were
/// there before are saved and put back when it's switched off. Explorer's own windows and the taskbar's File Explorer
/// button still open Explorer.
/// </summary>
public static class DefaultFileManager
{
    public const string Verb = "pxdCommander";
    private const string DirectoryShell = @"Software\Classes\Directory\shell";
    private const string DriveShell = @"Software\Classes\Drive\shell";
    private const string BackupKey = @"Software\pxdCommander\DefaultFileManager";
    // Win+E opens this ("This PC / Quick access") through its opennewwindow verb.
    internal const string WinEKey = @"Software\Classes\CLSID\{52205fd8-5dfb-447d-801a-d0b52f2e83e1}\shell\opennewwindow\command";

    private static RegistryKey Root => Installer.Root;

    public static bool IsSet
    {
        get
        {
            using var key = Root.OpenSubKey(DirectoryShell);
            return string.Equals(key?.GetValue("") as string, Verb, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Makes pxd Commander (this exe) the default for folders, drives and Win+E.</summary>
    public static void Set(string exe)
    {
        if (!IsSet)
        {
            using var backup = Root.CreateSubKey(BackupKey);
            backup.SetValue("PrevDirectoryVerb", CurrentDefault(DirectoryShell));
            backup.SetValue("PrevDriveVerb", CurrentDefault(DriveShell));
            backup.SetValue("HadFolderMenu", Installer.HasFolderMenu ? 1 : 0, RegistryValueKind.DWord);
        }
        Write(exe);
        Announce();
    }

    /// <summary>Puts back whatever opened folders and drives before (Explorer).</summary>
    public static void Unset()
    {
        if (!IsSet) return;
        bool hadMenu;
        using (var backup = Root.OpenSubKey(BackupKey))
        {
            Restore(DirectoryShell, backup?.GetValue("PrevDirectoryVerb") as string);
            Restore(DriveShell, backup?.GetValue("PrevDriveVerb") as string);
            hadMenu = backup?.GetValue("HadFolderMenu") is int i && i != 0;
        }
        Root.DeleteSubKeyTree(@"Software\Classes\CLSID\{52205fd8-5dfb-447d-801a-d0b52f2e83e1}\shell\opennewwindow", throwOnMissingSubKey: false);
        if (!hadMenu)
        {
            Root.DeleteSubKeyTree(DirectoryShell + @"\" + Verb, throwOnMissingSubKey: false);
            Root.DeleteSubKeyTree(DriveShell + @"\" + Verb, throwOnMissingSubKey: false);
        }
        Root.DeleteSubKeyTree(BackupKey, throwOnMissingSubKey: false);
        Announce();
    }

    /// <summary>After an install or update: point the entries at the (possibly moved) exe.</summary>
    public static void Refresh(string exe)
    {
        if (IsSet) Write(exe);
    }

    /// <summary>The folder menu keeps these keys while it's the default (an update may pass --no-folder-menu).</summary>
    internal static bool Keeps(string menuKey) =>
        IsSet && (menuKey.Equals(DirectoryShell + @"\" + Verb, StringComparison.OrdinalIgnoreCase)
                  || menuKey.Equals(DriveShell + @"\" + Verb, StringComparison.OrdinalIgnoreCase));

    private static void Write(string exe)
    {
        foreach (var shell in new[] { DirectoryShell, DriveShell })
        {
            using (var key = Root.CreateSubKey(shell + @"\" + Verb))
            {
                key.SetValue("MUIVerb", "Open in pxd Commander");
                key.SetValue("Icon", exe + ",0");
                using var cmd = key.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" \"%V\"");
            }
            using var shellKey = Root.CreateSubKey(shell);
            shellKey.SetValue("", Verb);
        }
        using var winE = Root.CreateSubKey(WinEKey);
        winE.SetValue("", $"\"{exe}\"");
        winE.SetValue("DelegateExecute", ""); // an empty value here makes Windows run the command instead of Explorer
    }

    private static string CurrentDefault(string shell)
    {
        using var key = Root.OpenSubKey(shell);
        return key?.GetValue("") as string ?? "";
    }

    private static void Restore(string shell, string? previous)
    {
        using var key = Root.CreateSubKey(shell);
        if (string.IsNullOrEmpty(previous) || previous.Equals(Verb, StringComparison.OrdinalIgnoreCase)) key.DeleteValue("", throwOnMissingValue: false);
        else key.SetValue("", previous);
    }

    private static void Announce()
    {
        if (!Installer.Testing) SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}

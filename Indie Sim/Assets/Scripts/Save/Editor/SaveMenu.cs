using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor helpers for the save folder (save-system-spec.md §2, Editor testability).</summary>
public static class SaveMenu
{
    private const string Root = "Tools/Save/";

    [MenuItem(Root + "Open Save Folder", priority = 0)]
    private static void OpenSaveFolder()
    {
        Directory.CreateDirectory(SaveService.DefaultFolder);
        EditorUtility.RevealInFinder(SaveService.DefaultFolder + Path.DirectorySeparatorChar);
    }

    [MenuItem(Root + "Dump Slot 0", priority = 20)] private static void DumpSlot0() => Dump(SlotPath(0));
    [MenuItem(Root + "Dump Slot 1", priority = 21)] private static void DumpSlot1() => Dump(SlotPath(1));
    [MenuItem(Root + "Dump Slot 2", priority = 22)] private static void DumpSlot2() => Dump(SlotPath(2));
    [MenuItem(Root + "Dump Profile", priority = 23)] private static void DumpProfile() => Dump(Path.Combine(SaveService.DefaultFolder, "profile.json"));

    [MenuItem(Root + "Delete All Saves", priority = 40)]
    private static void DeleteAllSaves()
    {
        string folder = SaveService.DefaultFolder;
        if (!Directory.Exists(folder))
        {
            Debug.Log($"[SaveMenu] No save folder at {folder}.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Delete All Saves",
                $"Delete every slot and the profile in:\n{folder}\n\nThis can't be undone.", "Delete", "Cancel"))
            return;

        Directory.Delete(folder, recursive: true);
        Debug.Log($"[SaveMenu] Deleted {folder}.");
        if (Application.isPlaying)
            Debug.LogWarning("[SaveMenu] Deleted during Play — the running SaveService still has its in-memory copy and may write again.");
    }

    private static string SlotPath(int index) => Path.Combine(SaveService.DefaultFolder, $"slot_{index}.json");

    private static void Dump(string path)
    {
        string source = File.Exists(path) ? path : path + ".bak";
        if (!File.Exists(source))
        {
            Debug.Log($"[SaveMenu] {Path.GetFileName(path)} doesn't exist (no .bak either).");
            return;
        }

        string label = source == path ? Path.GetFileName(path) : $"{Path.GetFileName(path)} (main missing — showing .bak)";
        Debug.Log($"[SaveMenu] {label}:\n{File.ReadAllText(source)}");
    }
}

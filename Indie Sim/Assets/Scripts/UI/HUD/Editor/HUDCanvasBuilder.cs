using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static UIBuildKit;

/// <summary>
/// One-click builder for the gameplay HUD canvas prefab, split out of the
/// Player Canvas (which keeps the menus):
///   - HUDCanvas: overlay canvas under the Player Canvas' menus, same scaler,
///     no raycaster (nothing on it is clickable), GameHUD for show/hide
///   - Timer + Ammo Count: copies of the Player Canvas ones (same look and
///     position), now driven by HUDTimer / HUDAmmo
///   - BurningCigsPanel: copy of the Player Canvas one, BurningCigsHUD intact
///   - Stomp Indicator: filled bar + "STOMP" label + Chain Stomp charge pips
///   - Held Cigs Panel: a pool of "HUD Cig" prefabs (cig art + rarity border)
/// The originals in the Player Canvas are deactivated, not deleted. Sprites
/// are Unity's default UI sprite — placeholders to swap art on.
/// Skipped if the HUD prefab already exists; delete it to rebuild.
/// </summary>
public static class HUDCanvasBuilder
{
    private const string PlayerCanvasPath = "Assets/Prefabs/UI/Player Canvas RoguelikeMode.prefab";
    private const string HudPrefabPath = "Assets/Prefabs/UI/HUD Canvas.prefab";
    private const string CigPrefabPath = "Assets/Prefabs/UI/HUD Cig.prefab";
    private const int HudSortingOrder = 900; // under the Player Canvas (1000) so menus draw on top

    private static readonly string[] MovedToHud = { "Timer", "Timer (1)", "Ammo Count", "BurningCigsPanel" };

    [MenuItem("Tools/HUD/Build HUD Canvas Prefab", priority = 120)]
    private static void BuildPrefab()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Build HUD Canvas", "Exit Play mode first.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath) != null)
        {
            Debug.Log($"[HUDCanvasBuilder] {HudPrefabPath} already exists — delete it to rebuild.");
            return;
        }

        GameObject playerCanvas = PrefabUtility.LoadPrefabContents(PlayerCanvasPath);
        GameObject hud = null;
        try
        {
            GameObject cigPrefab = BuildCigPrefab();
            hud = BuildHud(playerCanvas.transform, cigPrefab);
            PrefabUtility.SaveAsPrefabAsset(hud, HudPrefabPath);

            foreach (string name in MovedToHud)
            {
                Transform original = playerCanvas.transform.Find(name);
                if (original != null) original.gameObject.SetActive(false);
            }
            PrefabUtility.SaveAsPrefabAsset(playerCanvas, PlayerCanvasPath);

            Debug.Log($"[HUDCanvasBuilder] Built {HudPrefabPath}; deactivated the old HUD elements in the Player Canvas.");
        }
        finally
        {
            if (hud != null) Object.DestroyImmediate(hud);
            PrefabUtility.UnloadPrefabContents(playerCanvas);
        }
    }

    [MenuItem("Tools/HUD/Add HUD Canvas To Open Scene", priority = 121)]
    private static void AddToScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[HUDCanvasBuilder] No {HudPrefabPath} — build it first.");
            return;
        }
        if (Object.FindFirstObjectByType<GameHUD>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("[HUDCanvasBuilder] This scene already has a GameHUD.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Add HUD Canvas");
        EditorSceneManager.MarkSceneDirty(instance.scene);
        Selection.activeGameObject = instance;
    }

    private static GameObject BuildHud(Transform playerCanvas, GameObject cigPrefab)
    {
        GameObject root = new GameObject("HUDCanvas", typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = HudSortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        CanvasScaler sourceScaler = playerCanvas.GetComponent<CanvasScaler>();
        if (sourceScaler != null) EditorUtility.CopySerialized(sourceScaler, scaler);

        root.AddComponent<GameHUD>();

        TMP_Text sourceTimer = playerCanvas.Find("Timer")?.GetComponent<TMP_Text>();
        TMP_FontAsset font = sourceTimer != null ? sourceTimer.font : null;

        BuildTimer(root.transform, playerCanvas);
        BuildAmmo(root.transform, playerCanvas);
        CopyFrom(playerCanvas, "BurningCigsPanel", root.transform);
        BuildStomp(root.transform, font);
        BuildHeldCigs(root.transform, cigPrefab);

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        return root;
    }

    // Full-screen group with its own canvas, so the copies keep their Player Canvas positions
    // and the once-a-second text change doesn't rebuild the rest of the HUD.
    private static void BuildTimer(Transform root, Transform playerCanvas)
    {
        RectTransform group = NewRect("Timer", root);
        Stretch(group);
        group.gameObject.AddComponent<Canvas>();

        TMP_Text shadow = CopyFrom(playerCanvas, "Timer (1)", group)?.GetComponent<TMP_Text>();
        TMP_Text label = CopyFrom(playerCanvas, "Timer", group)?.GetComponent<TMP_Text>();
        if (shadow != null) shadow.name = "Timer Shadow";
        if (label != null) label.name = "Timer Text";

        HUDTimer timer = group.gameObject.AddComponent<HUDTimer>();
        SerializedObject so = new SerializedObject(timer);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("shadowLabel").objectReferenceValue = shadow;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildAmmo(Transform root, Transform playerCanvas)
    {
        GameObject ammo = CopyFrom(playerCanvas, "Ammo Count", root);
        if (ammo == null) return;

        HUDAmmo hudAmmo = ammo.AddComponent<HUDAmmo>();
        SerializedObject so = new SerializedObject(hudAmmo);
        so.FindProperty("label").objectReferenceValue = ammo.GetComponent<TMP_Text>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildStomp(Transform root, TMP_FontAsset font)
    {
        RectTransform bar = NewRect("Stomp Indicator", root);
        Place(bar, new Vector2(0f, 0f), new Vector2(180f, 70f), new Vector2(240f, 48f));
        bar.gameObject.AddComponent<Canvas>(); // the fill changes every frame while recharging

        RectTransform background = NewRect("Background", bar);
        Stretch(background);
        AddImage(background, Box);

        RectTransform fillRect = NewRect("Fill", bar);
        Stretch(fillRect);
        Image fill = AddImage(fillRect, Accent);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        TextMeshProUGUI label = Label(bar, "Label", "STOMP", 28, Center, Vector2.zero, new Vector2(240f, 48f), Color.black);
        Stretch((RectTransform)label.transform);
        if (font != null) label.font = font;

        // Pips sit on top of the bar, one per possible charge (1 + Chain Stomp's max of 4).
        RectTransform charges = NewRect("Charges", bar);
        charges.anchorMin = new Vector2(0f, 1f);
        charges.anchorMax = new Vector2(1f, 1f);
        charges.pivot = new Vector2(0.5f, 0f);
        charges.anchoredPosition = new Vector2(0f, 6f);
        charges.sizeDelta = new Vector2(0f, 12f);
        HorizontalLayoutGroup layout = charges.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        Image[] pips = new Image[5];
        for (int i = 0; i < pips.Length; i++)
            pips[i] = AddImage(NewRect($"Pip {i + 1}", charges), Color.white);

        StompHUD stomp = bar.gameObject.AddComponent<StompHUD>();
        SerializedObject so = new SerializedObject(stomp);
        so.FindProperty("fill").objectReferenceValue = fill;
        SerializedProperty pipsProp = so.FindProperty("chargePips");
        pipsProp.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++)
            pipsProp.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildHeldCigs(Transform root, GameObject cigPrefab)
    {
        RectTransform panel = NewRect("Held Cigs Panel", root);
        Place(panel, new Vector2(0f, 1f), new Vector2(240f, -90f), new Vector2(420f, 120f));
        HorizontalLayoutGroup layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        HeldCigIconView[] slots = new HeldCigIconView[Pack.MaxSlots];
        for (int i = 0; i < slots.Length; i++)
        {
            GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(cigPrefab, panel);
            slot.name = $"HUD Cig ({i + 1})";
            slots[i] = slot.GetComponent<HeldCigIconView>();
        }

        HeldCigsHUD held = panel.gameObject.AddComponent<HeldCigsHUD>();
        SerializedObject so = new SerializedObject(held);
        so.FindProperty("rarityConfig").objectReferenceValue = FindRarityConfig();
        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Border (rarity tint) > dark backing > cig art.
    private static GameObject BuildCigPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CigPrefabPath);
        if (existing != null) return existing;

        GameObject root = new GameObject("HUD Cig", typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        try
        {
            ((RectTransform)root.transform).sizeDelta = new Vector2(64f, 96f);

            RectTransform borderRect = NewRect("Border", root.transform);
            Stretch(borderRect);
            Image border = AddImage(borderRect, Color.white);

            RectTransform backing = NewRect("Backing", root.transform);
            Inset(backing, 4f);
            AddImage(backing, Box);

            RectTransform iconRect = NewRect("Icon", root.transform);
            Inset(iconRect, 10f);
            Image icon = AddImage(iconRect, Color.white);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            HeldCigIconView view = root.AddComponent<HeldCigIconView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("border").objectReferenceValue = border;
            so.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, CigPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Inset(RectTransform rect, float inset)
    {
        Stretch(rect);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static GameObject CopyFrom(Transform playerCanvas, string name, Transform parent)
    {
        Transform source = playerCanvas.Find(name);
        if (source == null)
        {
            Debug.LogWarning($"[HUDCanvasBuilder] '{name}' not found in the Player Canvas — skipped.");
            return null;
        }

        GameObject copy = Object.Instantiate(source.gameObject, parent, false);
        copy.name = name;
        copy.SetActive(true);
        return copy;
    }

    private static RarityConfig FindRarityConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:RarityConfig");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[HUDCanvasBuilder] No RarityConfig asset — held cig borders will stay white.");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<RarityConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}

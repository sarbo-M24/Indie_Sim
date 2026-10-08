using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using MinMaxCurve = UnityEngine.ParticleSystem.MinMaxCurve;
using MinMaxGradient = UnityEngine.ParticleSystem.MinMaxGradient;

/// <summary>
/// One-click rebuild of Teleporter.prefab's look: moves the SpriteRenderer
/// onto a "Sprite" child (it spins and pulses there, leaving the trigger and
/// FX on the root unscaled) and (re)creates the "FX" child — two orbiting
/// pixel rings, sparks, rising motes, a teleport burst and a 2D light — wired
/// to TeleporterFX. Particles are square pixels (Assets/2d Assets/FX).
/// Tune the particle systems in the prefab afterwards; re-running replaces FX.
/// </summary>
public static class TeleporterFXBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Teleporter.prefab";
    private const string MaterialPath = "Assets/2d Assets/FX/PixelParticle.mat";
    private const string LightSourcePrefab = "Assets/Prefabs/Enemies/Enemy Spawner.prefab";
    private const string UnlitSpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    private const float SpriteDiameter = 1.6f; // world units; the player is ~1
    private const string SortingLayer = "gore"; // above floor + blood, below walls
    private const int SortingOrder = 60;

    private static readonly Color Cyan = new Color(0.35f, 1f, 1f);
    private static readonly Color Ice = new Color(0.55f, 0.8f, 1f);
    private static readonly Color Gold = new Color(1f, 0.85f, 0.35f);

    [MenuItem("Tools/FX/Build Teleporter FX")]
    private static void Build()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogError($"[TeleporterFXBuilder] Missing {MaterialPath}.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform sprite = EnsureSpriteChild(root);
            float radius = SpriteDiameter * 0.5f;

            Transform oldFx = root.transform.Find("FX");
            if (oldFx != null) Object.DestroyImmediate(oldFx.gameObject);
            Transform fxRoot = new GameObject("FX").transform;
            fxRoot.SetParent(root.transform, false);

            ParticleSystem inner = Make(fxRoot, material, "Orbit Inner", true, 45f, 1.2f, 2f, 0.07f, 0.12f, Cyan, Color.white, 300);
            SetShape(inner, radius * 1.05f, 0f);
            Orbit(inner, 3f, -0.08f);

            ParticleSystem outer = Make(fxRoot, material, "Orbit Outer", true, 30f, 2f, 3f, 0.05f, 0.09f, Ice, Cyan, 300);
            SetShape(outer, radius * 1.55f, 0f);
            Orbit(outer, -1.6f, 0.05f);

            ParticleSystem sparks = Make(fxRoot, material, "Sparks", false, 16f, 0.25f, 0.55f, 0.05f, 0.09f, Color.white, Color.white, 300); // colour from Hot()
            SetShape(sparks, radius * 0.95f, 0f);
            ParticleSystem.MainModule sparkMain = sparks.main;
            sparkMain.startSpeed = new MinMaxCurve(2f, 4.5f);
            sparkMain.prewarm = false;
            ParticleSystem.EmissionModule sparkEmission = sparks.emission;
            sparkEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, new MinMaxCurve(4, 10), 0, 0.45f) });
            Drag(sparks, 0.4f, 0.2f);
            ParticleSystem.SizeOverLifetimeModule sparkSize = sparks.sizeOverLifetime;
            sparkSize.enabled = true;
            sparkSize.size = new MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            SetColorOverLifetime(sparks, Hot(0.4f, 0.6f));

            ParticleSystem motes = Make(fxRoot, material, "Motes", false, 12f, 1.2f, 2.2f, 0.05f, 0.08f, Cyan, Ice, 200);
            SetShape(motes, radius * 1.2f, 1f);
            ParticleSystem.VelocityOverLifetimeModule rise = motes.velocityOverLifetime;
            rise.enabled = true;
            rise.space = ParticleSystemSimulationSpace.World;
            rise.x = new MinMaxCurve(-0.1f, 0.1f);
            rise.y = new MinMaxCurve(0.4f, 1f);
            rise.z = new MinMaxCurve(0f, 0f);
            ParticleSystem.NoiseModule noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.8f;

            ParticleSystem burst = Make(fxRoot, material, "Burst", false, 0f, 0.4f, 0.9f, 0.06f, 0.14f, Color.white, Color.white, 500); // colour from Hot()
            ParticleSystem.MainModule burstMain = burst.main;
            burstMain.loop = false;
            burstMain.playOnAwake = false;
            burstMain.prewarm = false;
            burstMain.startSpeed = new MinMaxCurve(3f, 8f);
            SetShape(burst, radius * 0.4f, 1f);
            Drag(burst, 0.5f, 0.12f);
            SetColorOverLifetime(burst, Hot(0.3f, 0.5f));

            Light2D glow = MakeLight(fxRoot);

            TeleporterFX fx = fxRoot.gameObject.AddComponent<TeleporterFX>();
            SerializedObject fxSO = new SerializedObject(fx);
            fxSO.FindProperty("orbitInner").objectReferenceValue = inner;
            fxSO.FindProperty("orbitOuter").objectReferenceValue = outer;
            fxSO.FindProperty("sparks").objectReferenceValue = sparks;
            fxSO.FindProperty("motes").objectReferenceValue = motes;
            fxSO.FindProperty("burst").objectReferenceValue = burst;
            fxSO.FindProperty("glow").objectReferenceValue = glow;
            fxSO.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject teleporterSO = new SerializedObject(root.GetComponent<Teleporter>());
            teleporterSO.FindProperty("visual").objectReferenceValue = sprite;
            teleporterSO.FindProperty("fx").objectReferenceValue = fx;
            teleporterSO.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[TeleporterFXBuilder] Rebuilt Teleporter.prefab FX.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The root's SpriteRenderer moves to a "Sprite" child sized to SpriteDiameter;
    // the root goes back to scale 1 so the trigger radius means world units.
    private static Transform EnsureSpriteChild(GameObject root)
    {
        Transform child = root.transform.Find("Sprite");
        if (child == null)
        {
            child = new GameObject("Sprite").transform;
            child.SetParent(root.transform, false);
        }

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();

        SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            EditorUtility.CopySerialized(rootRenderer, renderer);
            Object.DestroyImmediate(rootRenderer);
        }

        // Unlit: the portal glows on its own instead of sitting in the dungeon's dark.
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterial);
        if (unlit != null) renderer.sharedMaterial = unlit;

        root.transform.localScale = Vector3.one;
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        if (renderer.sprite != null)
        {
            float scale = SpriteDiameter / renderer.sprite.bounds.size.x;
            child.localScale = new Vector3(scale, scale, 1f);
        }
        return child;
    }

    private static ParticleSystem Make(Transform parent, Material material, string name, bool local, float rate,
        float lifeMin, float lifeMax, float sizeMin, float sizeMax, Color colorA, Color colorB, int maxParticles)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.duration = 5f;
        main.startLifetime = new MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new MinMaxCurve(0f);
        main.startSize = new MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new MinMaxGradient(colorA, colorB);
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;

        SetColorOverLifetime(ps, FadeInOut());

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = SortingLayer;
        renderer.sortingOrder = SortingOrder;
        return ps;
    }

    // Circle edge (thickness 0) or filled disc (thickness 1); particles head outward.
    private static void SetShape(ParticleSystem ps, float radius, float thickness)
    {
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = thickness;
    }

    private static void Orbit(ParticleSystem ps, float orbitalZ, float radial)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new MinMaxCurve(0f);
        velocity.y = new MinMaxCurve(0f);
        velocity.z = new MinMaxCurve(0f);
        velocity.orbitalX = new MinMaxCurve(0f);
        velocity.orbitalY = new MinMaxCurve(0f);
        velocity.orbitalZ = new MinMaxCurve(orbitalZ);
        velocity.radial = new MinMaxCurve(radial);
    }

    private static void Drag(ParticleSystem ps, float limit, float dampen)
    {
        ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = new MinMaxCurve(limit);
        drag.dampen = dampen;
    }

    private static void SetColorOverLifetime(ParticleSystem ps, Gradient gradient)
    {
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new MinMaxGradient(gradient);
    }

    private static Gradient FadeInOut()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // White → gold → cyan, fading out after `holdUntil`.
    private static Gradient Hot(float goldAt, float holdUntil)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Gold, goldAt), new GradientColorKey(Cyan, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // Copies the spawner's Light2D so its 2D-renderer settings (target sorting
    // layers, blend style) match the rest of the dungeon, then recolours it.
    private static Light2D MakeLight(Transform parent)
    {
        GameObject go = new GameObject("Glow");
        go.transform.SetParent(parent, false);
        Light2D light = go.AddComponent<Light2D>();

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(LightSourcePrefab);
        Light2D sourceLight = source != null ? source.GetComponent<Light2D>() : null;
        if (sourceLight != null) EditorUtility.CopySerialized(sourceLight, light);

        light.color = new Color(0.3f, 0.95f, 1f);
        light.intensity = 1.2f;
        light.pointLightInnerRadius = 0.6f;
        light.pointLightOuterRadius = 4f;

        SerializedObject so = new SerializedObject(light);
        so.FindProperty("m_LightCookieSprite").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        return light;
    }
}

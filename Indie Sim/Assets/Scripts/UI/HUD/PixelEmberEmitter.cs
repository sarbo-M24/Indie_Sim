using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pixel-art burning tip for UI: a flickering cherry plus embers that rise
/// and ash that flakes off and falls. Drawn as its own Graphic (square,
/// grid-snapped quads in a fixed palette) because a ParticleSystem doesn't
/// render on a Screen Space - Overlay canvas. "Up" is screen-up whatever the
/// parent's rotation, so it works on the rotated cig sliders. Rebuilds its
/// mesh every frame — keep it under a nested Canvas.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PixelEmberEmitter : MaskableGraphic
{
    [Header("Look")]
    [Tooltip("Size of one art pixel, in canvas units. Positions snap to this grid.")]
    [SerializeField] private float pixelSize = 4f;
    [SerializeField] private Color[] emberPalette =
    {
        new Color32(255, 243, 160, 255),
        new Color32(255, 194, 61, 255),
        new Color32(255, 122, 31, 255),
        new Color32(224, 48, 30, 255),
        new Color32(122, 24, 20, 255),
    };
    [SerializeField] private Color[] ashPalette =
    {
        new Color32(200, 200, 200, 255),
        new Color32(138, 138, 138, 255),
        new Color32(94, 94, 94, 255),
    };

    [Header("Cherry (the glowing tip)")]
    [Tooltip("Half-width of the glowing tip, in art pixels.")]
    [SerializeField] private int cherryHalfWidth = 2;
    [SerializeField] private float cherryFlickerInterval = 0.08f;

    [Header("Emission")]
    [SerializeField] private int maxParticles = 48;
    [SerializeField] private float emberRate = 12f;
    [SerializeField] private float ashRate = 5f;
    [Tooltip("Spawn spread across the tip, in art pixels.")]
    [SerializeField] private float spawnSpread = 2f;

    [Header("Motion (canvas units / second)")]
    [SerializeField] private Vector2 emberRiseSpeed = new Vector2(30f, 70f);
    [SerializeField] private Vector2 emberLifetime = new Vector2(0.35f, 0.9f);
    [SerializeField] private float emberWobble = 25f;
    [SerializeField] private Vector2 ashLifetime = new Vector2(0.8f, 1.6f);
    [SerializeField] private float ashGravity = 60f;
    [SerializeField] private float ashDrift = 20f;

    private struct Particle
    {
        public Vector2 Position; // screen-aligned offset from the tip
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public bool IsAsh;
        public float Seed;
    }

    private Particle[] _particles;
    private int _count;
    private float _emberAccumulator;
    private float _ashAccumulator;
    private float _flickerTimer;
    private int _flickerSeed;

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        _count = 0;
        _emberAccumulator = 0f;
        _ashAccumulator = 0f;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (_particles == null || _particles.Length != maxParticles)
        {
            _particles = new Particle[Mathf.Max(1, maxParticles)];
            _count = 0;
        }

        Simulate(dt);

        _emberAccumulator += emberRate * dt;
        while (_emberAccumulator >= 1f) { _emberAccumulator -= 1f; Spawn(false); }
        _ashAccumulator += ashRate * dt;
        while (_ashAccumulator >= 1f) { _ashAccumulator -= 1f; Spawn(true); }

        _flickerTimer -= dt;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = cherryFlickerInterval;
            _flickerSeed = Random.Range(0, 1000);
        }

        SetVerticesDirty();
    }

    private void Spawn(bool ash)
    {
        if (_count >= _particles.Length) return;

        float spread = spawnSpread * pixelSize;
        Particle p = new Particle
        {
            Position = new Vector2(Random.Range(-spread, spread), Random.Range(0f, pixelSize)),
            IsAsh = ash,
            Seed = Random.value * 100f,
        };

        if (ash)
        {
            p.Velocity = new Vector2(Random.Range(-ashDrift, ashDrift), Random.Range(10f, 35f)); // pops up, then falls
            p.Lifetime = Random.Range(ashLifetime.x, ashLifetime.y);
        }
        else
        {
            p.Velocity = new Vector2(Random.Range(-emberWobble, emberWobble) * 0.5f, Random.Range(emberRiseSpeed.x, emberRiseSpeed.y));
            p.Lifetime = Random.Range(emberLifetime.x, emberLifetime.y);
        }

        _particles[_count++] = p;
    }

    private void Simulate(float dt)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            Particle p = _particles[i];
            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                _particles[i] = _particles[--_count];
                continue;
            }

            if (p.IsAsh)
            {
                p.Velocity.y -= ashGravity * dt;
                p.Velocity.x += Mathf.Sin((p.Age + p.Seed) * 6f) * ashDrift * dt;
            }
            else
            {
                p.Velocity.x = Mathf.Sin((p.Age + p.Seed) * 9f) * emberWobble;
            }

            p.Position += p.Velocity * dt;
            _particles[i] = p;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (pixelSize <= 0f) return;

        // Simulation runs in screen-aligned axes; map them into this rect's
        // local space so a rotated parent doesn't rotate the smoke.
        Vector2 up = ((Vector2)rectTransform.InverseTransformDirection(Vector3.up)).normalized;
        Vector2 right = ((Vector2)rectTransform.InverseTransformDirection(Vector3.right)).normalized;
        if (up == Vector2.zero) up = Vector2.up;
        if (right == Vector2.zero) right = Vector2.right;

        DrawCherry(vh, up, right);

        if (_particles == null) return;
        for (int i = 0; i < _count; i++)
        {
            Particle p = _particles[i];
            float t = p.Age / p.Lifetime;
            Color c = p.IsAsh ? Pick(ashPalette, t) : Pick(emberPalette, t);
            DrawPixel(vh, Snap(p.Position), up, right, c);
        }
    }

    // A small block of tip pixels, recoloured on each flicker tick.
    private void DrawCherry(VertexHelper vh, Vector2 up, Vector2 right)
    {
        if (emberPalette == null || emberPalette.Length == 0) return;
        int hot = Mathf.Min(3, emberPalette.Length);

        for (int x = -cherryHalfWidth; x <= cherryHalfWidth; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                int hash = (x * 73 + y * 151 + _flickerSeed) & 0xFF;
                if (y == 1 && Mathf.Abs(x) == cherryHalfWidth && hash < 128) continue; // ragged top corners
                Color c = emberPalette[Mathf.Abs(x) == cherryHalfWidth ? Mathf.Min(hot, emberPalette.Length - 1) : hash % hot];
                DrawPixel(vh, new Vector2(x * pixelSize, y * pixelSize), up, right, c);
            }
        }
    }

    private Vector2 Snap(Vector2 position) =>
        new Vector2(Mathf.Round(position.x / pixelSize) * pixelSize, Mathf.Round(position.y / pixelSize) * pixelSize);

    private static Color Pick(Color[] palette, float t)
    {
        if (palette == null || palette.Length == 0) return Color.white;
        return palette[Mathf.Clamp(Mathf.FloorToInt(t * palette.Length), 0, palette.Length - 1)];
    }

    private void DrawPixel(VertexHelper vh, Vector2 screenOffset, Vector2 up, Vector2 right, Color c)
    {
        Vector2 center = right * screenOffset.x + up * screenOffset.y;
        Vector2 hx = right * (pixelSize * 0.5f);
        Vector2 hy = up * (pixelSize * 0.5f);
        Color32 vc = c * color;

        int start = vh.currentVertCount;
        vh.AddVert(center - hx - hy, vc, Vector4.zero);
        vh.AddVert(center - hx + hy, vc, Vector4.zero);
        vh.AddVert(center + hx + hy, vc, Vector4.zero);
        vh.AddVert(center + hx - hy, vc, Vector4.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}

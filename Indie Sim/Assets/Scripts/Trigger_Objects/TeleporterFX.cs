using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The teleporter's particles and light, driven by Teleporter:
///   idle     — pixels circling in two rings, sparks spitting off the edge,
///              motes drifting up, a pulsing glow;
///   charged  — the player is in range: everything emits and orbits faster
///              and the glow brightens (SetCharged);
///   teleport — a big burst and a flash while the teleport delay runs
///              (PlayTeleport).
/// The particle systems are plain children of the prefab's FX object, so
/// their look is tuned in the Inspector; this only scales their rates.
/// </summary>
public class TeleporterFX : MonoBehaviour
{
    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem orbitInner;
    [SerializeField] private ParticleSystem orbitOuter;
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private ParticleSystem motes;
    [Tooltip("Emitted all at once when the teleport starts.")]
    [SerializeField] private ParticleSystem burst;
    [SerializeField, Min(0)] private int burstCount = 120;

    [Header("Light")]
    [SerializeField] private Light2D glow;
    [SerializeField] private float glowIdle = 1.2f;
    [SerializeField] private float glowCharged = 2.5f;
    [SerializeField] private float glowTeleport = 6f;
    [SerializeField] private float glowFlicker = 0.25f;

    [Header("Charge")]
    [Tooltip("Emission × this at full charge (player in range).")]
    [SerializeField] private float chargedEmission = 3f;
    [Tooltip("Particle simulation speed at full charge — the rings spin faster.")]
    [SerializeField] private float chargedSimSpeed = 2.2f;
    [Tooltip("Charge gained / lost per second.")]
    [SerializeField] private float chargeRate = 3f;

    private ParticleSystem[] _looping;
    private float[] _baseRates;
    private float _charge;
    private float _chargeTarget;
    private float _extraCharge; // pushed past 1 during the teleport
    private float _glowBoost;

    private void Awake()
    {
        _looping = new[] { orbitInner, orbitOuter, sparks, motes };
        _baseRates = new float[_looping.Length];
        for (int i = 0; i < _looping.Length; i++)
            if (_looping[i] != null) _baseRates[i] = _looping[i].emission.rateOverTimeMultiplier;
    }

    /// <summary>Player in range (true) or gone (false).</summary>
    public void SetCharged(bool charged) => _chargeTarget = charged ? 1f : 0f;

    /// <summary>The teleport has started; `duration` is its delay before the level changes.</summary>
    public void PlayTeleport(float duration)
    {
        if (burst != null) burst.Emit(burstCount);
        StartCoroutine(TeleportSurge(duration));
    }

    private IEnumerator TeleportSurge(float duration)
    {
        _chargeTarget = 1f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / Mathf.Max(duration, 0.01f);
            _extraCharge = k;           // up to double the charged rates
            _glowBoost = k;
            yield return null;
        }
        _extraCharge = 0f;
        _glowBoost = 0f;
    }

    private void Update()
    {
        _charge = Mathf.MoveTowards(_charge, _chargeTarget, chargeRate * Time.deltaTime);
        float level = _charge + _extraCharge;

        float emission = Mathf.LerpUnclamped(1f, chargedEmission, level);
        float simSpeed = Mathf.LerpUnclamped(1f, chargedSimSpeed, level);
        for (int i = 0; i < _looping.Length; i++)
        {
            ParticleSystem ps = _looping[i];
            if (ps == null) continue;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTimeMultiplier = _baseRates[i] * emission;
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpeed = simSpeed;
        }

        if (glow != null)
        {
            float baseGlow = Mathf.Lerp(glowIdle, glowCharged, _charge);
            baseGlow = Mathf.Lerp(baseGlow, glowTeleport, _glowBoost);
            float flicker = 1f + (Mathf.PerlinNoise(Time.time * 6f, 0.37f) - 0.5f) * 2f * glowFlicker;
            glow.intensity = baseGlow * flicker;
        }
    }
}

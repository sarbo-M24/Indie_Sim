using UnityEngine;

public class ManualGoreCollisions : MonoBehaviour
{
    private ChunkedGorePainter painter;
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
        painter = Object.FindAnyObjectByType<ChunkedGorePainter>();

        // FORCE the particle system to always simulate so far-away kills work
        var main = ps.main;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
    }

    void LateUpdate()
    {
        if (painter == null) return;

        int numParticlesAlive = ps.GetParticles(particles);
        bool simulationIsWorld = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;

        // Paint a little before death. Checking against exactly one deltaTime
        // missed particles whenever the next frame ran longer than this one —
        // they died inside the simulation before ever being painted.
        float paintWindow = Mathf.Max(Time.deltaTime * 2f, 0.05f);

        for (int i = 0; i < numParticlesAlive; i++)
        {
            if (particles[i].remainingLifetime <= paintWindow)
            {
                Vector3 pos = simulationIsWorld ? particles[i].position : transform.TransformPoint(particles[i].position);
                pos.z = 0;
                
                painter.PaintSplat(pos);
                
                // To prevent the "double stamp" or flickering, 
                // we kill the particle immediately after painting
                particles[i].remainingLifetime = -1f; 
            }
        }
        
        // Apply the "immediate death" back to the system to remove the gap
        ps.SetParticles(particles, numParticlesAlive);
    }
}
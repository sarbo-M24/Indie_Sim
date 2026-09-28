using UnityEngine;
using System.Collections;

public class MusicManager : MonoBehaviour
{

    public static MusicManager Instance;

    [Header("Player Reference")]
    [SerializeField] private Transform player;
    
    [Header("Enemy Detection")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float detectionRadius = 20f;
    
    [Header("Ambient Tracks")]
    [SerializeField] private AudioClip[] ambientTracks;
    [SerializeField] [Range(0f, 1f)] private float ambientMinVolume = 0.01f;
    [SerializeField] [Range(0f, 1f)] private float ambientMaxVolume = 1f;
    
    [Header("Power Tracks")]
    [SerializeField] private AudioClip[] powerTracks;
    [SerializeField] [Range(0f, 1f)] private float powerMinVolume = 0.01f;
    [SerializeField] [Range(0f, 1f)] private float powerMaxVolume = 1f;
    [SerializeField] private float powerTrackLoopStartTime = 6f;
    
    [Header("Teleporter Sounds")]
    [SerializeField] private Transform teleporterTransform;
    [SerializeField] private float teleporterDetectionRadius = 10f;
    [SerializeField] private AudioClip teleporterAmbientSound;
    [SerializeField] [Range(0f, 1f)] private float teleporterAmbientVolume = 1f;
    [SerializeField] private AudioClip teleporterActivateSound;
    [SerializeField] [Range(0f, 1f)] private float teleporterActivateVolume = 1f;
    
    [Header("Death Sound")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField] [Range(0f, 1f)] private float deathSoundVolume = 1f;
    
    [Header("Transition Settings")]
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private float musicFadeOutSpeed = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebug = true;
    
    private AudioSource ambientSource;
    private AudioSource powerSource;
    private AudioSource teleporterAmbientSource;
    private AudioSource teleporterActivateSource;
    private AudioSource deathSource;
    
    private float currentAmbientVolume;
    private float currentPowerVolume;
    private float targetAmbientVolume;
    private float targetPowerVolume;
    private float currentTeleporterVolume;
    private float targetTeleporterVolume;
    
    private bool isPlayerDead = false;
    private bool isTeleporting = false;
    


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        InitializeAudioSources();
        
        // Start playing tracks if available
        if (ambientTracks.Length > 0)
        {
            PlayAmbientTrack(0);
            currentAmbientVolume = ambientMaxVolume;
            targetAmbientVolume = ambientMaxVolume;
        }
        
        if (powerTracks.Length > 0)
        {
            PlayPowerTrack(0);
            currentPowerVolume = powerMinVolume;
            targetPowerVolume = powerMinVolume;
        }
    }
    
    void InitializeAudioSources()
    {
        // Create ambient audio source
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = false; // Manual looping for custom start time
        ambientSource.playOnAwake = false;
        ambientSource.volume = ambientMaxVolume;
        
        // Create power audio source
        powerSource = gameObject.AddComponent<AudioSource>();
        powerSource.loop = false; // Manual looping for custom start time
        powerSource.playOnAwake = false;
        powerSource.volume = powerMinVolume;
        
        // Create teleporter ambient audio source
        teleporterAmbientSource = gameObject.AddComponent<AudioSource>();
        teleporterAmbientSource.loop = true;
        teleporterAmbientSource.playOnAwake = false;
        teleporterAmbientSource.volume = 0f;
        
        // Start playing teleporter ambient if clip is assigned
        if (teleporterAmbientSound != null)
        {
            teleporterAmbientSource.clip = teleporterAmbientSound;
            teleporterAmbientSource.Play();
        }
        
        // Create teleporter activate audio source
        teleporterActivateSource = gameObject.AddComponent<AudioSource>();
        teleporterActivateSource.loop = false;
        teleporterActivateSource.playOnAwake = false;
        
        // Create death audio source
        deathSource = gameObject.AddComponent<AudioSource>();
        deathSource.loop = false;
        deathSource.playOnAwake = false;

        // Music and SFX volume sliders (null until MainMixer exists — plays unrouted, as before).
        ambientSource.outputAudioMixerGroup = AudioManager.MusicGroup;
        powerSource.outputAudioMixerGroup = AudioManager.MusicGroup;
        teleporterAmbientSource.outputAudioMixerGroup = AudioManager.SfxGroup;
        teleporterActivateSource.outputAudioMixerGroup = AudioManager.SfxGroup;
        deathSource.outputAudioMixerGroup = AudioManager.SfxGroup;
    }
    
    void Update()
    {
        if (isPlayerDead) return;
        
        if (player == null) return;
        
        // Check if power track needs to loop
        if (!powerSource.isPlaying && powerTracks.Length > 0)
        {
            LoopPowerTrack();
        }
        
        // Check if ambient track needs to loop
        if (!ambientSource.isPlaying && ambientTracks.Length > 0)
        {
            LoopAmbientTrack();
        }
        
        // Check teleporter proximity and visibility
        bool teleporterVisible = false;
        float distanceToTeleporter = 0f;
        
        if (teleporterTransform != null && !isTeleporting)
        {
            distanceToTeleporter = Vector2.Distance(player.position, teleporterTransform.position);
            
            if (distanceToTeleporter <= teleporterDetectionRadius)
            {
                // Check line of sight to teleporter
                Vector2 directionToTeleporter = teleporterTransform.position - player.position;
                RaycastHit2D hit = Physics2D.Raycast(player.position, directionToTeleporter.normalized, distanceToTeleporter, wallLayer);
                
                if (hit.collider == null)
                {
                    teleporterVisible = true;
                }
            }
        }
        
        // Set teleporter volume based on visibility and distance
        if (teleporterVisible)
        {
            float intensity = 1f - (distanceToTeleporter / teleporterDetectionRadius);
            targetTeleporterVolume = Mathf.Lerp(0f, teleporterAmbientVolume, intensity);
        }
        else
        {
            targetTeleporterVolume = 0f;
        }
        
        // Smoothly transition teleporter volume
        currentTeleporterVolume = Mathf.Lerp(currentTeleporterVolume, targetTeleporterVolume, fadeSpeed * Time.deltaTime);
        teleporterAmbientSource.volume = currentTeleporterVolume;
        
        // Detect enemies in range using 2D physics
        float closestEnemyDistance = detectionRadius;
        bool enemyVisible = false;
        int visibleEnemyCount = 0;
        
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(player.position, detectionRadius, enemyLayer);
        
        if (enemiesInRange.Length > 0)
        {
            // Check line of sight for each enemy
            foreach (Collider2D enemy in enemiesInRange)
            {
                Vector2 directionToEnemy = enemy.transform.position - player.position;
                float distanceToEnemy = directionToEnemy.magnitude;
                
                // Raycast to check if there's a wall between player and enemy (2D)
                RaycastHit2D hit = Physics2D.Raycast(player.position, directionToEnemy.normalized, distanceToEnemy, wallLayer);
                
                if (hit.collider == null)
                {
                    // Enemy is visible (no wall blocking)
                    enemyVisible = true;
                    visibleEnemyCount++;
                    
                    if (distanceToEnemy < closestEnemyDistance)
                    {
                        closestEnemyDistance = distanceToEnemy;
                    }
                }
            }
        }
        
        if (showDebug)
        {
            Debug.Log($"Enemies in range: {enemiesInRange.Length}, Visible: {visibleEnemyCount}, Closest distance: {closestEnemyDistance:F2}");
        }
        
        if (enemyVisible)
        {
            // Calculate intensity based on closest visible enemy (0 = far, 1 = close)
            float intensity = 1f - (closestEnemyDistance / detectionRadius);
            
            // Set target volumes based on intensity
            targetAmbientVolume = Mathf.Lerp(ambientMaxVolume, ambientMinVolume, intensity);
            targetPowerVolume = Mathf.Lerp(powerMinVolume, powerMaxVolume, intensity);
            
            if (showDebug)
            {
                Debug.Log($"Intensity: {intensity:F2}, Target Ambient: {targetAmbientVolume:F2}, Target Power: {targetPowerVolume:F2}");
            }
        }
        else
        {
            // No enemies visible, return to ambient
            targetAmbientVolume = ambientMaxVolume;
            targetPowerVolume = powerMinVolume;
        }
        
        // Smoothly transition volumes
        currentAmbientVolume = Mathf.Lerp(currentAmbientVolume, targetAmbientVolume, fadeSpeed * Time.deltaTime);
        currentPowerVolume = Mathf.Lerp(currentPowerVolume, targetPowerVolume, fadeSpeed * Time.deltaTime);
        
        ambientSource.volume = currentAmbientVolume;
        powerSource.volume = currentPowerVolume;
    }
    
    void LoopPowerTrack()
    {
        if (powerSource.clip != null)
        {
            powerSource.time = powerTrackLoopStartTime;
            powerSource.Play();
        }
    }
    
    void LoopAmbientTrack()
    {
        if (ambientSource.clip != null)
        {
            ambientSource.time = 0f;
            ambientSource.Play();
        }
    }
    
    // Public methods to change tracks
    public void PlayAmbientTrack(int index)
    {
        if (index >= 0 && index < ambientTracks.Length)
        {
            ambientSource.clip = ambientTracks[index];
            ambientSource.time = 0f;
            ambientSource.Play();
        }
    }
    
    public void PlayPowerTrack(int index)
    {
        if (index >= 0 && index < powerTracks.Length)
        {
            powerSource.clip = powerTracks[index];
            powerSource.time = powerTrackLoopStartTime;
            powerSource.Play();
        }
    }
    
    // Teleporter methods - call these from your teleporter script
    public void PlayTeleporterActivate()
    {
        isTeleporting = true;
        
        // Play teleporter activation sound
        if (teleporterActivateSound != null)
        {
            teleporterActivateSource.clip = teleporterActivateSound;
            teleporterActivateSource.volume = teleporterActivateVolume;
            teleporterActivateSource.Play();
        }
        
        // Fade out all music during teleportation
        StartCoroutine(FadeOutMusic());
    }
    
    public void StopTeleporter()
    {
        isTeleporting = false;
        
        // Resume music after teleportation
        StartCoroutine(FadeInMusic());
    }
    
    // Call this method from your player controller when player dies
    public void OnPlayerDeath()
    {
        isPlayerDead = true;
        
        // Stop all music
        StartCoroutine(FadeOutMusic());
        
        // Play death sound
        if (deathSound != null)
        {
            deathSource.clip = deathSound;
            deathSource.volume = deathSoundVolume;
            deathSource.Play();
        }
    }
    
    private IEnumerator FadeOutMusic()
    {
        float startAmbientVol = ambientSource.volume;
        float startPowerVol = powerSource.volume;
        float elapsed = 0f;
        
        while (elapsed < 1f / musicFadeOutSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed * musicFadeOutSpeed;
            
            ambientSource.volume = Mathf.Lerp(startAmbientVol, 0f, t);
            powerSource.volume = Mathf.Lerp(startPowerVol, 0f, t);
            
            yield return null;
        }
        
        ambientSource.volume = 0f;
        powerSource.volume = 0f;
        ambientSource.Stop();
        powerSource.Stop();
    }
    
    private IEnumerator FadeInMusic()
    {
        ambientSource.Play();
        powerSource.Play();
        
        float elapsed = 0f;
        
        while (elapsed < 1f / musicFadeOutSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed * musicFadeOutSpeed;
            
            ambientSource.volume = Mathf.Lerp(0f, currentAmbientVolume, t);
            powerSource.volume = Mathf.Lerp(0f, currentPowerVolume, t);
            
            yield return null;
        }
        
        ambientSource.volume = currentAmbientVolume;
        powerSource.volume = currentPowerVolume;
    }
    
    // Visualize detection radius in editor
    void OnDrawGizmosSelected()
    {
        if (player != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, detectionRadius);
        }
        
        if (teleporterTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(teleporterTransform.position, teleporterDetectionRadius);
        }
    }
}
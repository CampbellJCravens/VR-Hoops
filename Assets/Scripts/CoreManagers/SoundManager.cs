using UnityEngine;

/// <summary>
/// Centralized sound manager for all game audio clips and background music.
/// Attach this to a GameObject in the scene called "SoundManager".
/// All scripts that need to play sounds will reference this manager.
/// </summary>
public class SoundManager : MonoBehaviour
{
    private static SoundManager s_Instance;

    [Header("Ball Sounds")]
    [Tooltip("Sound played when a ball is released from the player's hands.")]
    public AudioClip ballShot;
    
    [Tooltip("Volume for ball shot sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float ballShotVolume = 1.0f;
    
    [Tooltip("Sound played when a ball bounces on the ground or other surfaces.")]
    public AudioClip ballBounce;
    
    [Tooltip("Volume for ball bounce sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float ballBounceVolume = 1.0f;
    
    [Header("Machine Sounds")]
    [Tooltip("Sound played when a ball is launched from the ball machine.")]
    public AudioClip ballMachine;
    
    [Tooltip("Volume for ball machine sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float ballMachineVolume = 1.0f;
    
    [Header("Gameplay Sounds")]
    [Tooltip("Sound played when a basket is made (swish).")]
    public AudioClip swish;
    
    [Tooltip("Volume for swish sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float swishVolume = 1.0f;
    
    [Tooltip("Sound played when a basketball collides with the rim.")]
    public AudioClip rimHit;
    
    [Tooltip("Volume for rim hit sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float rimHitVolume = 1.0f;
    
    [Tooltip("Sound played when a basketball collides with the backboard.")]
    public AudioClip backboardHit;
    
    [Tooltip("Volume for backboard hit sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float backboardHitVolume = 1.0f;
    
    [Tooltip("Sound played when a basketball collides with a wall piece in the PlayArea.")]
    public AudioClip wallHit;
    
    [Tooltip("Volume for wall hit sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float wallHitVolume = 1.0f;
    
    [Tooltip("Sound played when the game enters the game over state.")]
    public AudioClip gameOver;
    
    [Tooltip("Volume for game over sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float gameOverVolume = 1.0f;
    
    [Tooltip("Sound played when a life is lost.")]
    public AudioClip loseLife;
    
    [Tooltip("Volume for lose life sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float loseLifeVolume = 1.0f;
    
    [Tooltip("Sound played when a life is gained.")]
    public AudioClip gainLife;
    
    [Tooltip("Volume for gain life sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float gainLifeVolume = 1.0f;
    
    [Tooltip("Sound played on the rim when entering the fire state.")]
    public AudioClip rimFireActivate;
    
    [Tooltip("Volume for rim fire activate sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float rimFireActivateVolume = 1.0f;
    
    [Tooltip("Persistent sound played on the ball while in fire state.")]
    public AudioClip ballFireLoop;
    
    [Tooltip("Volume for ball fire loop sound. Values > 1.0 will amplify the sound beyond its original volume.")]
    [Range(0f, 3f)]
    public float ballFireLoopVolume = 1.0f;
    
    [Header("Background Music")]
    [Tooltip("Background music tracks that play continuously, cycling through all songs.")]
    public AudioClip[] backgroundMusicTracks;
    
    [Tooltip("Volume for background music (0.0 to 1.0).")]
    [Range(0f, 1f)]
    public float backgroundMusicVolume = 0.5f;
    
    [Tooltip("Enable 3D spatial audio for background music. If disabled, music will play as 2D (non-spatial) sound.")]
    public bool useSpatialAudioForMusic = true;
    
    [Header("Debug")]
    [Tooltip("When enabled, mutes all sound effects that originate from within a PlayArea owned by the local client. Useful for testing spatial audio from other clients' play areas.")]
    public bool muteInPlayArea = false;

    private AudioSource m_BackgroundMusicSource;
    private int m_CurrentTrackIndex = 0;

    /// <summary>
    /// Gets the singleton instance of the SoundManager.
    /// </summary>
    public static SoundManager Instance
    {
        get
        {
            // Try to find existing instance in scene
            s_Instance = FindFirstObjectByType<SoundManager>();
            return s_Instance;
        }
    }

    private void Awake()
    {
        // Ensure singleton pattern
        s_Instance = this;
        
        // Setup background music AudioSource
        SetupBackgroundMusic();
    }

    private void Start()
    {
        // Randomly select a track when joining and start playing
        InitializeMusicWithRandomTrack();
    }
    
    /// <summary>
    /// Initializes music playback with a randomly selected track.
    /// </summary>
    private void InitializeMusicWithRandomTrack()
    {
        // Count valid tracks
        int validTrackCount = 0;
        foreach (var track in backgroundMusicTracks)
        {
            validTrackCount++;
        }
        
        // Randomly select a valid track
        int randomIndex = Random.Range(0, backgroundMusicTracks.Length);
        int attempts = 0;
        while (attempts < backgroundMusicTracks.Length)
        {
            randomIndex = (randomIndex + 1) % backgroundMusicTracks.Length;
            attempts++;
        }
        
        m_CurrentTrackIndex = randomIndex;
        PlayBackgroundMusic();
    }

    private void Update()
    {
        // Check if current track has finished and play next one
        if (backgroundMusicTracks.Length > 1)
        {
            // If not playing and we have tracks, start the next one
            if (!m_BackgroundMusicSource.isPlaying)
            {
                PlayNextTrack();
            }
        }
        
        // Update spatial settings if they changed in the Inspector (for runtime editing)
        float expectedSpatialBlend = useSpatialAudioForMusic ? 1.0f : 0.0f;
        if (Mathf.Abs(m_BackgroundMusicSource.spatialBlend - expectedSpatialBlend) > 0.01f)
        {
            UpdateMusicSpatialSettings();
        }
    }

    private void OnDestroy()
    {
        if (s_Instance == this)
        {
            s_Instance = null;
        }
    }

    /// <summary>
    /// Sets up the AudioSource for background music with configurable spatial audio settings.
    /// </summary>
    private void SetupBackgroundMusic()
    {
        // Create a dedicated AudioSource for background music
        m_BackgroundMusicSource = gameObject.AddComponent<AudioSource>();
        m_BackgroundMusicSource.playOnAwake = false;
        m_BackgroundMusicSource.loop = false; // Don't loop individual tracks - we'll cycle through the array
        m_BackgroundMusicSource.volume = backgroundMusicVolume;
        
        // Configure spatial audio based on toggle
        UpdateMusicSpatialSettings();
    }

    /// <summary>
    /// Updates the background music AudioSource spatial settings based on the useSpatialAudioForMusic toggle.
    /// </summary>
    private void UpdateMusicSpatialSettings()
    {

        if (useSpatialAudioForMusic)
        {
            // Configure for 3D spatial audio (diagetic world sound)
            m_BackgroundMusicSource.spatialBlend = 1.0f; // Full 3D sound
            m_BackgroundMusicSource.rolloffMode = AudioRolloffMode.Logarithmic; // Realistic distance falloff
            m_BackgroundMusicSource.minDistance = 5f; // Start fading at 5 units
            m_BackgroundMusicSource.maxDistance = 100f; // Fully faded at 100 units
            m_BackgroundMusicSource.dopplerLevel = 0f; // No doppler effect for music
        }
        else
        {
            // Configure for 2D audio (non-spatial, plays at same volume regardless of distance)
            m_BackgroundMusicSource.spatialBlend = 0.0f; // Full 2D sound
            m_BackgroundMusicSource.rolloffMode = AudioRolloffMode.Logarithmic; // Not used for 2D, but set anyway
            m_BackgroundMusicSource.minDistance = 1f; // Not used for 2D
            m_BackgroundMusicSource.maxDistance = 500f; // Not used for 2D
            m_BackgroundMusicSource.dopplerLevel = 0f; // No doppler effect
        }
    }

    /// <summary>
    /// Starts playing the background music from the current track.
    /// </summary>
    public void PlayBackgroundMusic()
    {
        // If already playing, don't restart
        if (m_BackgroundMusicSource.isPlaying)
        {
            return;
        }

        // Find the first valid track if current one is null
        if (m_CurrentTrackIndex < 0 || m_CurrentTrackIndex >= backgroundMusicTracks.Length)
        {
            for (int i = 0; i < backgroundMusicTracks.Length; i++)
            {
                m_CurrentTrackIndex = i;
                break;
            }
        }

        // Play the current track
        if (m_CurrentTrackIndex >= 0 && m_CurrentTrackIndex < backgroundMusicTracks.Length)
        {
            m_BackgroundMusicSource.clip = backgroundMusicTracks[m_CurrentTrackIndex];
            m_BackgroundMusicSource.volume = backgroundMusicVolume;
            m_BackgroundMusicSource.Play();
            Debug.Log($"[SoundManager] Started playing background music track {m_CurrentTrackIndex + 1}/{backgroundMusicTracks.Length}: {backgroundMusicTracks[m_CurrentTrackIndex].name}", this);
        }
    }

    /// <summary>
    /// Plays the next track in the background music array, cycling back to the first when reaching the end.
    /// </summary>
    private void PlayNextTrack()
    {
        // Move to next track index, cycling back to 0
        m_CurrentTrackIndex = (m_CurrentTrackIndex + 1) % backgroundMusicTracks.Length;

        // Find next valid track if current one is null
        int attempts = 0;
        while (attempts < backgroundMusicTracks.Length)
        {
            m_CurrentTrackIndex = (m_CurrentTrackIndex + 1) % backgroundMusicTracks.Length;
            attempts++;
        }

        // Play the track
        m_BackgroundMusicSource.clip = backgroundMusicTracks[m_CurrentTrackIndex];
        m_BackgroundMusicSource.volume = backgroundMusicVolume;
        m_BackgroundMusicSource.Play();
    }

    /// <summary>
    /// Stops playing the background music.
    /// </summary>
    public void StopBackgroundMusic()
    {
        if (m_BackgroundMusicSource.isPlaying)
        {
            m_BackgroundMusicSource.Stop();
        }
    }

    /// <summary>
    /// Sets the volume of the background music.
    /// </summary>
    public void SetBackgroundMusicVolume(float volume)
    {
        backgroundMusicVolume = Mathf.Clamp01(volume);
        m_BackgroundMusicSource.volume = backgroundMusicVolume;
    }

    /// <summary>
    /// Skips to the next track in the background music playlist.
    /// </summary>
    public void SkipToNextTrack()
    {
        if (m_BackgroundMusicSource.isPlaying)
        {
            m_BackgroundMusicSource.Stop();
        }
        PlayNextTrack();
    }

    /// <summary>
    /// Checks if a position is within a PlayArea owned by the local client.
    /// </summary>
    /// <param name="position">The world position to check.</param>
    /// <returns>True if the position is within a local client's PlayArea, false otherwise.</returns>
    private static bool IsPositionInLocalPlayArea(Vector3 position)
    {
        // Auto-finding is disabled. PlayAreas should be assigned via reference.
        // For now, return false as we cannot find PlayAreas without assignment
        return false;
        
        // Original auto-finding code removed - requires assignment
        /* PlayAreaManager[] allPlayAreas = FindObjectsByType<PlayAreaManager>(FindObjectsSortMode.None);
        
        foreach (PlayAreaManager playArea in allPlayAreas)
        {
            if (playArea == null)
                continue;
            
            // Check if this PlayArea is owned by the local client
            if (!playArea.IsOwnedByLocalClient())
                continue;
            
            // Check if the position is within this PlayArea's bounds
            // We'll use a simple distance check from the PlayArea's center
            // For more accurate bounds checking, you could use a Collider or Bounds
            float distance = Vector3.Distance(position, playArea.transform.position);
            
            // Use a reasonable radius (adjust based on your PlayArea size)
            // Most PlayAreas are probably within 10-20 units
            if (distance < 25f) // Adjust this value based on your PlayArea size
            {
                return true;
            }
        }

        return false; */
    }
    
    /// <summary>
    /// Gets the effective volume for a sound at a given position, accounting for mute settings.
    /// </summary>
    /// <param name="position">The world position where the sound will play.</param>
    /// <param name="requestedVolume">The requested volume.</param>
    /// <returns>The effective volume (0 if muted, otherwise the requested volume).</returns>
    public static float GetEffectiveVolume(Vector3 position, float requestedVolume)
    {
        // If mute in play area is enabled and position is in local play area, mute the sound
        if (Instance.muteInPlayArea && IsPositionInLocalPlayArea(position))
        {
            return 0f;
        }
        
        return requestedVolume;
    }
    
    /// <summary>
    /// Plays a one-shot audio clip at a given position with 3D spatial audio settings.
    /// This is a replacement for AudioSource.PlayClipAtPoint() which uses 2D sound by default.
    /// </summary>
    /// <param name="clip">The audio clip to play.</param>
    /// <param name="position">The world position to play the sound from.</param>
    /// <param name="volume">The volume to play the sound at.</param>
    public static void PlayClipAtPoint3D(AudioClip clip, Vector3 position, float volume)
    {

        // Check if sound should be muted
        float effectiveVolume = GetEffectiveVolume(position, volume);
        
        if (effectiveVolume <= 0f)
        {
            // Sound is muted, don't play it
            return;
        }

        // Create a temporary GameObject for the sound
        GameObject soundGameObject = new GameObject("OneShotSound3D");
        soundGameObject.transform.position = position;
        
        // Add AudioSource component with 3D spatial settings
        AudioSource audioSource = soundGameObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = effectiveVolume;
        audioSource.spatialBlend = 1.0f; // Full 3D sound
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic; // Realistic distance falloff
        audioSource.minDistance = 1f; // Start fading at 1 unit
        audioSource.maxDistance = 50f; // Fully faded at 50 units
        audioSource.dopplerLevel = 1.0f; // Enable doppler effect for moving sounds
        audioSource.playOnAwake = false;
        
        // Play the sound
        audioSource.Play();
        
        // Destroy the GameObject after the sound finishes
        Destroy(soundGameObject, clip.length);
    }

    /// <summary>
    /// Gets the ball shot sound clip.
    /// </summary>
    public static AudioClip GetBallShot()
    {
        return Instance.ballShot;
    }

    /// <summary>
    /// Gets the volume for ball shot sound.
    /// </summary>
    public static float GetBallShotVolume()
    {
        return Instance.ballShotVolume;
    }

    /// <summary>
    /// Gets the ball bounce sound clip.
    /// </summary>
    public static AudioClip GetBallBounce()
    {
        return Instance.ballBounce;
    }

    /// <summary>
    /// Gets the volume for ball bounce sound.
    /// </summary>
    public static float GetBallBounceVolume()
    {
        return Instance.ballBounceVolume;
    }

    /// <summary>
    /// Gets the ball machine sound clip.
    /// </summary>
    public static AudioClip GetBallMachine()
    {
        return Instance.ballMachine;
    }

    /// <summary>
    /// Gets the volume for ball machine sound.
    /// </summary>
    public static float GetBallMachineVolume()
    {
        return Instance.ballMachineVolume;
    }

    /// <summary>
    /// Gets the swish sound clip.
    /// </summary>
    public static AudioClip GetSwish()
    {
        return Instance.swish;
    }

    /// <summary>
    /// Gets the volume for swish sound.
    /// </summary>
    public static float GetSwishVolume()
    {
        return Instance.swishVolume;
    }

    /// <summary>
    /// Gets the rim hit sound clip.
    /// </summary>
    public static AudioClip GetRimHit()
    {
        return Instance.rimHit;
    }

    /// <summary>
    /// Gets the volume for rim hit sound.
    /// </summary>
    public static float GetRimHitVolume()
    {
        return Instance.rimHitVolume;
    }

    /// <summary>
    /// Gets the backboard hit sound clip.
    /// </summary>
    public static AudioClip GetBackboardHit()
    {
        return Instance.backboardHit;
    }

    /// <summary>
    /// Gets the volume for backboard hit sound.
    /// </summary>
    public static float GetBackboardHitVolume()
    {
        return Instance.backboardHitVolume;
    }

    /// <summary>
    /// Gets the wall hit sound clip.
    /// </summary>
    public static AudioClip GetWallHit()
    {
        return Instance.wallHit;
    }

    /// <summary>
    /// Gets the volume for wall hit sound.
    /// </summary>
    public static float GetWallHitVolume()
    {
        return Instance.wallHitVolume;
    }

    /// <summary>
    /// Gets the game over sound clip.
    /// </summary>
    public static AudioClip GetGameOver()
    {
        return Instance.gameOver;
    }

    /// <summary>
    /// Gets the volume for game over sound.
    /// </summary>
    public static float GetGameOverVolume()
    {
        return Instance.gameOverVolume;
    }

    /// <summary>
    /// Gets the lose life sound clip.
    /// </summary>
    public static AudioClip GetLoseLife()
    {
        return Instance.loseLife;
    }

    /// <summary>
    /// Gets the volume for lose life sound.
    /// </summary>
    public static float GetLoseLifeVolume()
    {
        return Instance.loseLifeVolume;
    }

    /// <summary>
    /// Gets the gain life sound clip.
    /// </summary>
    public static AudioClip GetGainLife()
    {
        return Instance.gainLife;
    }

    /// <summary>
    /// Gets the volume for gain life sound.
    /// </summary>
    public static float GetGainLifeVolume()
    {
        return Instance.gainLifeVolume;
    }

    /// <summary>
    /// Gets the rim fire activate sound clip.
    /// </summary>
    public static AudioClip GetRimFireActivate()
    {
        return Instance.rimFireActivate;
    }

    /// <summary>
    /// Gets the volume for rim fire activate sound.
    /// </summary>
    public static float GetRimFireActivateVolume()
    {
        return Instance.rimFireActivateVolume;
    }

    /// <summary>
    /// Gets the ball fire loop sound clip.
    /// </summary>
    public static AudioClip GetBallFireLoop()
    {
        return Instance.ballFireLoop;
    }

    /// <summary>
    /// Gets the volume for ball fire loop sound.
    /// </summary>
    public static float GetBallFireLoopVolume()
    {
        return Instance.ballFireLoopVolume;
    }
}


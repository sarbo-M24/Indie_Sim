using System.Collections.Generic;

/// <summary>
/// Persistent data. Survives app launches, reset only by an explicit erase-save.
/// Owned by GameSession.Persistent, saved to profile.json by the global.*
/// save sections (SaveBootstrap).
/// </summary>
[System.Serializable]
public class PersistentStats
{
    public int TotalCoinsEverCollected;
    public int TotalEnemiesKilled;
    public List<string> UnlockedAchievementIds = new List<string>();
    public int TotalRuns;
    public int BestRunDungeonsCleared;
    public bool DemoCompleted; // also the demo dev-panel unlock (global.demo)
    public bool TutorialCompleted; // first new run goes through Tutorial.unity until set (global.tutorial)
}

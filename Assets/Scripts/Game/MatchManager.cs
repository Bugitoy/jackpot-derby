using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MatchState { Countdown, Playing, Ended }

/// <summary>
/// Runs a match: 3-2-1-GO countdown, tracks eliminations and placements, last car standing wins,
/// saves the wins counter (PlayerPrefs) and restarts the scene for Play Again.
/// </summary>
public class MatchManager : MonoBehaviour
{
    public const string WinsKey = "JackpotDerby.Wins";

    [SerializeField] float countdownSeconds = 3f;
    [Tooltip("Delay before the results screen after the match is decided.")]
    [SerializeField] float resultsDelay = 1.6f;

    public static MatchManager Instance { get; private set; }
    public MatchState State { get; private set; } = MatchState.Countdown;
    public float MatchTime { get; private set; }
    public float CountdownLeft { get; private set; }
    public int CarsLeft => CarHealth.Alive.Count;
    /// <summary>Player's finishing place (1-4), 0 while still racing.</summary>
    public int PlayerPlacement { get; private set; }
    public CarIdentity Winner { get; private set; }
    public bool ResultsVisible { get; private set; }
    /// <summary>True while the title screen is up: the countdown waits until Menus calls StartFromMenu.</summary>
    public bool WaitingForMenu { get; private set; }
    /// <summary>Play Again reloads straight into the countdown; Main Menu reloads to the title screen.</summary>
    public static bool SkipMenuOnce;

    // This project enters Play mode without reloading scripts, so static events and lists would keep
    // the previous session's destroyed objects (whose handlers then throw). Start every session clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { SkipMenuOnce = false; Instance = null; }

    public event Action<int> CountdownTick;     // 3, 2, 1, 0 (=GO)
    public event Action MatchStarted;
    public event Action<CarHealth, CarIdentity, int> CarEliminated; // victim, killer, placement
    public event Action<int> ResultsShown;      // player's placement

    readonly List<CarMotor> motors = new List<CarMotor>();
    int lastTick = -1;
    float resultsTimer = -1f;
    int totalCars;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        WaitingForMenu = !SkipMenuOnce && FindAnyObjectByType<Menus>() != null;
        SkipMenuOnce = false;
    }

    void OnEnable() => CarHealth.AnyEliminated += OnEliminated;
    void OnDisable() => CarHealth.AnyEliminated -= OnEliminated;

    void Start()
    {
        foreach (var id in CarIdentity.All)
        {
            var m = id.GetComponent<CarMotor>();
            if (m != null) { motors.Add(m); m.InputEnabled = false; }
        }
        totalCars = CarHealth.Alive.Count;
        CountdownLeft = countdownSeconds;
        SetSystemsRunning(false);
    }

    void Update()
    {
        switch (State)
        {
            case MatchState.Countdown:
                if (WaitingForMenu) break;
                CountdownLeft -= Time.deltaTime;
                int tick = Mathf.CeilToInt(CountdownLeft);
                if (tick != lastTick) { lastTick = tick; CountdownTick?.Invoke(Mathf.Max(0, tick)); }
                if (CountdownLeft <= 0f) Begin();
                break;
            case MatchState.Playing:
                MatchTime += Time.deltaTime;
                break;
        }

        if (resultsTimer >= 0f)
        {
            resultsTimer -= Time.unscaledDeltaTime;
            if (resultsTimer < 0f) ShowResults();
        }
    }

    void Begin()
    {
        State = MatchState.Playing;
        foreach (var m in motors) if (m != null) m.InputEnabled = true;
        SetSystemsRunning(true);
        MatchStarted?.Invoke();
    }

    void OnEliminated(CarHealth victim, CarIdentity killer)
    {
        int placement = CarHealth.Alive.Count + 1;
        var id = victim.GetComponent<CarIdentity>();
        CarEliminated?.Invoke(victim, killer, placement);

        if (id != null && id.isPlayer && PlayerPlacement == 0)
        {
            PlayerPlacement = placement;
            resultsTimer = resultsDelay;
        }

        if (CarHealth.Alive.Count <= 1 && State != MatchState.Ended)
        {
            State = MatchState.Ended;
            Winner = CarHealth.Alive.Count == 1 ? CarHealth.Alive[0].Identity : null;
            if (Winner != null)
            {
                var m = Winner.GetComponent<CarMotor>();
                if (m != null) m.InputEnabled = false;
                if (Winner.isPlayer) PlayerPlacement = 1;
            }
            if (PlayerPlacement == 1)
                PlayerPrefs.SetInt(WinsKey, PlayerPrefs.GetInt(WinsKey, 0) + 1);
            SetSystemsRunning(false);
            resultsTimer = resultsDelay;
        }
    }

    void ShowResults()
    {
        if (ResultsVisible) return;
        ResultsVisible = true;
        ResultsShown?.Invoke(PlayerPlacement);
    }

    void SetSystemsRunning(bool on)
    {
        var spawner = FindAnyObjectByType<ChestSpawner>();
        if (spawner != null) spawner.Running = on;
    }

    public static int Wins => PlayerPrefs.GetInt(WinsKey, 0);

    /// <summary>Called when the title screen's camera swoop lands behind the player: 3, 2, 1, GO.</summary>
    public void StartFromMenu()
    {
        if (!WaitingForMenu) return;
        WaitingForMenu = false;
        CountdownLeft = countdownSeconds;
        lastTick = -1;
    }

    public void PlayAgain()
    {
        PlayerPrefs.Save();
        SkipMenuOnce = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void BackToMenu()
    {
        PlayerPrefs.Save();
        SkipMenuOnce = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static string Ordinal(int n)
    {
        switch (n) { case 1: return "1ST"; case 2: return "2ND"; case 3: return "3RD"; default: return n + "TH"; }
    }
}

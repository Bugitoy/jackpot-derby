using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays every sound in the game. Created automatically at startup and kept across scene loads,
/// so the music doesn't restart on Play Again. Listens to the gameplay events (damage, chests,
/// pickups, the odometer, the match) and gives each car an engine loop.
/// Other scripts can also call the static helpers (GameAudio.Shot, GameAudio.Click...).
/// </summary>
public class GameAudio : MonoBehaviour
{
    public const string MusicVolKey = "JackpotDerby.MusicVolume";
    public const string SfxVolKey = "JackpotDerby.SfxVolume";

    public static GameAudio I { get; private set; }
    public SoundBank Bank { get; private set; }

    /// <summary>0-1, saved in PlayerPrefs.</summary>
    public float MusicVolume { get => musicVol; set { musicVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicVolKey, musicVol); } }
    public float SfxVolume { get => sfxVol; set { sfxVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxVolKey, sfxVol); } }

    const int Voices = 28;
    const float MusicLevel = 0.42f;

    readonly List<AudioSource> pool = new List<AudioSource>();
    readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
    readonly Dictionary<AudioClip, int> playingNow = new Dictionary<AudioClip, int>();
    AudioSource musicA, musicB, stormSource;
    AudioClip currentMusic;
    float musicVol = 0.8f, sfxVol = 0.9f, musicFade = 1f, musicDuck = 1f, duckTimer;
    int nextVoice;
    float lastTick;
    bool stormWasShrinking;
    Odometer playerOdo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        var go = new GameObject("GameAudio");
        DontDestroyOnLoad(go);
        go.AddComponent<GameAudio>();
    }

    void Awake()
    {
        I = this;
        Bank = Resources.Load<SoundBank>("SoundBank");
        musicVol = PlayerPrefs.GetFloat(MusicVolKey, 0.8f);
        sfxVol = PlayerPrefs.GetFloat(SfxVolKey, 0.9f);

        for (int i = 0; i < Voices; i++) pool.Add(NewSource("Voice" + i));
        musicA = NewSource("MusicA"); musicB = NewSource("MusicB");
        foreach (var m in new[] { musicA, musicB }) { m.loop = true; m.ignoreListenerPause = true; m.priority = 0; }
        stormSource = NewSource("StormWind");
        stormSource.loop = true;

        SubscribeStatic();
        SceneManager.sceneLoaded += (s, m) => BindScene();
        BindScene();
    }

    AudioSource NewSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 10f;
        s.maxDistance = 75f;
        return s;
    }

    // ---------------- playing ----------------

    /// <summary>
    /// Plays a one-shot. pos = null plays it flat (UI, player-only stings); otherwise it's placed in the
    /// world, with nearby sounds mostly flat so the close-up chase camera doesn't make them ping-pong.
    /// </summary>
    public AudioSource Play(AudioClip clip, Vector3? pos = null, float volume = 1f, float pitch = 1f,
                            float pitchJitter = 0.04f, float minGap = 0.03f, int maxVoices = 4, bool ignorePause = false)
    {
        if (clip == null || sfxVol <= 0f) return null;
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(clip, out float last) && now - last < minGap) return null;
        if (playingNow.TryGetValue(clip, out int count) && count >= maxVoices) return null;
        lastPlayed[clip] = now;

        var s = TakeVoice();
        s.clip = clip;
        s.volume = volume * sfxVol;
        s.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        s.ignoreListenerPause = ignorePause;
        if (pos.HasValue)
        {
            s.transform.position = pos.Value;
            s.spatialBlend = 0.75f;
        }
        else s.spatialBlend = 0f;
        s.Play();
        return s;
    }

    AudioSource TakeVoice()
    {
        // Prefer a free voice; otherwise steal the oldest in round-robin order.
        for (int i = 0; i < pool.Count; i++)
        {
            var s = pool[(nextVoice + i) % pool.Count];
            if (!s.isPlaying) { nextVoice = (nextVoice + i + 1) % pool.Count; return s; }
        }
        var steal = pool[nextVoice];
        nextVoice = (nextVoice + 1) % pool.Count;
        steal.Stop();
        return steal;
    }

    public void PlayMusic(AudioClip clip, float fadeTime = 1.2f)
    {
        if (clip == null || clip == currentMusic) return;
        currentMusic = clip;
        // Crossfade: the old source fades out in Update while the new one fades in.
        var tmp = musicA; musicA = musicB; musicB = tmp;
        musicA.clip = clip;
        musicA.volume = 0f;
        musicA.Play();
        musicFade = 0f;
        musicFadeSpeed = 1f / Mathf.Max(0.05f, fadeTime);
    }
    float musicFadeSpeed = 1f;

    /// <summary>Drops the music for a moment so a jingle can be heard.</summary>
    public void DuckMusic(float seconds, float level = 0.25f) { duckTimer = seconds; musicDuckTarget = level; }
    float musicDuckTarget = 1f;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        musicFade = Mathf.MoveTowards(musicFade, 1f, dt * musicFadeSpeed);
        duckTimer -= dt;
        musicDuck = Mathf.MoveTowards(musicDuck, duckTimer > 0f ? musicDuckTarget : 1f, dt * (duckTimer > 0f ? 4f : 0.6f));
        float baseVol = MusicLevel * musicVol * musicDuck;
        musicA.volume = baseVol * musicFade;
        musicB.volume = baseVol * (1f - musicFade);
        if (musicFade >= 1f && musicB.isPlaying) musicB.Stop();

        // Count voices per clip for the per-clip voice limit.
        playingNow.Clear();
        foreach (var s in pool)
            if (s.isPlaying && s.clip != null) playingNow[s.clip] = playingNow.TryGetValue(s.clip, out int c) ? c + 1 : 1;

        UpdateStorm(dt);
    }

    void UpdateStorm(float dt)
    {
        var storm = StormController.Instance;
        if (storm == null || Bank == null || Bank.stormLoop == null)
        {
            if (stormSource.isPlaying) stormSource.Stop();
            return;
        }
        if (!stormSource.isPlaying && !AudioListener.pause) { stormSource.clip = Bank.stormLoop; stormSource.Play(); }

        var player = CarIdentity.Player;
        bool outside = player != null && !storm.IsInside(player.transform.position, 0f);
        bool playing = MatchManager.Instance == null || MatchManager.Instance.State == MatchState.Playing;
        float target = (outside ? 0.75f : storm.IsShrinking ? 0.16f : 0.07f) * (playing ? 1f : 0.4f) * sfxVol;
        stormSource.volume = Mathf.MoveTowards(stormSource.volume, target, dt * 0.8f);

        if (storm.IsShrinking && !stormWasShrinking) Play(Bank.thunder, null, 0.75f, 1f, 0.06f);
        stormWasShrinking = storm.IsShrinking;
    }

    // ---------------- gameplay hooks ----------------

    void SubscribeStatic()
    {
        CarHealth.AnyDamaged += OnDamaged;
        CarHealth.AnyEliminated += OnEliminated;
        RamDamage.AnyRam += OnRam;
        Chest.AnyOpened += OnChestOpened;
        Chest.AnySpawned += c => { if (Bank != null) Play(Bank.chestSpawn, null, 0.55f, 1f, 0f, 0.5f); };
        Pickup.AnyCollected += OnPickup;
        BlackCat.AnyScrambled += OnCat;
    }

    void BindScene()
    {
        stormWasShrinking = false;
        if (Bank == null) return;

        var mm = MatchManager.Instance;
        if (mm != null)
        {
            mm.CountdownTick += n =>
            {
                Play(n > 0 ? Bank.beep : Bank.go, null, n > 0 ? 0.6f : 0.75f, 1f, 0f, 0.1f);
                if (n == 0) PlayMusic(Bank.musicMatch, 0.5f);
            };
            mm.ResultsShown += placement =>
            {
                DuckMusic(placement == 1 ? 3.6f : 3f, 0.15f);
                Play(placement == 1 ? Bank.win : Bank.lose, null, 0.85f, 1f, 0f, 0.5f, 1, true);
            };
            mm.CarEliminated += (victim, killer, placement) =>
            {
                if (killer != null && killer.isPlayer) Play(Bank.takedown, null, 0.6f, 1f, 0f);
            };
        }
        PlayMusic(mm != null && mm.WaitingForMenu ? Bank.musicMenu : Bank.musicMatch, 0.6f);

        foreach (var id in CarIdentity.All)
        {
            if (id.GetComponent<EngineAudio>() == null) id.gameObject.AddComponent<EngineAudio>();
            var turbo = id.GetComponent<TurboAbility>();
            var car = id;
            if (turbo != null) turbo.Activated += () => Play(Bank.turbo, car.isPlayer ? (Vector3?)null : car.transform.position, car.isPlayer ? 0.7f : 0.6f, 1f, 0.05f);
            if (!id.isPlayer) continue;

            playerOdo = id.GetComponent<Odometer>();
            if (playerOdo == null) continue;
            playerOdo.LockChanged += locked => Play(locked ? Bank.@lock : Bank.unlock, null, 0.6f, 1f, 0.02f, 0.05f);
            playerOdo.NumberChanged += OnPlayerNumber;
            playerOdo.HintRevealed += OnPlayerHint;
        }
    }

    void OnPlayerNumber(int n)
    {
        if (playerOdo == null || playerOdo.IsLocked) return;
        float now = Time.time;
        if (now - lastTick < 0.07f) return;
        lastTick = now;
        Play(Bank.reelTick, null, 0.22f, 1f + (n % 10) * 0.012f, 0.03f, 0.05f, 2);
    }

    void OnPlayerHint(HintResult h)
    {
        switch (h.kind)
        {
            case HintKind.Lucky: Play(Bank.hintLucky, null, 0.8f, 1f, 0f, 0.3f, 1); DuckMusic(1.8f, 0.35f); break;
            case HintKind.Close: Play(Bank.hintClose, null, 0.65f, 1f, 0f, 0.3f, 1); break;
            case HintKind.TooHigh: Play(Bank.hintHigh, null, 0.55f, h.way ? 0.84f : 1f, 0f, 0.3f, 1); break;
            case HintKind.TooLow: Play(Bank.hintLow, null, 0.55f, h.way ? 1.12f : 1f, 0f, 0.3f, 1); break;
        }
    }

    static bool IsPlayer(CarIdentity id) => id != null && id.isPlayer;

    void OnDamaged(DamageInfo d)
    {
        if (Bank == null || d.amount < 0.5f) return;
        bool mine = IsPlayer(d.victim != null ? d.victim.Identity : null) || IsPlayer(d.attacker);
        switch (d.kind)
        {
            case DamageKind.Blaster:
                Play(Bank.hit, d.point, mine ? 0.75f : 0.5f, IsPlayer(d.victim != null ? d.victim.Identity : null) ? 0.85f : 1.05f, 0.08f, 0.035f, 4);
                break;
            case DamageKind.Storm:
                break;
        }
    }

    void OnRam(RamDamage rammer, RamDamage victim, float damage, bool side)
    {
        if (Bank == null) return;
        float v = Mathf.Clamp(0.45f + damage / 40f, 0.45f, 1f);
        Play(Bank.ram, victim.transform.position, v, side ? 0.92f : 1f, 0.06f, 0.08f, 3);
    }

    void OnEliminated(CarHealth victim, CarIdentity killer)
    {
        if (Bank == null) return;
        Vector3 p = victim.transform.position;
        Play(Bank.explosion, p, 1f, 1f, 0.05f, 0.05f, 3);
        Play(Bank.coins, p, 0.6f, 1f, 0.05f, 0.05f, 3);
    }

    void OnChestOpened(Chest chest, GameObject car, HintResult hint)
    {
        if (Bank == null) return;
        bool mine = CarIdentity.Player != null && car == CarIdentity.Player.gameObject;
        Play(Bank.chestOpen, mine ? (Vector3?)null : chest.transform.position, mine ? 0.8f : 0.55f, 1f, 0f, 0.1f);
    }

    void OnPickup(Pickup p, GameObject car, string message)
    {
        if (Bank == null) return;
        bool mine = CarIdentity.Player != null && car == CarIdentity.Player.gameObject;
        var clip = p.Kind == PickupKind.Clover ? Bank.clover : Bank.dice;
        Play(clip, mine ? (Vector3?)null : p.transform.position, mine ? 0.75f : 0.5f, 1f, 0.02f, 0.1f);
    }

    void OnCat(GameObject car, int n)
    {
        if (Bank == null) return;
        bool mine = CarIdentity.Player != null && car == CarIdentity.Player.gameObject;
        Play(Bank.cat, mine ? (Vector3?)null : car.transform.position, mine ? 0.75f : 0.5f, 1f, 0.06f, 0.3f);
    }

    // ---------------- static helpers for other scripts ----------------

    public static void Shot(Vector3 pos, bool jackpot, bool player)
    {
        if (I == null || I.Bank == null) return;
        I.Play(jackpot ? I.Bank.shotJackpot : I.Bank.shotSmall, pos, player ? 0.5f : 0.4f, 1f, 0.07f, 0.03f, 6);
    }

    public static void ChestBonk(Vector3 pos, bool player)
    {
        if (I == null || I.Bank == null) return;
        I.Play(I.Bank.chestBonk, player ? (Vector3?)null : pos, player ? 0.7f : 0.45f, 1f, 0.05f, 0.15f);
    }

    /// <summary>A car shoving a parked car: a lower, softer crunch than a car-on-car ram.</summary>
    public static void Bump(Vector3 pos, float strength01)
    {
        if (I == null || I.Bank == null) return;
        I.Play(I.Bank.ram, pos, Mathf.Lerp(0.3f, 0.7f, strength01), 0.78f, 0.06f, 0.1f, 3);
    }

    public static void Click() { if (I != null && I.Bank != null) I.Play(I.Bank.uiClick, null, 0.6f, 1f, 0.03f, 0.03f, 2, true); }
    public static void Hover() { if (I != null && I.Bank != null) I.Play(I.Bank.uiHover, null, 0.35f, 1f, 0.03f, 0.04f, 2, true); }
}

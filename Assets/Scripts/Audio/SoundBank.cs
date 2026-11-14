using UnityEngine;

/// <summary>
/// Every sound in the game. Filled automatically by Jackpot Derby/Build Sounds (SoundForge):
/// each field is matched to the WAV with the same name in snake_case.
/// Lives in Resources so GameAudio can load it from any scene.
/// </summary>
[CreateAssetMenu(menuName = "Jackpot Derby/Sound Bank")]
public class SoundBank : ScriptableObject
{
    [Header("Combat")]
    public AudioClip shotSmall;
    public AudioClip shotJackpot;
    public AudioClip hit;
    public AudioClip ram;
    public AudioClip explosion;
    public AudioClip coins;
    public AudioClip takedown;
    public AudioClip turbo;

    [Header("Number and chests")]
    public AudioClip chestOpen;
    public AudioClip chestSpawn;
    public AudioClip chestBonk;
    public AudioClip @lock;
    public AudioClip unlock;
    public AudioClip reelTick;
    public AudioClip hintLucky;
    public AudioClip hintClose;
    public AudioClip hintHigh;
    public AudioClip hintLow;

    [Header("Pickups")]
    public AudioClip clover;
    public AudioClip dice;
    public AudioClip cat;

    [Header("Match and UI")]
    public AudioClip beep;
    public AudioClip go;
    public AudioClip win;
    public AudioClip lose;
    public AudioClip uiClick;
    public AudioClip uiHover;

    [Header("Loops")]
    public AudioClip thunder;
    public AudioClip stormLoop;
    public AudioClip engineLoop;
    public AudioClip musicMatch;
    public AudioClip musicMenu;
}

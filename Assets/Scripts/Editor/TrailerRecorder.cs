using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Records the scripted trailer (TrailerDirector) to Recordings/JackpotDerby_Trailer.mp4 at
/// 1920x1080, 60 fps, with the game's audio. The dry run plays the same cut without recording
/// video and saves a still every 2 seconds to Recordings/TrailerStills for checking shots.
/// The editor's saved win count is put back afterwards (the trailer wins a match).
/// </summary>
[InitializeOnLoad]
public static class TrailerRecorder
{
    const string ModeKey = "JackpotDerby.Trailer.Mode";   // 0 off, 1 dry run, 2 record
    const string WinsKey = "JackpotDerby.Trailer.Wins";
    const string OutDir = "Recordings";

    static RecorderController controller;
    static float nextStill;

    static TrailerRecorder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Jackpot Derby/Trailer/Dry Run (stills)")]
    static void DryRun() => Begin(1);

    [MenuItem("Jackpot Derby/Trailer/Record Trailer")]
    static void Record() => Begin(2);

    static void Begin(int mode)
    {
        if (EditorApplication.isPlaying) return;
        SessionState.SetInt(ModeKey, mode);
        PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "Trailer 1080p");
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        int mode = SessionState.GetInt(ModeKey, 0);
        if (mode == 0) return;

        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetInt(WinsKey, PlayerPrefs.GetInt(MatchManager.WinsKey, 0));
            Directory.CreateDirectory(OutDir);
            if (mode == 2) StartMovie();
            else
            {
                Time.captureFramerate = 60;
                string stills = Path.Combine(OutDir, "TrailerStills");
                Directory.CreateDirectory(stills);
                foreach (var f in Directory.GetFiles(stills, "*.png")) File.Delete(f);
                nextStill = 0.5f;
                TrailerDirector.StillRequested = t =>
                {
                    if (t < nextStill) return;
                    ScreenCapture.CaptureScreenshot(Path.Combine(stills, $"t{t:00.0}.png"));
                    nextStill += 2f;
                };
            }
            new GameObject("TrailerDirector").AddComponent<TrailerDirector>();
            EditorApplication.update += Poll;
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Poll;
            if (controller != null && controller.IsRecording()) controller.StopRecording();
            controller = null;
            Time.captureFramerate = 0;
            TrailerDirector.StillRequested = null;
            PlayerPrefs.SetInt(MatchManager.WinsKey, SessionState.GetInt(WinsKey, 0));
            PlayerPrefs.Save();
            SessionState.EraseInt(ModeKey);
            Debug.Log(mode == 2 ? $"[Trailer] Saved {Path.GetFullPath(Path.Combine(OutDir, "JackpotDerby_Trailer.mp4"))}"
                                : $"[Trailer] Dry run stills in {Path.GetFullPath(Path.Combine(OutDir, "TrailerStills"))}");
        }
    }

    static void Poll()
    {
        if (TrailerDirector.Finished && EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    static void StartMovie()
    {
        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Trailer";
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        movie.AudioInputSettings.PreserveAudio = true;
        movie.OutputFile = Path.Combine(OutDir, "JackpotDerby_Trailer");
        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToManual();
        settings.FrameRate = 60f;
        settings.CapFrameRate = true;
        RecorderOptions.VerboseMode = false;
        controller = new RecorderController(settings);
        controller.PrepareRecording();
        controller.StartRecording();
    }
}

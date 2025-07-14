using UnityEngine;
using Muse.Net;
using Muse.Net.Enums;
using Muse.Net.EventArgs;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

public class MuseService : MonoBehaviour
{
    public static MuseService Instance { get; private set; }

    [Header("Connection Settings")]
    [Tooltip("The BLE name or address of your Muse 2 headset.")]
    public string museDeviceName = "Muse-2";

    public bool IsConnected { get; private set; }
    public event Action OnBlink;
    public event Action OnJawClench;

    private MuseClient client;
    private float[] eyeBuffer = new float[0];
    private float[] jawBuffer = new float[0];

    // thresholds & timings
    const int BufferSize = 256;         // ~1.2 s @ 220 Hz
    const float EyeThreshold = 300f;    // adjust as needed
    const float JawThreshold = 500f;    // adjust as needed
    const float AnalysisInterval = 0.3f; // in seconds
    const float CooldownDuration = 1f;   // in seconds
    private bool inCooldown = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
        eyeBuffer = new float[BufferSize];
        jawBuffer = new float[BufferSize];
    }

    private async void Start()
    {
        // optional: auto‐connect on start
        // await Connect();
    }

    public async Task Connect()
    {
        if (IsConnected) return;

        client = new MuseClient(museDeviceName);
        bool ok = await client.Connect();
        if (!ok) { Debug.LogError("Failed to connect to Muse"); return; }

        // Subscribe to the 4 EEG channels
        await client.Subscribe(
            Channel.EEG_AF7, Channel.EEG_AF8,
            Channel.EEG_TP9, Channel.EEG_TP10
        );

        client.NotifyEeg += OnEegReceived;
        await client.Resume();

        IsConnected = true;
        Debug.Log("Muse connected and streaming");
        // start the analysis loop
        InvokeRepeating(nameof(AnalyzeBuffers), AnalysisInterval, AnalysisInterval);
    }

    public async Task Disconnect()
    {
        if (!IsConnected) return;
        CancelInvoke(nameof(AnalyzeBuffers));
        client.NotifyEeg -= OnEegReceived;
        await client.Pause();
        client.Disconnect();
        IsConnected = false;
        Debug.Log("Muse disconnected");
    }

    private void OnEegReceived(object sender, EegEventArgs e)
    {
        // e.Samples length is channel-specific; just take max abs
        float maxAmp = 0f;
        foreach (var s in e.Samples) maxAmp = Mathf.Max(maxAmp, Mathf.Abs(s));

        // Demultiplex: AF7/AF8 → eyeBuffer, TP9/TP10 → jawBuffer
        if (e.Channel == Channel.EEG_AF7 || e.Channel == Channel.EEG_AF8)
            ShiftAndAppend(eyeBuffer, maxAmp);
        else
            ShiftAndAppend(jawBuffer, maxAmp);
    }

    private void ShiftAndAppend(float[] buf, float value)
    {
        // simple circular shift
        Array.Copy(buf, 1, buf, 0, buf.Length - 1);
        buf[buf.Length - 1] = value;
    }

    private void AnalyzeBuffers()
    {
        if (inCooldown) return;

        float eyePeak = MaxValue(eyeBuffer);
        float jawPeak = MaxValue(jawBuffer);

        bool eyeBlink = eyePeak > EyeThreshold;
        bool jawClench = jawPeak > JawThreshold;

        if (eyeBlink && !jawClench) FireBlink();
        else if (!eyeBlink && jawClench) FireJaw();
        else if (eyeBlink && jawClench)
        {
            // winner-takes-all
            if (eyePeak > jawPeak) FireBlink();
            else FireJaw();
        }
    }

    private float MaxValue(float[] buf)
    {
        float m = 0;
        foreach (var v in buf) if (v > m) m = v;
        return m;
    }

    private void FireBlink()
    {
        inCooldown = true;
        OnBlink?.Invoke();
        Invoke(nameof(ResetCooldown), CooldownDuration);
    }

    private void FireJaw()
    {
        inCooldown = true;
        OnJawClench?.Invoke();
        Invoke(nameof(ResetCooldown), CooldownDuration);
    }

    private void ResetCooldown() => inCooldown = false;
}

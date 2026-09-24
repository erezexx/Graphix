using System;
using System.Runtime.InteropServices;
using System.Threading;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Graphix.ModMain), "Graphix", "1.0.0", "erezexx & neomij")]
[assembly: MelonGame]

namespace Graphix
{
    public class ModMain : MelonMod
    {
        private static MelonPreferences_Category configCategory;

        private static MelonPreferences_Entry<bool>   modEnabled;
        private static MelonPreferences_Entry<int>    resolutionWidth;
        private static MelonPreferences_Entry<int>    resolutionHeight;
        private static MelonPreferences_Entry<int>    screenMode;
        private static MelonPreferences_Entry<int>    textureMipmapLimit;
        private static MelonPreferences_Entry<int>    anisotropicFiltering;
        private static MelonPreferences_Entry<int>    antiAliasingSamples;
        private static MelonPreferences_Entry<bool>   softParticlesEnabled;
        private static MelonPreferences_Entry<int>    shadowQuality;
        private static MelonPreferences_Entry<int>    shadowResolution;
        private static MelonPreferences_Entry<float>  shadowDistance;
        private static MelonPreferences_Entry<int>    targetFPS;
        private static MelonPreferences_Entry<int>    vsyncCount;
        private static MelonPreferences_Entry<bool>   fastLoadingPriority;
        private static MelonPreferences_Entry<bool>   showPerformanceStats;
        private static MelonPreferences_Entry<bool>   preciseFpsLimiter;
        private static MelonPreferences_Entry<string> hudToggleKey;

        private bool showHud = true;

        private const int FRAME_HISTORY = 300;
        private readonly float[] frameTimes = new float[FRAME_HISTORY];
        private int frameHead       = 0;
        private int framesCollected = 0;

        private float currentFps;
        private float currentFrameMs;
        private float fps1Low;
        private float fps01Low;
        private float minFps = float.MaxValue;
        private float maxFps = 0f;
        private float fpsUpdateTimer;
        private const float FPS_UPDATE_INTERVAL = 0.25f;

        private GUIStyle  hudStyle;
        private Texture2D hudBg;

        private static readonly Color ColLabel  = new Color(0.35f, 0.95f, 0.35f);
        private static readonly Color ColValue  = Color.white;
        private static readonly Color ColOrange = new Color(1.00f, 0.60f, 0.10f);

        private readonly System.Diagnostics.Stopwatch frameStopwatch = new System.Diagnostics.Stopwatch();

        public override void OnInitializeMelon()
        {
            configCategory = MelonPreferences.CreateCategory("Graphix", "Graphix Settings");

            modEnabled           = configCategory.CreateEntry("ModEnabled", true, "Master switch for the mod");
            resolutionWidth      = configCategory.CreateEntry("ResolutionWidth", 1920, "Screen resolution width");
            resolutionHeight     = configCategory.CreateEntry("ResolutionHeight", 1080, "Screen resolution height");
            screenMode           = configCategory.CreateEntry("ScreenMode", 1, "0=ExclusiveFullscreen, 1=FullscreenWindow, 2=MaximizedWindow, 3=Windowed");
            textureMipmapLimit   = configCategory.CreateEntry("TextureMipmapLimit", 0, "Texture limit: 0=Full, 1=Half, 2=Quarter, 3=Eighth");
            anisotropicFiltering = configCategory.CreateEntry("AnisotropicFiltering", 2, "0=Disabled, 1=Enable, 2=ForceEnable");
            antiAliasingSamples  = configCategory.CreateEntry("AntiAliasingSamples", 8, "MSAA: 0=Disabled, 2=2x, 4=4x, 8=8x");
            softParticlesEnabled = configCategory.CreateEntry("SoftParticlesEnabled", true, "Soft Particles rendering switch");
            shadowQuality        = configCategory.CreateEntry("ShadowQuality", 2, "0=Disabled, 1=HardOnly, 2=All");
            shadowResolution     = configCategory.CreateEntry("ShadowResolution", 2, "0=Low(512), 1=Med(1024), 2=High(2048), 3=VeryHigh(4096)");
            shadowDistance       = configCategory.CreateEntry("ShadowDistance", 150.0f, "Shadow drawing distance in meters");
            targetFPS            = configCategory.CreateEntry("TargetFPS", -1, "-1=Unlimited, or set specific lock (60, 144)");
            vsyncCount           = configCategory.CreateEntry("VSyncCount", 0, "0=Disabled, 1=Every VBlank, 2=Every Second VBlank");
            fastLoadingPriority  = configCategory.CreateEntry("FastLoadingPriority", true, "true=Faster loading (uses more CPU), false=Normal loading");
            showPerformanceStats = configCategory.CreateEntry("ShowPerformanceStats", true, "Show in-game HUD with performance stats");
            preciseFpsLimiter    = configCategory.CreateEntry("PreciseFpsLimiter", true,
                "true = hybrid sleep+spin limiter (more accurate pacing, recommended with VSync=0), false = Unity default targetFrameRate");
            hudToggleKey         = configCategory.CreateEntry("HudToggleKey", "F4",
                "KeyCode used to toggle the HUD (e.g. F4, F8, BackQuote)");

            MelonPreferences.Save();

            if (!modEnabled.Value)
            {
                LoggerInstance.Msg("Graphix is disabled in config.");
                return;
            }

            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg("Graphix Mod Loaded successfully!");
            LoggerInstance.Msg("Created by: erezexx & neomij");
            LoggerInstance.Msg($"Active Graphics API: {SystemInfo.graphicsDeviceType}");
            LoggerInstance.Msg($"GPU Model: {SystemInfo.graphicsDeviceName}");
            LoggerInstance.Msg($"VRAM: {SystemInfo.graphicsMemorySize} MB");
            LoggerInstance.Msg($"System RAM: {SystemInfo.systemMemorySize} MB");
            LoggerInstance.Msg("==================================================");

            NvmlGpu.TryInit();
            if (NvmlGpu.Ok) LoggerInstance.Msg("NVML initialised - extended GPU stats enabled in HUD.");
            else            LoggerInstance.Msg("NVML not available - HUD will show 'N/A' for GPU usage/temp/clock/power.");

            ApplyGraphicsSettings();
            LogAutoPresetRecommendation();

            frameStopwatch.Restart();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (!modEnabled.Value) return;
            ApplyGraphicsSettings();
        }

        private void ApplyGraphicsSettings()
        {
            try
            {
                FullScreenMode mode = (FullScreenMode)Mathf.Clamp(screenMode.Value, 0, 3);
                Screen.SetResolution(resolutionWidth.Value, resolutionHeight.Value, mode);

                int mip = Mathf.Clamp(textureMipmapLimit.Value, 0, 3);
                SetTextureMipmapLimit(mip);

                QualitySettings.anisotropicFiltering = (AnisotropicFiltering)Mathf.Clamp(anisotropicFiltering.Value, 0, 2);
                QualitySettings.antiAliasing         = Mathf.Clamp(antiAliasingSamples.Value, 0, 8);

                QualitySettings.softParticles    = softParticlesEnabled.Value;
                QualitySettings.shadows          = (ShadowQuality)Mathf.Clamp(shadowQuality.Value, 0, 2);
                QualitySettings.shadowResolution = (ShadowResolution)Mathf.Clamp(shadowResolution.Value, 0, 3);
                QualitySettings.shadowDistance   = shadowDistance.Value;

                QualitySettings.vSyncCount = Mathf.Clamp(vsyncCount.Value, 0, 2);

                if (preciseFpsLimiter.Value)
                    Application.targetFrameRate = -1;
                else
                    Application.targetFrameRate = targetFPS.Value;

                Application.backgroundLoadingPriority = fastLoadingPriority.Value
                    ? UnityEngine.ThreadPriority.High
                    : UnityEngine.ThreadPriority.BelowNormal;
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Graphix failed to apply some graphics settings: {ex.Message}");
            }
        }

        private static void SetTextureMipmapLimit(int limit)
        {
            var t = typeof(QualitySettings);

            var globalProp = t.GetProperty("globalTextureMipmapLimit",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (globalProp != null && globalProp.CanWrite && globalProp.PropertyType == typeof(int))
            {
                globalProp.SetValue(null, limit);
                return;
            }

            var masterProp = t.GetProperty("masterTextureLimit",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (masterProp != null && masterProp.CanWrite && masterProp.PropertyType == typeof(int))
            {
                masterProp.SetValue(null, limit);
                return;
            }
        }

        private void LogAutoPresetRecommendation()
        {
            int vram  = SystemInfo.graphicsMemorySize;
            int ram   = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            var api   = SystemInfo.graphicsDeviceType;
            string gpu = SystemInfo.graphicsDeviceName ?? "Unknown";

            string tier =
                (vram >= 10240 && ram >= 16384) ? "Ultra" :
                (vram >= 6144  && ram >= 12288) ? "High"  :
                (vram >= 3072  && ram >= 8192 ) ? "Medium":
                (vram >= 2048  && ram >= 4096 ) ? "Low"   : "VeryLow";

            int recShadowRes = tier switch { "Ultra" => 3, "High" => 2, "Medium" => 1, _ => 0 };
            int recShadowQuality = tier == "VeryLow" ? 0 : (tier == "Low" ? 1 : 2);
            int recAA = (tier == "Ultra" || tier == "High") ? 8 : (tier == "Medium" ? 4 : 0);
            int recMip = tier switch { "Ultra" => 0, "High" => 0, "Medium" => 1, "Low" => 2, _ => 3 };
            float recShadowDist = tier switch
            {
                "Ultra"  => 200f,
                "High"   => 150f,
                "Medium" => 100f,
                "Low"    =>  60f,
                _        =>  30f
            };

            LoggerInstance.Msg("=========== Graphix Auto-Preset ===========");
            LoggerInstance.Msg($"GPU: {gpu}");
            LoggerInstance.Msg($"API: {api} | VRAM: {vram} MB | RAM: {ram} MB | CPU cores: {cores}");
            LoggerInstance.Msg($"Recommended tier: {tier}");
            LoggerInstance.Msg($"  ShadowQuality    = {recShadowQuality} ({ShadowLabel(recShadowQuality)})");
            LoggerInstance.Msg($"  ShadowResolution = {recShadowRes} ({ShadowResLabel(recShadowRes)})");
            LoggerInstance.Msg($"  ShadowDistance   = {recShadowDist} m");
            LoggerInstance.Msg($"  AntiAliasing     = {recAA}x");
            LoggerInstance.Msg($"  MipmapLimit      = {recMip}");
            LoggerInstance.Msg("(Settings are NOT applied automatically - change config if you agree)");
            LoggerInstance.Msg("===========================================");
        }

        private static string ShadowLabel(int q)    => q switch { 0 => "Disabled", 1 => "HardOnly", _ => "All" };
        private static string ShadowResLabel(int r) => r switch { 0 => "512", 1 => "1024", 2 => "2048", _ => "4096" };

        public override void OnUpdate()
        {
            if (!modEnabled.Value) return;

            if (Enum.TryParse<KeyCode>(hudToggleKey.Value, true, out var key) &&
                InputHelper.GetKeyDown(key))
            {
                showHud = !showHud;
                LoggerInstance.Msg($"Graphix HUD: {(showHud ? "ON" : "OFF")}");
            }

            if (!showHud || !showPerformanceStats.Value) return;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                frameTimes[frameHead] = dt;
                frameHead = (frameHead + 1) % FRAME_HISTORY;
                if (framesCollected < FRAME_HISTORY) framesCollected++;
            }

            fpsUpdateTimer += dt;
            if (fpsUpdateTimer >= FPS_UPDATE_INTERVAL && framesCollected > 0)
            {
                fpsUpdateTimer = 0f;
                UpdateFpsStats();
            }
        }

        private void UpdateFpsStats()
        {
            float sum = 0f, mn = float.MaxValue, mx = 0f;
            for (int i = 0; i < framesCollected; i++)
            {
                float t = frameTimes[i];
                sum += t;
                if (t < mn) mn = t;
                if (t > mx) mx = t;
            }

            float avgMs = (sum / framesCollected) * 1000f;
            currentFrameMs = avgMs;
            currentFps     = avgMs > 0f ? 1000f / avgMs : 0f;
            minFps         = mx > 0f   ? 1000f / (mx * 1000f) : 0f;
            maxFps         = mn < float.MaxValue ? 1000f / (mn * 1000f) : 0f;

            var copy = new float[framesCollected];
            Array.Copy(frameTimes, copy, framesCollected);
            Array.Sort(copy);

            fps1Low  = PercentileFps(copy, 0.99f);
            fps01Low = PercentileFps(copy, 0.999f);
        }

        private static float PercentileFps(float[] sortedAscending, float percentile)
        {
            if (sortedAscending.Length == 0) return 0f;
            int idx = Mathf.Clamp((int)(sortedAscending.Length * percentile), 0, sortedAscending.Length - 1);
            float ms = sortedAscending[idx] * 1000f;
            return ms > 0f ? 1000f / ms : 0f;
        }

        public override void OnLateUpdate()
        {
            if (!modEnabled.Value || !preciseFpsLimiter.Value) return;

            if (QualitySettings.vSyncCount > 0) { frameStopwatch.Restart(); return; }

            int target = targetFPS.Value;
            if (target <= 0) { frameStopwatch.Restart(); return; }

            double targetMs = 1000.0 / target;
            double elapsed  = frameStopwatch.Elapsed.TotalMilliseconds;

            if (frameStopwatch.IsRunning && elapsed < targetMs)
            {
                double remaining = targetMs - elapsed;

                if (remaining > 2.0)
                    Thread.Sleep((int)(remaining - 1.5));

                while (frameStopwatch.Elapsed.TotalMilliseconds < targetMs)
                    Thread.SpinWait(20);
            }

            frameStopwatch.Restart();
        }

        public override void OnGUI()
        {
            if (!modEnabled.Value || !showHud || !showPerformanceStats.Value) return;
            EnsureHudStyles();

            const float pad   = 8f;
            const float lineH = 18f;
            const float boxW  = 620f;
            float boxH = lineH * 3 + pad * 2 + 2f;

            GUI.DrawTexture(new Rect(10, 10, boxW, boxH), hudBg);

            float cx = 10 + pad;
            float cy = 10 + pad;

            long managedMb = GC.GetTotalMemory(false) / (1024 * 1024);

            string gpuPct   = NvmlGpu.Ok ? $"{NvmlGpu.Usage()} %"    : "N/A";
            string gpuMHz   = NvmlGpu.Ok ? $"{NvmlGpu.Clock()} MHz"  : "N/A";
            string gpuTemp  = NvmlGpu.Ok ? $"{NvmlGpu.Temp()} °C"    : "N/A";
            string gpuPower = NvmlGpu.Ok ? $"{NvmlGpu.Power():F1} W" : "N/A";

            DrawLabel(cx,        cy, "GPU:",   ColLabel);
            DrawLabel(cx +  55f, cy, gpuPct,   ColOrange);
            DrawLabel(cx + 135f, cy, gpuMHz,   ColOrange);
            DrawLabel(cx + 245f, cy, gpuTemp,  ColOrange);
            DrawLabel(cx + 330f, cy, gpuPower, ColOrange);

            cy += lineH;
            DrawLabel(cx,        cy, "MEM:",                                     ColLabel);
            DrawLabel(cx +  55f, cy, $"{managedMb} MB",                          ColOrange);
            DrawLabel(cx + 175f, cy, $"{SystemInfo.graphicsMemorySize} MB VRAM", ColOrange);

            cy += lineH;
            DrawLabel(cx,        cy, "FPS:",                              ColLabel);
            DrawLabel(cx +  55f, cy, $"{Mathf.RoundToInt(currentFps)}",   ColValue);
            DrawLabel(cx + 115f, cy, $"{currentFrameMs:F1} ms",           ColValue);
            DrawLabel(cx + 215f, cy, $"{Mathf.RoundToInt(fps1Low)} 1%",   ColValue);
            DrawLabel(cx + 305f, cy, $"{Mathf.RoundToInt(fps01Low)} .1%", ColValue);
            DrawLabel(cx + 415f, cy, $"v{Mathf.RoundToInt(minFps)} ^{Mathf.RoundToInt(maxFps)}", ColValue);
        }

        private void DrawLabel(float x, float y, string text, Color color)
        {
            hudStyle.normal.textColor = color;
            GUI.Label(new Rect(x, y, 560, 20), text, hudStyle);
        }

        private void EnsureHudStyles()
        {
            if (hudStyle != null) return;

            hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText  = false
            };

            hudBg = new Texture2D(1, 1);
            hudBg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            hudBg.Apply();
        }
    }

    internal static class InputHelper
    {
        private static System.Reflection.MethodInfo _getKeyDown;
        private static bool _init;

        public static bool GetKeyDown(KeyCode key)
        {
            if (!_init)
            {
                _init = true;
                try
                {
                    var t = Type.GetType("UnityEngine.Input, UnityEngine.InputLegacyModule")
                         ?? Type.GetType("UnityEngine.Input, UnityEngine.CoreModule")
                         ?? Type.GetType("UnityEngine.Input, UnityEngine");
                    if (t != null)
                        _getKeyDown = t.GetMethod("GetKeyDown", new[] { typeof(KeyCode) });
                }
                catch { }
            }

            if (_getKeyDown == null) return false;
            try { return (bool)_getKeyDown.Invoke(null, new object[] { key }); }
            catch { return false; }
        }
    }

    internal static class NvmlGpu
    {
        [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")] private static extern int Init();
        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
        private static extern int GetHandle(uint i, out IntPtr d);
        [DllImport("nvml.dll")] private static extern int nvmlDeviceGetUtilizationRates(IntPtr d, out Util u);
        [DllImport("nvml.dll")] private static extern int nvmlDeviceGetTemperature(IntPtr d, int s, out uint t);
        [DllImport("nvml.dll")] private static extern int nvmlDeviceGetClockInfo(IntPtr d, int t, out uint c);
        [DllImport("nvml.dll")] private static extern int nvmlDeviceGetPowerUsage(IntPtr d, out uint mW);

        [StructLayout(LayoutKind.Sequential)]
        private struct Util { public uint Gpu; public uint Memory; }

        private static IntPtr _dev;
        public static bool Ok { get; private set; }

        public static void TryInit()
        {
            try
            {
                if (Init() != 0)                 { Ok = false; return; }
                if (GetHandle(0, out _dev) != 0) { Ok = false; return; }
                Ok = true;
            }
            catch
            {
                Ok = false;
            }
        }

        public static uint  Usage() { return Ok && nvmlDeviceGetUtilizationRates(_dev, out var u) == 0 ? u.Gpu      : 0u; }
        public static uint  Temp()  { return Ok && nvmlDeviceGetTemperature(_dev, 0, out var t)     == 0 ? t         : 0u; }
        public static uint  Clock() { return Ok && nvmlDeviceGetClockInfo(_dev, 0, out var c)       == 0 ? c         : 0u; }
        public static float Power() { return Ok && nvmlDeviceGetPowerUsage(_dev, out var p)         == 0 ? p / 1000f : 0f; }
    }
}
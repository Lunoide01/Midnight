using UnityEngine;
using UnityEngine.Events;

public class DawnTimer : MonoBehaviour
{
    [Header("Durata della notte in secondi")]
    public float nightDuration = 180f;

    [Header("Riferimenti")]
    public Light sunLight;
    public Material nightSkybox;    // CoriolisNight4k
    public Material morningSkybox;  // Cloudymorning
    public Material daySkybox;      // MegaSun

    [Header("Colori luce")]
    public Color nightLightColor   = new Color(0.20f, 0.22f, 0.50f);
    public Color preDawnLightColor = new Color(0.80f, 0.35f, 0.10f);
    public Color dawnLightColor    = new Color(1.00f, 0.85f, 0.60f);

    [Header("Colori ambient")]
    public Color nightAmbient  = new Color(0.04f, 0.04f, 0.10f);
    public Color preDawnAmbient = new Color(0.20f, 0.10f, 0.08f);
    public Color dawnAmbient   = new Color(0.60f, 0.50f, 0.40f);

    [Header("Colori nebbia")]
    public Color nightFog   = new Color(0.05f, 0.07f, 0.15f);
    public Color preDawnFog = new Color(0.40f, 0.20f, 0.15f);
    public Color dawnFog    = new Color(0.80f, 0.60f, 0.40f);

    [Header("Eventi")]
    public UnityEvent onPreDawnWarning;  // Scatta all'80% — "L'alba si avvicina!"
    public UnityEvent onDawnReached;     // Scatta al 100% — vampiro brucia

    // Stato interno
    private float timer = 0f;
    private bool preDawnTriggered = false;
    private bool dawnTriggered = false;

    void Start()
    {
        // Imposta skybox notturna all'avvio
        if (nightSkybox != null)
        {
            RenderSettings.skybox = nightSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // Imposta stato iniziale
        RenderSettings.ambientLight = nightAmbient;
        RenderSettings.fogColor = nightFog;

        if (sunLight != null)
        {
            sunLight.color = nightLightColor;
            sunLight.intensity = 0.1f;
            sunLight.transform.rotation = Quaternion.Euler(30f, -30f, 0f);
        }
    }

    void Update()
    {
        if (dawnTriggered) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / nightDuration);

        // ─── FASE 1: NOTTE (0% → 75%) ───────────────────────────────
        if (t < 0.75f)
        {
            float phase = t / 0.75f;

            // Luce lunare stabile con leggero cambio
            if (sunLight != null)
            {
                sunLight.color = nightLightColor;
                sunLight.intensity = Mathf.Lerp(0.10f, 0.12f, phase);
            }

            RenderSettings.ambientLight = nightAmbient;
            RenderSettings.fogColor = nightFog;

            // Skybox notturna: abbassa leggermente l'exposure verso la fine
            SetSkyboxExposure(nightSkybox, Mathf.Lerp(1.0f, 0.85f, phase));
        }

        // ─── FASE 2: PRE-ALBA (75% → 90%) ───────────────────────────
        else if (t < 0.90f)
        {
            float phase = (t - 0.75f) / 0.15f;

            // Evento avviso alba (una sola volta)
            if (!preDawnTriggered)
            {
                preDawnTriggered = true;
                RenderSettings.skybox = morningSkybox;
                DynamicGI.UpdateEnvironment();
                onPreDawnWarning?.Invoke();
            }

            // Luce che vira verso arancione
            if (sunLight != null)
            {
                sunLight.color = Color.Lerp(nightLightColor, preDawnLightColor, phase);
                sunLight.intensity = Mathf.Lerp(0.12f, 0.80f, phase);
                sunLight.transform.rotation = Quaternion.Lerp(
                    Quaternion.Euler(30f, -30f, 0f),
                    Quaternion.Euler(50f, 0f, 0f),
                    phase
                );
            }

            // Ambient e nebbia verso rosa/arancio
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, preDawnAmbient, phase);
            RenderSettings.fogColor = Color.Lerp(nightFog, preDawnFog, phase);

            // Skybox mattutina: aumenta exposure gradualmente
            SetSkyboxExposure(morningSkybox, Mathf.Lerp(0.3f, 1.0f, phase));
        }

        // ─── FASE 3: ALBA (90% → 100%) ──────────────────────────────
        else
        {
            float phase = (t - 0.90f) / 0.10f;

            // Luce che diventa pieno sole
            if (sunLight != null)
            {
                sunLight.color = Color.Lerp(preDawnLightColor, dawnLightColor, phase);
                sunLight.intensity = Mathf.Lerp(0.80f, 2.50f, phase);
                sunLight.transform.rotation = Quaternion.Lerp(
                    Quaternion.Euler(50f, 0f, 0f),
                    Quaternion.Euler(65f, 10f, 0f),
                    phase
                );
            }

            RenderSettings.ambientLight = Color.Lerp(preDawnAmbient, dawnAmbient, phase);
            RenderSettings.fogColor = Color.Lerp(preDawnFog, dawnFog, phase);

            SetSkyboxExposure(morningSkybox, Mathf.Lerp(1.0f, 2.0f, phase));

            // Fine — vampiro brucia
            if (phase >= 1f)
            {
                dawnTriggered = true;
                if (daySkybox != null)
                {
                    RenderSettings.skybox = daySkybox;
                    DynamicGI.UpdateEnvironment();
                }
                onDawnReached?.Invoke();
            }
        }
    }

    // Utility: cambia l'exposure della skybox se supportata
    private void SetSkyboxExposure(Material skybox, float value)
    {
        if (skybox == null) return;
        if (skybox.HasProperty("_Exposure"))
            skybox.SetFloat("_Exposure", value);
    }

    // Utility pubblica: tempo rimasto in secondi
    public float GetTimeRemaining() => Mathf.Max(0f, nightDuration - timer);

    // Utility pubblica: progresso 0-1
    public float GetProgress() => Mathf.Clamp01(timer / nightDuration);
}

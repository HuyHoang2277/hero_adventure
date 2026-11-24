using System.Collections;
using UnityEngine;

/// <summary>
/// Quản lý tập trung VFX hạt 2D (dùng chung 1 ParticleSystem + Emit(), không Instantiate mỗi lần nổ),
/// Hit-Stop trên unscaled time (tôn trọng 2x Speed và Pause) và phản hồi rung thiết bị.
/// </summary>
public class CombatVFXManager : MonoBehaviour
{
    private static CombatVFXManager instance;

    public static CombatVFXManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CombatVFXManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CombatVFXManager");
                    instance = go.AddComponent<CombatVFXManager>();
                }
            }
            return instance;
        }
    }

    private ParticleSystem burstParticles;
    private Material particleMaterial;

    private Coroutine hitStopCoroutine;
    private float hitStopEndRealtime = 0f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        InitializeParticleSystem();
    }

    private void InitializeParticleSystem()
    {
        if (burstParticles != null) return;

        GameObject psObj = new GameObject("Pooled2DBurstVFX");
        psObj.transform.SetParent(transform, false);

        burstParticles = psObj.AddComponent<ParticleSystem>();
        burstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = burstParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.50f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.gravityModifier = 0.65f;
        main.maxParticles = 240;

        var emission = burstParticles.emission;
        emission.enabled = false; // Chỉ phát bằng Emit() có kiểm soát

        var shape = burstParticles.shape;
        shape.enabled = false;

        // Thu nhỏ mượt về 0 để hạt không biến mất đột ngột (vfx.md)
        var sizeOverLifetime = burstParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve shrinkCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.7f),
            new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrinkCurve);

        var colorOverLifetime = burstParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGrad = new Gradient();
        fadeGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGrad);

        ParticleSystemRenderer psr = psObj.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                particleMaterial = new Material(spriteShader);
                psr.material = particleMaterial;
            }
            psr.sortingLayerName = "Gem";
            psr.sortingOrder = 15;
        }
    }

    public void PlayGemBurst(Vector3 worldPos, Color gemColor, int count = 10)
    {
        if (burstParticles == null) InitializeParticleSystem();
        if (burstParticles == null) return;

        Color brightTint = Color.Lerp(gemColor, Color.white, 0.25f);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(1.5f, 3.3f);
            Vector3 velocity = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 0.6f, 0f);

            var emitParams = new ParticleSystem.EmitParams
            {
                position = worldPos + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.12f, 0.12f), 0f),
                velocity = velocity,
                startColor = (i % 3 == 0) ? brightTint : gemColor,
                startSize = Random.Range(0.09f, 0.16f),
                startLifetime = Random.Range(0.28f, 0.48f)
            };
            burstParticles.Emit(emitParams, 1);
        }
    }

    public void PlayHitImpact(Vector3 worldPos, bool isCritical)
    {
        if (burstParticles == null) InitializeParticleSystem();
        if (burstParticles == null) return;

        int count = isCritical ? 16 : 9;
        Color primary = isCritical ? new Color(1f, 0.30f, 0.22f, 1f) : new Color(1f, 0.85f, 0.35f, 1f);
        Color accent = new Color(1f, 0.96f, 0.78f, 1f);

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = isCritical ? Random.Range(2.4f, 4.8f) : Random.Range(1.6f, 3.2f);
            Vector3 velocity = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 0.5f, 0f);

            var emitParams = new ParticleSystem.EmitParams
            {
                position = worldPos + new Vector3(0f, 0.15f, 0f),
                velocity = velocity,
                startColor = (i % 2 == 0) ? primary : accent,
                startSize = isCritical ? Random.Range(0.11f, 0.20f) : Random.Range(0.08f, 0.13f),
                startLifetime = isCritical ? Random.Range(0.32f, 0.52f) : Random.Range(0.22f, 0.38f)
            };
            burstParticles.Emit(emitParams, 1);
        }
    }

    public void PlaySupportSparkle(Vector3 worldPos, Color tint)
    {
        if (burstParticles == null) InitializeParticleSystem();
        if (burstParticles == null) return;

        for (int i = 0; i < 8; i++)
        {
            Vector3 velocity = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(1.2f, 2.5f), 0f);
            var emitParams = new ParticleSystem.EmitParams
            {
                position = worldPos + new Vector3(Random.Range(-0.28f, 0.28f), Random.Range(-0.15f, 0.25f), 0f),
                velocity = velocity,
                startColor = tint,
                startSize = Random.Range(0.08f, 0.14f),
                startLifetime = Random.Range(0.35f, 0.55f)
            };
            burstParticles.Emit(emitParams, 1);
        }
    }

    /// <summary>
    /// Hit-stop tập trung trên unscaled time — tự khôi phục đúng tốc độ 1x/2x của BattleManager và không đè Pause.
    /// </summary>
    public void TriggerHitStop(float unscaledDuration = 0.055f)
    {
        if (BattleManager.Instance != null &&
            (BattleManager.Instance.CurrentState == GameState.Paused ||
             BattleManager.Instance.CurrentState == GameState.Finished))
        {
            return;
        }

        float clampedDuration = Mathf.Clamp(unscaledDuration, 0.02f, 0.10f);
        hitStopEndRealtime = Mathf.Max(hitStopEndRealtime, Time.realtimeSinceStartup + clampedDuration);

        if (hitStopCoroutine == null && isActiveAndEnabled)
        {
            hitStopCoroutine = StartCoroutine(HitStopRoutine());
        }
    }

    private IEnumerator HitStopRoutine()
    {
        Time.timeScale = 0.05f;

        while (Time.realtimeSinceStartup < hitStopEndRealtime)
        {
            if (BattleManager.Instance != null && BattleManager.Instance.CurrentState == GameState.Paused)
            {
                hitStopCoroutine = null;
                yield break;
            }
            yield return null;
        }

        hitStopCoroutine = null;
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.ApplyCurrentTimeScale();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    public void TriggerHaptic(long milliseconds = 35)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.Vibrate(milliseconds);
        }
    }

    private void OnDestroy()
    {
        if (particleMaterial != null) Destroy(particleMaterial);
        if (instance == this) instance = null;
    }
}

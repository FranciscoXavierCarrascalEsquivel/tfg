using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Desintegra un UI Image en desenes de trossets de pixels que exploten cap a fora.
/// Cridada: EnemyDestroyFX.Play(enemyPortraitImage, callbackEnAcabar);
/// </summary>
public class EnemyDestroyFX : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────
    private const int   COLS          = 10;
    private const int   ROWS          = 12;
    private const float BURST_DURATION = 1.6f;
    private const float GRAVITY        = -420f;
    private const float INITIAL_PAUSE  = 0.06f; // breu flash de "impacte"

    // ── Factory ──────────────────────────────────────────────────────
    public static void Play(Image portrait, Action onDone, Color? tint = null)
    {
        if (portrait == null || !portrait.enabled)
        {
            onDone?.Invoke();
            return;
        }

        var go = new GameObject("EnemyDestroyFX");
        // Parenjar al mateix nivell que el retrat per coordenades iguals
        go.transform.SetParent(portrait.transform.parent, false);
        go.transform.SetAsLastSibling();

        var fx = go.AddComponent<EnemyDestroyFX>();
        fx.sourcePortrait = portrait;
        fx.onDone         = onDone;
        fx.tint           = tint ?? Color.white;
    }

    // ── Dades internes ───────────────────────────────────────────────
    private Image  sourcePortrait;
    private Action onDone;
    private Color  tint = Color.white;

    private RectTransform[] tileRTs;
    private Vector2[]       velocities;
    private float[]         angularVels;
    private Graphic[]       graphics;

    private void Start() => StartCoroutine(Run());

    // ── Rutina principal ─────────────────────────────────────────────
    private IEnumerator Run()
    {
        RectTransform srcRT   = sourcePortrait.rectTransform;
        Vector2       srcSize = srcRT.rect.size;

        // ── Calcul UV de l'sprite ────────────────────────────────────
        Sprite  sp  = sourcePortrait.sprite;
        Texture tex = sp != null ? sp.texture : null;
        Rect    uv  = new Rect(0, 0, 1, 1); // default si no hi ha sprite

        if (sp != null && tex != null)
        {
            uv = new Rect(
                sp.textureRect.x      / tex.width,
                sp.textureRect.y      / tex.height,
                sp.textureRect.width  / tex.width,
                sp.textureRect.height / tex.height
            );
        }

        // El contenidor ocupa exactament el mateix espai que el retrat
        var selfRT = gameObject.AddComponent<RectTransform>();
        selfRT.anchorMin        = srcRT.anchorMin;
        selfRT.anchorMax        = srcRT.anchorMax;
        selfRT.pivot            = srcRT.pivot;
        selfRT.anchoredPosition = srcRT.anchoredPosition;
        selfRT.sizeDelta        = srcSize;

        // ── Creació de les teselles ──────────────────────────────────
        bool isProjectile = sourcePortrait.GetComponent<ProjectileUI>() != null;

        int     count = COLS * ROWS;
        tileRTs     = new RectTransform[count];
        velocities  = new Vector2[count];
        angularVels = new float[count];
        graphics    = new Graphic[count];

        float tileW = srcSize.x / COLS;
        float tileH = srcSize.y / ROWS;

        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                int idx    = r * COLS + c;
                var tileGo = new GameObject($"P{idx}");
                tileGo.transform.SetParent(transform, false);

                var rt = tileGo.AddComponent<RectTransform>();
                // Mida amb un gap d'1px per donar efecte pixelat
                rt.sizeDelta = new Vector2(tileW - 1f, tileH - 1f);
                // Posicio relativa al centre del contenidor
                rt.anchoredPosition = new Vector2(
                    (c + 0.5f) * tileW - srcSize.x * 0.5f,
                    (r + 0.5f) * tileH - srcSize.y * 0.5f
                );

                // Visual: RawImage amb UV sub-rect si tenim textura
                Graphic g;
                if (tex != null)
                {
                    var raw     = tileGo.AddComponent<RawImage>();
                    raw.texture = tex;
                    raw.uvRect  = new Rect(
                        uv.x + (float)c / COLS * uv.width,
                        uv.y + (float)r / ROWS * uv.height,
                        uv.width  / COLS,
                        uv.height / ROWS
                    );
                    raw.color = tint; // Apliquem el tint aquí
                    g = raw;
                }
                else
                {
                    // Fallback: quadrats de color si no hi ha textura
                    var img   = tileGo.AddComponent<Image>();
                    float hue = (float)(c + r) / (COLS + ROWS) * 0.12f + 0.04f;
                    img.color = Color.HSVToRGB(hue, 0.85f, 1f) * tint;
                    g = img;
                }

                // Velocitat: radial des del centre + component aleatori
                Vector2 dir   = rt.anchoredPosition.magnitude > 0.01f
                    ? rt.anchoredPosition.normalized
                    : UnityEngine.Random.insideUnitCircle.normalized;
                    
                float speed = UnityEngine.Random.Range(80f, 250f);
                if (isProjectile) speed *= 2.2f; // Els projectils exploten molt més fort

                velocities[idx]  = dir * speed
                    + new Vector2(
                        UnityEngine.Random.Range(-50f, 50f),
                        UnityEngine.Random.Range(50f, 200f) * (isProjectile ? 1.5f : 1f)   // bias cap amunt
                      );

                // Girs més ràpids
                angularVels[idx] = UnityEngine.Random.Range(-800f, 800f);
                if (isProjectile) angularVels[idx] *= 1.5f;

                tileRTs[idx]     = rt;
                graphics[idx]    = g;

                // Amaguem les teselles inicialment per veure només l'sprite tremolant
                tileGo.SetActive(false);
            }
        }

        // ── Tremolor (Shake) previ a la explosio ──────────────────────
        float shakeDuration = isProjectile ? 0.12f : 0.45f;
        float elapsedShake = 0f;
        Vector3 origPos = sourcePortrait.rectTransform.anchoredPosition3D;
        
        while (elapsedShake < shakeDuration)
        {
            elapsedShake += Time.deltaTime;
            sourcePortrait.rectTransform.anchoredPosition3D = origPos + (Vector3)(UnityEngine.Random.insideUnitCircle * (isProjectile ? 16f : 8f));
            yield return null;
        }
        sourcePortrait.rectTransform.anchoredPosition3D = origPos;

        // Amaguem el retrat original i mostrem les partícules
        sourcePortrait.enabled = false;

        // --- NOU: So de la explosio al moment que el retrat desapareix (només per l'enemic, no projectils) ---
        if (!isProjectile)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm != null) cm.PlayExplosionSound();
        }

        for (int i = 0; i < count; i++)
        {
            if (tileRTs[i] != null) tileRTs[i].gameObject.SetActive(true);
        }

        // ── Animació de la explosio ──────────────────────────────────
        
        // Per defecte (l'enemic) el terra és la base de la seva imatge, afegim -40 extres perquè caigui una mica més
        float baseFloorY = -srcSize.y / 2f - 40f;
        
        if (isProjectile)
        {
            // Pels projectils, el terra és la líniaKill del combat (l'escenari real) i li afegim offset negatiu perquè baixin més
            var cm = FindFirstObjectByType<CombatManager>();
            float killY = cm != null ? cm.GetDestroyLimitY() : -1200f;
            // Calculem la distància local des de la posició actual del projectil fins a terra, -80 extres
            baseFloorY = killY - srcRT.anchoredPosition.y - 80f;
        }

        // Creem un terra lleugerament irregular per cada partícula perquè quedin "amontonades"
        float[] individualFloors = new float[count];
        for (int i = 0; i < count; i++)
        {
            individualFloors[i] = baseFloorY + UnityEngine.Random.Range(-30f, 30f);
        }

        bool allStopped = false;
        
        // Els projectils tenen el doble de gravetat així cauen més deprés de l'explosió
        float currentGravity = isProjectile ? GRAVITY * 2.2f : GRAVITY;

        while (!allStopped)
        {
            float dt = Time.deltaTime;
            allStopped = true;

            for (int i = 0; i < count; i++)
            {
                if (tileRTs[i] == null) continue;

                // Si no ha tocat el SEU terra, cau i gira
                if (tileRTs[i].anchoredPosition.y > individualFloors[i])
                {
                    allStopped = false;
                    velocities[i].y += currentGravity * dt;
                    tileRTs[i].anchoredPosition += velocities[i] * dt;
                    tileRTs[i].Rotate(0f, 0f, angularVels[i] * dt);
                }
                else
                {
                    // Quan toca el terra, aturem sec i ho deixem a la base exacte
                    Vector2 pos = tileRTs[i].anchoredPosition;
                    pos.y = individualFloors[i];
                    tileRTs[i].anchoredPosition = pos;

                    velocities[i] = Vector2.zero;
                    angularVels[i] = 0f;
                }
            }

            yield return null;
        }

        // NO destruim l'objecte: els pixels queden amuntegats as terra per sempre
        onDone?.Invoke();
    }
}

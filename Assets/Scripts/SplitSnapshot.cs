using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Classe encarregada de realitzar l'efecte de transició de "pantalla trencada" (Split Transition).
/// Captura la pantalla, la divideix en dues meitats a través d'una línia fragmentada/pixelada
/// i en gestiona l'animació d'obertura o tancament (desplaçament lateral amb sacsejada).
/// Dissenyat específicament per a donar un toc retro de "glitch" o fractura en els combats.
/// </summary>
public class SplitSnapshot : MonoBehaviour
{
    [Header("Interfície d'Usuari (UI)")]
    [SerializeField] private RawImage left;
    [SerializeField] private RawImage right;

    [Header("Animació")]
    [SerializeField] private float offscreen = 2500f;

    [Header("Àudio")]
    [SerializeField] private AudioClip shatterSound;

    private Texture2D snapshot;
    private Texture2D leftTex;
    private Texture2D rightTex;

    private float halfWidthPx;

    private Vector2 splitNormal; 

    // =========================================================================
    // API PÚBLICA (Mètodes de control des de l'exterior)
    // =========================================================================

    /// <summary>
    /// Configura les textures i genera el tall fragmentat a partir d'una captura de pantalla.
    /// Aquest mètode realitza la divisió de píxels i simula un efecte pixelat/retro a la vora de la fractura.
    /// </summary>
    /// <param name="tex">Textura de captura de pantalla original.</param>
    public void SetSnapshot(Texture2D tex)
    {
        snapshot = tex;

        int width = tex.width;
        int height = tex.height;

        leftTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        rightTex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color32[] pixels = tex.GetPixels32();
        Color32[] leftPixels = new Color32[pixels.Length];
        Color32[] rightPixels = new Color32[pixels.Length];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 blackBorder = new Color32(0, 0, 0, 255);

        // --- Generació de la Línia Fragmentada Diagonal ---
        float bottomX = width * 0.2f;
        float topX = width * 0.8f;
        
        Vector2 dir = new Vector2(topX - bottomX, height).normalized;
        splitNormal = new Vector2(-dir.y, dir.x); // Perpendicular

        List<Vector2> crackPoints = new List<Vector2>();
        crackPoints.Add(new Vector2(bottomX, 0)); 

        float currentY = 0;
        while (currentY < height)
        {
            float stepY = Random.Range(height * 0.02f, height * 0.12f);
            currentY += stepY;

            if (currentY >= height)
            {
                crackPoints.Add(new Vector2(topX, height)); 
                break;
            }

            float lerpT = currentY / (float)height;
            float baseX = Mathf.Lerp(bottomX, topX, lerpT);

            float xOffset = Random.Range(-width * 0.15f, width * 0.15f);
            
            crackPoints.Add(new Vector2(baseX + xOffset, currentY));
        }

        // --- Tallar els píxels ---
        int currentSegment = 0;
        int borderThickness = (int)(Screen.width * 0.005f); // gruix base per la dispersió
        if (borderThickness < 2) borderThickness = 2;
        
        // Defineix el tamany del pixel simulat (ex. 6x6 píxels reals per cada "píxel gros")
        int retroPixelSize = 6; 

        for (int y = 0; y < height; y++)
        {
            while (currentSegment < crackPoints.Count - 2 && crackPoints[currentSegment + 1].y < y)
            {
                currentSegment++;
            }

            Vector2 p1 = crackPoints[currentSegment];
            Vector2 p2 = crackPoints[currentSegment + 1];

            float t = (y - p1.y) / (p2.y - p1.y);
            int splitX = (int)Mathf.Lerp(p1.x, p2.x, t);

            int startRow = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = startRow + x;
                
                float dist = Mathf.Abs(x - splitX);
                bool isBorder = false;
                
                // Línia central sòlida
                if (dist < 1) 
                {
                    isBorder = true;
                }
                else if (dist <= borderThickness * 3.5f)
                {
                    // Agrupem per "blocs" perquè els píxels espargits es vegin molts grans
                    // Fem servir soroll Perlin fixe basat en els blocs
                    float blockX = Mathf.Floor(x / (float)retroPixelSize);
                    float blockY = Mathf.Floor(y / (float)retroPixelSize);
                    
                    // Valors de gradient ràpids per simular 'Random' però lligats al bloc
                    float noiseVal = Mathf.PerlinNoise(blockX * 0.6f, blockY * 0.6f);

                    // Probabilitat segons distància
                    float chance = 1f - (dist / (borderThickness * 3.5f));
                    chance *= 0.65f; 
                    
                    if (noiseVal < chance)
                    {
                        isBorder = true;
                    }
                }

                if (x <= splitX)
                {
                    leftPixels[index] = isBorder ? blackBorder : pixels[index];
                    rightPixels[index] = clear;
                }
                else
                {
                    leftPixels[index] = clear;
                    rightPixels[index] = isBorder ? blackBorder : pixels[index];
                }
            }
        }

        leftTex.SetPixels32(leftPixels);
        leftTex.Apply(false);
        rightTex.SetPixels32(rightPixels);
        rightTex.Apply(false);

        left.texture = leftTex;
        right.texture = rightTex;

        left.uvRect = new Rect(0f, 0f, 1f, 1f);
        right.uvRect = new Rect(0f, 0f, 1f, 1f);

        var lrt = (RectTransform)left.transform;
        var rrt = (RectTransform)right.transform;

        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = Vector2.zero;

        lrt.anchoredPosition = Vector2.zero;
        rrt.anchoredPosition = Vector2.zero;

        halfWidthPx = lrt.rect.width / 2f; 
        if (halfWidthPx <= 0f) halfWidthPx = Screen.width * 0.5f;
    }

    public bool keepAlive = false;

    /// <summary>
    /// Corrutina per a reproduir l'animació de fractura i separació (Obertura).
    /// </summary>
    public IEnumerator PlayOpen()
    {
        if (shatterSound != null)
        {
            var audioGo = new GameObject("ShatterSound");
            var src = audioGo.AddComponent<AudioSource>();
            src.clip = shatterSound;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(audioGo, shatterSound.length + 0.1f);
        }

        var lrt = (RectTransform)left.transform;
        var rrt = (RectTransform)right.transform;

        Vector2 lBase = Vector2.zero;
        Vector2 rBase = Vector2.zero;

        // ANIM CONFIG
        float preOpenDistance = 25f;   
        float preOpenTime = 0.15f;     
        float holdTime = 0.6f;         
        float snapTime = 0.8f;         

        float shakeStrength = 15f;     
        float shakeSpeed = 30f;        

        Vector2 splitHorizontal = new Vector2(1f, 0f);

        Vector2 lPre   = -splitHorizontal * preOpenDistance;
        Vector2 rPre   = splitHorizontal * preOpenDistance;

        Vector2 lFinal = -splitHorizontal * offscreen;
        Vector2 rFinal = splitHorizontal * offscreen;

        float maxSafeShakeX = Mathf.Max(0f, preOpenDistance * 1.5f); 
        maxSafeShakeX = Mathf.Min(maxSafeShakeX, halfWidthPx * 0.35f); 

        // 1) COP INICIAL
        float t = 0f;
        while (t < preOpenTime)
        {
            float a = EaseOutCubic(t / preOpenTime);

            float shakeM = GetShakeX(Time.unscaledTime, shakeStrength, shakeSpeed, maxSafeShakeX);
            Vector2 shake = splitHorizontal * shakeM;

            lrt.anchoredPosition = Vector2.LerpUnclamped(lBase, lPre, a) + shake;
            rrt.anchoredPosition = Vector2.LerpUnclamped(rBase, rPre, a) - shake;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // 2) TENSİÓ
        float holdT = 0f;
        while (holdT < holdTime)
        {
            float shakeM = GetShakeX(Time.unscaledTime, shakeStrength * 1.5f, shakeSpeed, maxSafeShakeX);
            Vector2 shake = splitHorizontal * shakeM;

            lrt.anchoredPosition = lPre + shake;
            rrt.anchoredPosition = rPre - shake;

            holdT += Time.unscaledDeltaTime;
            yield return null;
        }

        // 3) SNAP
        t = 0f;
        while (t < snapTime)
        {
            float a = EaseInCubic(t / snapTime);

            lrt.anchoredPosition = Vector2.LerpUnclamped(lPre, lFinal, a);
            rrt.anchoredPosition = Vector2.LerpUnclamped(rPre, rFinal, a);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        lrt.anchoredPosition = lFinal;
        rrt.anchoredPosition = rFinal;

        if (!keepAlive)
        {
            Cleanup();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Corrutina per a reproduir l'animació inversa, on les dues meitats es tornen a ajuntar
    /// lentament fins a tancar la pantalla completa (Tancament).
    /// </summary>
    public IEnumerator PlayClose()
    {
        var lrt = (RectTransform)left.transform;
        var rrt = (RectTransform)right.transform;

        Vector2 splitHorizontal = new Vector2(1f, 0f);
        Vector2 lFinal = -splitHorizontal * offscreen;
        Vector2 rFinal = splitHorizontal * offscreen;
        Vector2 lTarget = Vector2.zero;
        Vector2 rTarget = Vector2.zero;

        // ANIM CONFIG TANCAMENT: Ara és bastant més lent (1.5 segons en comptes de 0.5)
        float snapTime = 1.5f;

        // Fes sonar el vidre JUST al començar l'animació de tornar a unir-se
        if (shatterSound != null)
        {
            var audioGo = new GameObject("ShatterSound");
            var src = audioGo.AddComponent<AudioSource>();
            src.clip = shatterSound;
            src.pitch = 0.6f; // Un so més greu per tancar
            src.spatialBlend = 0f;
            src.Play();
            Destroy(audioGo, shatterSound.length + 0.1f);
        }

        float t = 0f;
        while (t < snapTime)
        {
            // Entra a poc a poc i s'agafa al mig amb suavitat
            float a = EaseOutCubic(t / snapTime);
            lrt.anchoredPosition = Vector2.LerpUnclamped(lFinal, lTarget, a);
            rrt.anchoredPosition = Vector2.LerpUnclamped(rFinal, rTarget, a);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        lrt.anchoredPosition = lTarget;
        rrt.anchoredPosition = rTarget;

        yield return new WaitForSecondsRealtime(0.5f); // Pausa més llarga abans de desactivar-se per donar temps a veure's sencer
    }

    // =========================================================================
    // MÈTODES MATEMÀTICS I DE SUPORT (Eases i sacsejada)
    // =========================================================================

    private static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        float p = 1f - x;
        return 1f - p * p * p;
    }

    private static float EaseInCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x;
    }

    /// <summary>
    /// Calcola un desplaçament en l'eix X utilitzant Perlin Noise per simular una sacsejada orgànica.
    /// </summary>
    private static float GetShakeX(float time, float strength, float speed, float maxAbs)
    {
        float sx = (Mathf.PerlinNoise(time * speed, 0.123f) - 0.5f) * 2f;
        float v = sx * strength;
        if (maxAbs > 0f) v = Mathf.Clamp(v, -maxAbs, maxAbs);
        return v;
    }

    /// <summary>
    /// Neteja i alliberament de recursos gràfics per a evitar pèrdues de memòria (Memory Leaks).
    /// </summary>
    public void Cleanup()
    {
        if (snapshot != null)
        {
            Destroy(snapshot);
            snapshot = null;
        }
        if (leftTex != null)
        {
            Destroy(leftTex);
            leftTex = null;
        }
        if (rightTex != null)
        {
            Destroy(rightTex);
            rightTex = null;
        }
    }
}

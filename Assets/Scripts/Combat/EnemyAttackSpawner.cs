using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyAttackSpawner : MonoBehaviour
{
    [SerializeField] private RectTransform projectilesRoot;
    [SerializeField] private RectTransform arenaRect;

    private GameObject projectilePrefab;
    private EnemyAttackPattern currentPattern;

    public void Configure(GameObject prefab, EnemyAttackPattern pattern)
    {
        projectilePrefab = prefab;
        currentPattern = pattern;
    }

    public IEnumerator Run(float duration)
    {
        switch (currentPattern)
        {
            case EnemyAttackPattern.HorizontalWaves:
                yield return HorizontalWavesRoutine(duration);
                break;
            case EnemyAttackPattern.TargetedHoming:
                yield return TargetedHomingRoutine(duration);
                break;
            case EnemyAttackPattern.CircleBurst:
                yield return CircleBurstRoutine(duration);
                break;
            case EnemyAttackPattern.DiagonalCross:
                yield return DiagonalCrossRoutine(duration);
                break;
            case EnemyAttackPattern.FastMeteors:
                yield return FastMeteorsRoutine(duration);
                break;
            case EnemyAttackPattern.SnakeWaves:
                yield return SnakeWavesRoutine(duration);
                break;
            case EnemyAttackPattern.RandomDrop:
            default:
                yield return RandomDropRoutine(duration);
                break;
        }
    }

    // ===================================
    // 1. RANDOM DROP (Classic - ara totalment irregular)
    // ===================================
    private IEnumerator RandomDropRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                var go = Instantiate(projectilePrefab, projectilesRoot);
                var rt = go.GetComponent<RectTransform>();
                var proj = go.GetComponent<ProjectileUI>();

                if (rt && proj)
                {
                    float xLimit = arenaRect.rect.width / 3f;
                    float x = Random.Range(-xLimit, xLimit);
                    float y = arenaRect.rect.height / 2f + Random.Range(10f, 60f); // Neix a alçades lleugerament diferents

                    rt.anchoredPosition = new Vector2(x, y);
                    
                    // Direcció meitat recte, meitat esbiaixada aleatòria, a velocitat variable
                    Vector2 dir = new Vector2(Random.Range(-0.2f, 0.2f), -1f).normalized;
                    proj.Init(dir, Random.Range(150f, 350f));
                }
            }
            float wait = Random.Range(0.2f, 0.6f); // Pausa impredictible
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // ===================================
    // 2. HORIZONTAL WAVES (Falling from sky - irregular i forats aleatoris)
    // ===================================
    private IEnumerator HorizontalWavesRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                float w = arenaRect.rect.width;
                // Agafem franges irregulars, cap d'elles properes exactament a la paret extrema
                float[] waveX = new float[] { -w/3.5f + Random.Range(-20f, 20f), Random.Range(-15f, 15f), w/3.5f + Random.Range(-20f, 20f)};
                
                int safeZone = Random.Range(0, 3);
                int anotherSafeZone = (Random.value > 0.7f) ? Random.Range(0, 3) : -1; // 30% de probabilitat de ser una onada fluixa amb 2 forats

                for (int i = 0; i < 3; i++)
                {
                    if (i == safeZone || i == anotherSafeZone) continue; 
                    
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    var rt = go.GetComponent<RectTransform>();
                    var proj = go.GetComponent<ProjectileUI>();

                    if (rt && proj)
                    {
                        // Altura desalineada perquè no semblin una màquina perfecta
                        rt.anchoredPosition = new Vector2(waveX[i], arenaRect.rect.height / 2f + 20f + Random.Range(0f, 60f));
                        proj.Init(Vector2.down, Random.Range(220f, 280f)); // Caigudes a velocitat variable
                    }
                }
            }
            
            float wait = Random.Range(0.6f, 1.1f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // ===================================
    // 3. TARGETED HOMING (Més imprecís, perillos i amb ràfegues)
    // ===================================
    private IEnumerator TargetedHomingRoutine(float duration)
    {
        float t = 0f;
        var hands = FindObjectsByType<HandController>(FindObjectsSortMode.None);
        
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect && hands.Length > 0)
            {
                var targetHand = hands[Random.Range(0, hands.Length)];
                
                // Un cop posat l'ull, dispara entre 1 i 2 bales seguides directes
                int rafagas = Random.Range(1, 3); 
                for(int i = 0; i < rafagas; i++)
                {
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    var rt = go.GetComponent<RectTransform>();
                    var proj = go.GetComponent<ProjectileUI>();

                    if (rt && proj && targetHand != null)
                    {
                        float xLimit = arenaRect.rect.width / 3f;
                        float startX = Random.Range(-xLimit, xLimit);
                        float startY = arenaRect.rect.height / 2f + Random.Range(30f, 100f);
                        rt.anchoredPosition = new Vector2(startX, startY);

                        var handRt = targetHand.GetComponent<RectTransform>();
                        if (handRt != null)
                        {
                            // Afegeix un error mínim per ser més estable i llegible
                            Vector2 error = new Vector2(Random.Range(-15f, 15f), Random.Range(-15f, 15f));
                            Vector2 targetPos = handRt.anchoredPosition + error; 
                            
                            Vector2 dir = (targetPos - rt.anchoredPosition).normalized;
                            if (dir.y > 0) dir.y = -dir.y; // Forces fall down
                            
                            proj.Init(dir, Random.Range(180f, 350f)); // Velocitat de l'assassí sorpresa
                        }
                    }
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                }
            }
            
            float wait = Random.Range(0.8f, 1.4f); // Més distanciats per respirar
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // ===================================
    // 4. CIRCLE BURST (Estable i regular formant ventall inferior)
    // ===================================
    private IEnumerator CircleBurstRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                int numProjectiles = Random.Range(4, 7); // Menys metralla (4-6)
                
                // Mantenim angles fixos però no massa estirats als costats (225º a 315º) per estabilitat
                float arrelAngle = 225f; 
                float topAngle = 315f;
                float angleStep = (topAngle - arrelAngle) / (numProjectiles - 1); 

                for (int i = 0; i < numProjectiles; i++)
                {
                    // 15% possibilitat d'una bala fantasma per donar forats intel·ligents
                    if (Random.value < 0.15f) continue;

                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    var rt = go.GetComponent<RectTransform>();
                    var proj = go.GetComponent<ProjectileUI>();

                    if (rt && proj)
                    {
                        // Surt fix centrat per donar idea clara de forma sota-paraigo
                        rt.anchoredPosition = new Vector2(0f, arenaRect.rect.height / 2f + Random.Range(0, 30)); 
                        
                        float rad = (arrelAngle + i * angleStep) * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                        proj.Init(dir, Random.Range(180f, 220f));
                    }
                }
            }
            float wait = Random.Range(1.2f, 1.8f); // Pausa més llarga entre bursts!
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // ===================================
    // 5. DIAGONAL CROSS (Posicions de cruilla aleatòries i asimètriques)
    // ===================================
    private IEnumerator DiagonalCrossRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                float w = arenaRect.rect.width / 2.2f; // Limitem Marge perquè tampoc neixin massa al límit exterior
                float h = arenaRect.rect.height / 2f;
                
                // Marge més segur endins
                Vector2 cornerL = new Vector2(-w + Random.Range(10, 50), h + Random.Range(10, 80));
                Vector2 cornerR = new Vector2(w - Random.Range(10, 50), h + Random.Range(10, 80));
                
                Vector2[] corners = new Vector2[] { cornerL, cornerR };

                foreach (var corner in corners)
                {
                    // Cada cantó pot escupir entre 1 i 2 línies emparellades
                    int rafagaCr = Random.Range(1, 3);
                    for (int n = 0; n < rafagaCr; n++)
                    {
                        var go = Instantiate(projectilePrefab, projectilesRoot);
                        var rt = go.GetComponent<RectTransform>();
                        var proj = go.GetComponent<ProjectileUI>();

                        if (rt && proj)
                        {
                            rt.anchoredPosition = corner + new Vector2(Random.Range(-20, 20), Random.Range(-20, 20)); // Vora difusa
                            
                            // Dispara exclusivament cap a dins de l'espai visible d'abaix
                            float endX = (corner.x < 0) ? Random.Range(0, w - 20) : Random.Range(-w + 20, 0);
                            Vector2 targetBot = new Vector2(endX, -h - 150f);
                            
                            proj.Init((targetBot - rt.anchoredPosition).normalized, Random.Range(200f, 320f));
                        }
                    }
                }
            }
            float wait = Random.Range(0.7f, 1.4f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // ===================================
    // 6. FAST METEORS (Meteors menys saturats i menys erràtics lateralment)
    // ===================================
    private IEnumerator FastMeteorsRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                int meteors = 1; // Un per un caient contínuament
                for (int m = 0; m < meteors; m++)
                {
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    var rt = go.GetComponent<RectTransform>();
                    var proj = go.GetComponent<ProjectileUI>();

                    if (rt && proj)
                    {
                        float xLimit = arenaRect.rect.width / 4f; // Centralitzat per poder parar-los
                        float x = Random.Range(-xLimit, xLimit);
                        float y = arenaRect.rect.height / 2f + Random.Range(30f, 80f); 

                        rt.anchoredPosition = new Vector2(x, y);
                        
                        // Diagonal gairebé recta a plom
                        float dx = Random.Range(-0.1f, 0.1f);

                        Vector2 dir = new Vector2(dx, -1f).normalized;
                        proj.Init(dir, Random.Range(300f, 550f)); // Ràpids però previsibles
                    }
                }
            }
            
            float wait = Random.Range(0.2f, 0.6f); // Reducció dràstica del volum (+ temps dedelay)
            yield return new WaitForSeconds(wait); 
            t += wait;
        }
    }

    // ===================================
    // 7. SNAKE WAVES (Serpentegen fora de ritme amb velocitats pròpies)
    // ===================================
    private IEnumerator SnakeWavesRoutine(float duration)
    {
        float t = 0f;
        float angleSweep = Random.Range(0f, 5f); // No comença sempre al centre de la S
        float variabilitatFrequencia = Random.Range(0.3f, 1.2f); // Algunes serps són molt denses i espesses, altres molt distants
        
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                var go = Instantiate(projectilePrefab, projectilesRoot);
                var rt = go.GetComponent<RectTransform>();
                var proj = go.GetComponent<ProjectileUI>();

                if (rt && proj)
                {
                    // No deixis que se'n vagi als marges, força-ho al centre
                    float startX = Mathf.Sin(angleSweep) * (arenaRect.rect.width / 4f) + Random.Range(-10f, 10f);
                    float startY = arenaRect.rect.height / 2f + Random.Range(10f, 50f);
                    
                    rt.anchoredPosition = new Vector2(startX, startY);
                    
                    // Màxima obertura horitzontal 0.25f (molt poqueta obertura lateral pr garatizar estabilitat)
                    float dirX = Mathf.Sin(angleSweep + Random.Range(-0.2f, 0.2f)) * 0.25f;
                    Vector2 dir = new Vector2(dirX, -1f).normalized;
                    proj.Init(dir, Random.Range(180f, 250f));
                }
            }
            
            angleSweep += variabilitatFrequencia;
            // Delay bastant més lent! Així cauen de 1 en 1 o molt pausadament
            float wait = Random.Range(0.3f, 0.5f); 
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }
}

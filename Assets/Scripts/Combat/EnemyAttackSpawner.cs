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
                yield return HorizontalWavesRoutine(duration, false);
                break;
            case EnemyAttackPattern.HorizontalWavesSpinning:
                yield return HorizontalWavesRoutine(duration, true);
                break;

            case EnemyAttackPattern.CircleBurst:
                yield return CircleBurstRoutine(duration, false);
                break;
            case EnemyAttackPattern.CircleBurstSpinning:
                yield return CircleBurstRoutine(duration, true);
                break;

            case EnemyAttackPattern.DiagonalCross:
                yield return DiagonalCrossRoutine(duration, false);
                break;
            case EnemyAttackPattern.DiagonalCrossSpinning:
                yield return DiagonalCrossRoutine(duration, true);
                break;

            case EnemyAttackPattern.FastMeteors:
                yield return FastMeteorsRoutine(duration, false);
                break;
            case EnemyAttackPattern.FastMeteorsSpinning:
                yield return FastMeteorsRoutine(duration, true);
                break;

            case EnemyAttackPattern.SnakeWaves:
                yield return SnakeWavesRoutine(duration, false);
                break;
            case EnemyAttackPattern.SnakeWavesSpinning:
                yield return SnakeWavesRoutine(duration, true);
                break;

            case EnemyAttackPattern.RandomDropSpinning:
                yield return RandomDropRoutine(duration, true);
                break;
            case EnemyAttackPattern.RandomDrop:
            default:
                yield return RandomDropRoutine(duration, false);
                break;
        }
    }

    // ===================================
    // 1. RANDOM DROP (Classic - ara totalment irregular)
    // ===================================
    private IEnumerator RandomDropRoutine(float duration, bool spinning)
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
                    float y = arenaRect.rect.height / 2f + Random.Range(10f, 60f);

                    rt.anchoredPosition = new Vector2(x, y);
                    Vector2 dir = new Vector2(Random.Range(-0.2f, 0.2f), -1f).normalized;
                    proj.Init(dir, Random.Range(150f, 350f), 0, 0, spinning);
                }
            }
            float wait = Random.Range(0.2f, 0.6f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // ===================================
    // 2. HORIZONTAL WAVES (Redissenyat: Cascades seguibles)
    // ===================================
    private IEnumerator HorizontalWavesRoutine(float duration, bool spinning)
    {
        float t = 0f;
        float screenW = arenaRect.rect.width * 0.8f;
        float stepX = screenW / 6f;
        int currentStep = 0;

        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                float x = -screenW/2f + (currentStep % 7) * stepX;
                currentStep++;

                var go = Instantiate(projectilePrefab, projectilesRoot);
                var rt = go.GetComponent<RectTransform>();
                var proj = go.GetComponent<ProjectileUI>();

                if (rt && proj)
                {
                    rt.anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + 40f);
                    proj.Init(Vector2.down, 250f, 0, 0, spinning);
                }
            }
            
            float wait = 0.4f; 
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // REMOVED: TargetedHomingRoutine (No agrada a l'usuari)

    // ===================================
    // 4. CIRCLE BURST (Més pocs projectils + opció spinning)
    // ===================================
    private IEnumerator CircleBurstRoutine(float duration, bool spinning)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                int numProjectiles = Random.Range(3, 5); // Reduït: 3-4 projectils
                
                float startAngle = 252.5f; 
                float endAngle = 287.5f;
                float angleStep = (endAngle - startAngle) / (numProjectiles - 1); 

                for (int i = 0; i < numProjectiles; i++)
                {
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    var rt = go.GetComponent<RectTransform>();
                    var proj = go.GetComponent<ProjectileUI>();

                    if (rt && proj)
                    {
                        rt.anchoredPosition = new Vector2(0f, arenaRect.rect.height / 2f + 20f); 
                        float rad = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                        proj.Init(dir, 200f, 0, 0, spinning);
                    }
                }
            }
            float wait = Random.Range(1.3f, 2.0f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // ===================================
    // 5. DIAGONAL CROSS (Cruilles amb suport spinning)
    // ===================================
    private IEnumerator DiagonalCrossRoutine(float duration, bool spinning)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                float w = arenaRect.rect.width / 2.2f;
                float h = arenaRect.rect.height / 2f;
                
                // Afegim irregularitat a les cantonades
                Vector2 cornerL = new Vector2(-w + Random.Range(10f, 60f), h + Random.Range(30f, 80f));
                Vector2 cornerR = new Vector2(w - Random.Range(10f, 60f), h + Random.Range(30f, 80f));
                Vector2[] corners = new Vector2[] { cornerL, cornerR };

                foreach (var corner in corners)
                {
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    var rt = go.GetComponent<RectTransform>();
                    var proj = go.GetComponent<ProjectileUI>();

                    if (rt && proj)
                    {
                        rt.anchoredPosition = corner;
                        // El punt de destí també és més irregular (no sempre al centre)
                        float offsetTargetX = Random.Range(-150f, 150f);
                        float endX = (corner.x < 0) ? (50f + offsetTargetX) : (-50f + offsetTargetX);
                        
                        Vector2 target = new Vector2(endX, -h - 200f);
                        Vector2 dir = (target - rt.anchoredPosition).normalized;
                        
                        // Velocitat una mica variable
                        float randomSpeed = Random.Range(220f, 320f);
                        proj.Init(dir, randomSpeed, 0, 0, spinning);
                    }
                    
                    // Petita pausa irregular entre el naixement del costat esquerre i dret
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
                }
            }
            float wait = Random.Range(0.7f, 1.3f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // ===================================
    // 6. FAST METEORS (Velocitat amb opció spinning)
    // ===================================
    private IEnumerator FastMeteorsRoutine(float duration, bool spinning)
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
                    float xLimit = arenaRect.rect.width / 4f;
                    float x = Random.Range(-xLimit, xLimit);
                    rt.anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + 50f);
                    proj.Init(Vector2.down, 450f, 0, 0, spinning);
                }
            }
            float wait = 0.4f;
            yield return new WaitForSeconds(wait); 
            t += wait;
        }
    }

    // ===================================
    // 7. SNAKE WAVES (Zig-zag real basat en el projectil)
    // ===================================
    private IEnumerator SnakeWavesRoutine(float duration, bool spinning)
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
                    float x = Random.Range(-arenaRect.rect.width/4f, arenaRect.rect.width/4f);
                    rt.anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + 30f);
                    // Zig-zag el doble de pronunciat (amplitud 120) i mantenint la velocitat lenta
                    proj.Init(Vector2.down, 200f, 4.5f, 120f, spinning);
                }
            }
            
            float wait = 0.8f; 
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }
}

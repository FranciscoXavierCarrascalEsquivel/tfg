using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Generador i Coreògraf de Patrons d'Atac Enemics (EnemyAttackSpawner).
/// Aquest component és el "cervell ofensiu" del combat, encarregat d'instanciar i dirigir
/// les coreografies de projectils que ha d'esquivar o blocar el jugador.
/// Rep la informació del prefab actiu i l'enumerat de patró triat per la criatura (`EnemyAttackPattern`),
/// i executa corrutines asíncrones de llançament amb temporitzadors de seguretat.
/// 
/// PATRONS DE BULLET HELL DEL TFG:
/// 1. **RandomDrop**: Pluja irregular de projectils lents sobre l'eix horitzontal.
/// 2. **HorizontalWaves**: Cascades lineals progressives des de l'esquerra a la dreta.
/// 3. **CircleBurst**: Anell d'esclat concèntric reduït de 3 o 4 bales cap a l'avatar de la mà.
/// 4. **DiagonalCross**: Llançaments creuats de cantonada a cantonada amb irregularitats horitzontals.
/// 5. **FastMeteors**: Balas de gran velocitat rectes cap avall per a reflexos ràpids.
/// 6. **SnakeWaves**: Projectils lineals en ones de zig-zag horitzontals molt marcades de tipus serp.
/// 7. **RainWithRed / RapidFireRed**: Balas estàndard barrejades amb projectils vermells que no permeten Parry.
/// 8. **RedHomingBarrage**: Persecució directa de les mans de forma dinàmica asíncrona per ràtio Lerp.
/// 9. **RedSweepWall**: Murs de projectils vermells densos amb dos petits espais segurs per forçar parades d'escut.
/// 10. **SimpleStraightLines**: Columnes verticals uniformes des del sostre de l'arena.
/// 11. **AlternatingSides**: Ràfegues alternes de costat esquerre a costat dret.
/// 12. **SideSweepers**: Llançaments horitzontals des de les vores laterals de l'arena (dreta o esquerra).
/// 13. **ExpandingCross**: Fonts de projectils de 8 direccions concèntriques naixent des del centre.
/// </summary>
public class EnemyAttackSpawner : MonoBehaviour
{
    [SerializeField] private RectTransform projectilesRoot; // Contenidor pare per a organitzar jeràrquicament les bales
    [SerializeField] private RectTransform arenaRect; // Marc físic limitador de l'arena de combat

    private GameObject projectilePrefab;
    private EnemyAttackPattern currentPattern;
    private ParryZone[] cachedHands; // Desa referències a les mans actives del jugador per estalviar cerques per torn

    /// <summary>
    /// Enllaça les referències del projectil a utilitzar i el patró coreogràfic des del CombatManager.
    /// </summary>
    public void Configure(GameObject prefab, EnemyAttackPattern pattern)
    {
        projectilePrefab = prefab;
        currentPattern = pattern;
    }

    /// <summary>
    /// Corrutina mestra que fa de selector de patrons i inicia les sub-rutines corresponents.
    /// </summary>
    /// <param name="duration">Temps límit de duració de l'atac (en segons).</param>
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
            
            case EnemyAttackPattern.RainWithRed:
                yield return RainWithRedRoutine(duration, false);
                break;
            case EnemyAttackPattern.RainWithRedSpinning:
                yield return RainWithRedRoutine(duration, true);
                break;

            case EnemyAttackPattern.RapidFireRed:
                yield return RapidFireRedRoutine(duration, false);
                break;
            case EnemyAttackPattern.RapidFireRedSpinning:
                yield return RapidFireRedRoutine(duration, true);
                break;

            case EnemyAttackPattern.RedHomingBarrage:
                yield return RedHomingBarrageRoutine(duration, false);
                break;
            case EnemyAttackPattern.RedHomingBarrageSpinning:
                yield return RedHomingBarrageRoutine(duration, true);
                break;

            case EnemyAttackPattern.RedSweepWall:
                yield return RedSweepWallRoutine(duration);
                break;

            case EnemyAttackPattern.SimpleStraightLines:
                yield return SimpleStraightLinesRoutine(duration);
                break;
            case EnemyAttackPattern.AlternatingSides:
                yield return AlternatingSidesRoutine(duration);
                break;
            case EnemyAttackPattern.SideSweepers:
                yield return SideSweepersRoutine(duration);
                break;
            case EnemyAttackPattern.ExpandingCross:
                yield return ExpandingCrossRoutine(duration);
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

    // =========================================================================
    // 1. RANDOM DROP (Pluja clàssica irregular)
    // =========================================================================
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
                    // Dividim el límit de l'arena per allotjar el naixement sense talls a les cantonades
                    float xLimit = arenaRect.rect.width / 3f;
                    float x = Random.Range(-xLimit, xLimit);
                    float y = arenaRect.rect.height / 2f + Random.Range(10f, 60f); // Spawneja per sobre de la capçalera de l'arena

                    rt.anchoredPosition = new Vector2(x, y);
                    Vector2 dir = new Vector2(Random.Range(-0.2f, 0.2f), -1f).normalized; // Caiguda vertical amb petita desviació lateral
                    proj.Init(dir, Random.Range(150f, 350f), 0, 0, spinning);
                }
            }
            float wait = Random.Range(0.2f, 0.6f); // Ràtio de naixement irregular (asíncron)
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 2. HORIZONTAL WAVES (Cascades d'ones lineals)
    // =========================================================================
    private IEnumerator HorizontalWavesRoutine(float duration, bool spinning)
    {
        float t = 0f;
        float screenW = arenaRect.rect.width * 0.8f;
        float stepX = screenW / 6f; // Dividim l'ample en 6 passos per fer barrits suaus
        int currentStep = 0;

        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                // Calculem les línies de forma progressiva utilitzant mòduls
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

    // =========================================================================
    // 4. CIRCLE BURST (Anells d'esclats de bales des del mig)
    // =========================================================================
    private IEnumerator CircleBurstRoutine(float duration, bool spinning)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                int numProjectiles = Random.Range(3, 5); // Pocs projectils per garantir un combat net i esquivable
                
                // Limitem l'arc de l'anell entre 252.5 i 287.5 graus (cap a la part inferior)
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

    // =========================================================================
    // 5. DIAGONAL CROSS (Cantonades creuades irregulars)
    // =========================================================================
    private IEnumerator DiagonalCrossRoutine(float duration, bool spinning)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                float w = arenaRect.rect.width / 2.2f;
                float h = arenaRect.rect.height / 2f;
                
                // Cantonades amb petites desviacions aleatòries (irregularitats de naixement)
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
                        
                        // Creuem la trajectòria cap a la cantonada oposada per simular l'esquema de creu
                        float offsetTargetX = Random.Range(-150f, 150f);
                        float endX = (corner.x < 0) ? (50f + offsetTargetX) : (-50f + offsetTargetX);
                        
                        Vector2 target = new Vector2(endX, -h - 200f);
                        Vector2 dir = (target - rt.anchoredPosition).normalized;
                        
                        float randomSpeed = Random.Range(220f, 320f);
                        proj.Init(dir, randomSpeed, 0, 0, spinning);
                    }
                    
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
                }
            }
            float wait = Random.Range(0.7f, 1.3f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 6. FAST METEORS (Caigudes verticals a alta velocitat)
    // =========================================================================
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
                    proj.Init(Vector2.down, 450f, 0, 0, spinning); // Caiguda ràpida (450 velocitat)
                }
            }
            float wait = 0.4f;
            yield return new WaitForSeconds(wait); 
            t += wait;
        }
    }

    // =========================================================================
    // 7. SNAKE WAVES (Zig-Zag vertical pronunciat)
    // =========================================================================
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
                    
                    // Inicialitzem el projectil amb ones de zig-zag de gran amplitud (amplitud = 120, freqüència = 4.5)
                    proj.Init(Vector2.down, 200f, 4.5f, 120f, spinning);
                }
            }
            
            float wait = 0.8f; 
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 8. RAIN WITH RED (Pluja mixta estàndard + dany vermell prohibit)
    // =========================================================================
    private IEnumerator RainWithRedRoutine(float duration, bool spinning)
    {
        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                var go = Instantiate(projectilePrefab, projectilesRoot);
                var proj = go.GetComponent<ProjectileUI>();
                if (proj)
                {
                    float x = Random.Range(-arenaRect.rect.width/3f, arenaRect.rect.width/3f);
                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + 50f);
                    
                    // Probabilitat del 35% de generar una bala vermella prohibida
                    bool isRed = Random.value < 0.35f;
                    proj.Init(Vector2.down, 280f, 0, 0, spinning, isRed);
                }
            }
            float wait = 0.25f;
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 9. RED HOMING BARRAGE (Ràfegues vermelles de seguiment)
    // =========================================================================
    private IEnumerator RedHomingBarrageRoutine(float duration, bool spinning)
    {
        if (cachedHands == null || cachedHands.Length == 0) 
            cachedHands = FindObjectsByType<ParryZone>(FindObjectsSortMode.None);

        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                var go = Instantiate(projectilePrefab, projectilesRoot);
                var proj = go.GetComponent<ProjectileUI>();
                if (proj)
                {
                    float x = Random.Range(-arenaRect.rect.width / 4f, arenaRect.rect.width / 4f);
                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + 50f);
                    
                    proj.Init(Vector2.down, 250f, 0, 0, spinning, true);
                    
                    // Assignem una de les mans actives com a objectiu de seguiment
                    if (cachedHands != null && cachedHands.Length > 0)
                    {
                        var targetHand = cachedHands[Random.Range(0, cachedHands.Length)];
                        proj.SetHoming(targetHand.transform, 3.8f); // Força de seguiment ràpida (3.8)
                    }
                }
            }
            float wait = 0.6f;
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 10. RED SWEEP WALL (Murs compactes amb dobles obertures de seguretat)
    // =========================================================================
    private IEnumerator RedSweepWallRoutine(float duration)
    {
        float t = 0f;
        float screenW = arenaRect.rect.width * 0.95f;

        while (t < duration)
        {
            // Instanciem 16 projectils formant una línia horitzontal compacta
            int num = 16;
            int hole1 = Random.Range(1, 6);   // Obertura lliure a l'esquerra
            int hole2 = Random.Range(10, 15); // Obertura lliure a la dreta
            
            float step = screenW / (num - 1);

            for (int i = 0; i < num; i++)
            {
                if (i == hole1 || i == hole2) continue; // Deixem les línies lliures per a les dues mans

                var go = Instantiate(projectilePrefab, projectilesRoot);
                float x = -screenW/2f + i * step;
                go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + 50f);
                
                go.GetComponent<ProjectileUI>().Init(Vector2.down, 230f, 0, 0, false, true);
            }

            float wait = 2.8f; // Interval ampli perquè no se solapin verticalment les franges de murs
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 11. RAPID FIRE RED (Tir ràpid vermell persecutor)
    // =========================================================================
    private IEnumerator RapidFireRedRoutine(float duration, bool spinning)
    {
        if (cachedHands == null || cachedHands.Length == 0) 
            cachedHands = FindObjectsByType<ParryZone>(FindObjectsSortMode.None);

        float t = 0f;
        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                var go = Instantiate(projectilePrefab, projectilesRoot);
                var proj = go.GetComponent<ProjectileUI>();
                if (proj)
                {
                    float x = Random.Range(-240f, 240f);
                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + 50f);
                    
                    proj.Init(Vector2.down, 500f, 0, 0, spinning, true); // Velocitat punta extrem (500)
                    
                    if (cachedHands != null && cachedHands.Length > 0)
                    {
                        var targetHand = cachedHands[Random.Range(0, cachedHands.Length)];
                        proj.SetHoming(targetHand.transform, 4.8f); // Seguiment a gran velocitat de gir (4.8)
                    }
                }
            }
            float wait = 0.4f;
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 12. SIMPLE STRAIGHT LINES (Columnes verticals clàssiques)
    // =========================================================================
    private IEnumerator SimpleStraightLinesRoutine(float duration)
    {
        float t = 0f;
        float screenW = arenaRect.rect.width * 0.5f;

        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                int cols = Random.Range(1, 3);
                float step = screenW / cols;
                float offset = Random.Range(-step / 3f, step / 3f);

                for (int i = 0; i <= cols; i++)
                {
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    float x = -screenW / 2f + i * step + offset;
                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + 50f);
                    go.GetComponent<ProjectileUI>().Init(Vector2.down, 250f, 0, 0, false);
                }
            }
            float wait = 1.2f;
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 13. ALTERNATING SIDES (Ràfegues alternes de costats)
    // =========================================================================
    private IEnumerator AlternatingSidesRoutine(float duration)
    {
        float t = 0f;
        bool leftSide = true;

        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                int numProjectiles = 2;
                float halfW = arenaRect.rect.width / 2f;

                for (int i = 0; i < numProjectiles; i++)
                {
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    // Llimitem dinàmicament l'ample de spawn a un costat segons el flag d'alternança
                    float minX = leftSide ? -halfW + 20f : 20f;
                    float maxX = leftSide ? -20f : halfW - 20f;
                    float x = Random.Range(minX, maxX);
                    
                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, arenaRect.rect.height / 2f + Random.Range(20f, 60f));
                    go.GetComponent<ProjectileUI>().Init(Vector2.down, 280f, 0, 0, false);
                }
            }
            leftSide = !leftSide; // Canvi de costat
            float wait = 0.8f;
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 14. SIDE SWEEPERS (Llançaments des de les vores de l'arena Y)
    // =========================================================================
    private IEnumerator SideSweepersRoutine(float duration)
    {
        float t = 0f;
        bool fromLeft = true;

        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                float w = arenaRect.rect.width / 2f;
                float h = arenaRect.rect.height / 2f;

                var go = Instantiate(projectilePrefab, projectilesRoot);
                float startX = fromLeft ? -w - 50f : w + 50f;
                float startY = Random.Range(-h + 80f, h - 80f); // Alçada aleatòria
                go.GetComponent<RectTransform>().anchoredPosition = new Vector2(startX, startY);
                
                // Mirem cap al costat oposat
                Vector2 dir = fromLeft ? Vector2.right : Vector2.left;
                dir.y = Random.Range(-0.1f, 0.1f); // Petita desviació vertical
                dir.Normalize();

                go.GetComponent<ProjectileUI>().Init(dir, 220f, 0, 0, false);
            }
            fromLeft = !fromLeft;
            float wait = 0.25f;
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    // =========================================================================
    // 15. EXPANDING CROSS (Esclats estrellats centrats de 8 braços)
    // =========================================================================
    private IEnumerator ExpandingCrossRoutine(float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            if (projectilePrefab && projectilesRoot && arenaRect)
            {
                // Vector direccionals dels 8 braços estrellats
                Vector2[] dirs = new Vector2[] { 
                    Vector2.up, Vector2.down, Vector2.left, Vector2.right,
                    new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
                    new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized 
                };
                
                float cx = Random.Range(-60f, 60f);
                float cy = Random.Range(-20f, 80f); 
                Vector2 center = new Vector2(cx, cy);

                // Instanciem les 8 bales des del mateix centre de forma estrellada asíncrona
                foreach (var dir in dirs)
                {
                    var go = Instantiate(projectilePrefab, projectilesRoot);
                    go.GetComponent<RectTransform>().anchoredPosition = center;
                    go.GetComponent<ProjectileUI>().Init(dir, 160f, 0, 0, true);
                }
            }
            float wait = 1.0f;
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }
}

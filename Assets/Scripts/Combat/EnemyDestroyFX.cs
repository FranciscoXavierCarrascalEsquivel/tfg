using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component que s'encarrega de fer un efecte visual premium de desintegració de l'enemic.
/// Agafa un component Image de la UI (per exemple, el retrat de l'enemic) i el divideix proceduralment
/// en una graella de petits trossets (píxels o tesel·les) utilitzant RawImages amb sub-rectangles UV.
/// Després, fa tremolar el retrat original i, en el moment de l'explosió, el desactiva i llança totes
/// les partícules volant en una simulació física simple de gravetat fins que toquen a terra de l'arena,
/// on queden amuntegades abans de ser destruïdes.
/// </summary>
public class EnemyDestroyFX : MonoBehaviour
{
    // ── CONFIGURACIÓ DE L'EFECTE ─────────────────────────────────────
    private const int   COLS          = 10;   // Nombre de columnes horitzontals en què es dividirà la imatge
    private const int   ROWS          = 12;   // Nombre de files verticals en què es dividirà la imatge
    private const float BURST_DURATION = 1.6f; // Durada aproximada de l'impuls de l'explosió
    private const float GRAVITY        = -420f; // Força de gravetat cap avall aplicada a cada fragment en px/s^2
    private const float INITIAL_PAUSE  = 0.06f; // Breu pausa de retard per donar impacte abans de la dispersió

    // ── MÈTODE FACTORY PER INSTANCIAR L'EFECTE ────────────────────────
    /// <summary>
    /// Crea una instància temporal del sistema d'efectes a partir d'una imatge origen.
    /// Això permet fer que qualsevol enemic o projectil es trenqui en mil bocins fàcilment amb una sola crida.
    /// </summary>
    /// <param name="portrait">L'objecte Image de la UI que es vol desintegrar.</param>
    /// <param name="onDone">Acció de retorn (callback) que s'executarà quan s'hagi destruït el retrat.</param>
    /// <param name="tint">Color de tint opcional per pintar els fragments (per defecte, blanc).</param>
    public static void Play(Image portrait, Action onDone, Color? tint = null)
    {
        // Si no hi ha cap retrat actiu o vàlid, no podem fer l'animació, així que executem directament el callback de fi
        if (portrait == null || !portrait.enabled)
        {
            onDone?.Invoke();
            return;
        }

        // Creem un GameObject buit que contindrà la lògica de l'efecte de destrucció
        var go = new GameObject("EnemyDestroyFX");
        
        // El col·loquem exactament sota el mateix pare que la imatge original per mantenir l'ordre de renderització de la UI Canvas
        go.transform.SetParent(portrait.transform.parent, false);
        go.transform.SetSiblingIndex(portrait.transform.GetSiblingIndex());

        // Afegim aquest component al nou objecte i configurem els paràmetres necessaris per a l'execució
        var fx = go.AddComponent<EnemyDestroyFX>();
        fx.sourcePortrait = portrait;
        fx.onDone         = onDone;
        fx.tint           = tint ?? Color.white;
    }

    // ── VARIABLES I DADES INTERNES ────────────────────────────────────
    private Image  sourcePortrait;           // Imatge original de referència
    private Action onDone;                   // Callback de finalització de l'efecte
    private Color  tint = Color.white;       // Color del tint per modular el to de la imatge

    private RectTransform[] tileRTs;         // Array de referències dels transformadors dels fragments
    private Vector2[]       velocities;      // Vectors de velocitat instantània per a cadascun dels fragments
    private float[]         angularVels;     // Velocitats de rotació en l'eix Z per a cadascun dels fragments
    private Graphic[]       graphics;        // Referències gràfiques (RawImage o Image) per poder manipular el color si cal

    // Quan s'inicialitza el script, llancem la corrutina principal que orquestra tot el cicle de vida de l'efecte
    private void Start() => StartCoroutine(Run());

    // ── RUTINA PRINCIPAL DE L'EFECTE ──────────────────────────────────
    /// <summary>
    /// Corrutina principal de l'animació: calcula les coordenades UV del sprite original,
    /// genera la quadrícula de partícules de UI, fa tremolar el retrat origen, reprodueix el so de destrucció
    /// i finalment calcula les trajectòries físiques de caiguda fins a terra.
    /// </summary>
    private IEnumerator Run()
    {
        // Emmagatzemem les dimensions físiques del retrat original per recrear la quadrícula correctament
        RectTransform srcRT   = sourcePortrait.rectTransform;
        Vector2       srcSize = srcRT.rect.size;

        // ── CÀLCUL DE LES COORDENADES UV DE L'SPRITE ───────────────────
        // Això és crucial perquè a Unity les imatges poden estar dins d'un Atlas. Hem de calcular quina
        // part exacta de la textura del fitxer (de 0 a 1) equival al rectangle de l'sprite que s'està mostrant.
        Sprite  sp  = sourcePortrait.sprite;
        Texture tex = sp != null ? sp.texture : null;
        Rect    uv  = new Rect(0, 0, 1, 1); // Per defecte si no disposem de sprite vàlid

        if (sp != null && tex != null)
        {
            uv = new Rect(
                sp.textureRect.x      / tex.width,
                sp.textureRect.y      / tex.height,
                sp.textureRect.width  / tex.width,
                sp.textureRect.height / tex.height
            );
        }

        // Configurem el RectTransform principal d'aquest objecte contenidor perquè ocupi la mateixa posició i mida
        var selfRT = gameObject.AddComponent<RectTransform>();
        selfRT.anchorMin        = srcRT.anchorMin;
        selfRT.anchorMax        = srcRT.anchorMax;
        selfRT.pivot            = srcRT.pivot;
        selfRT.anchoredPosition = srcRT.anchoredPosition;
        selfRT.sizeDelta        = srcSize;

        // ── CREACIÓ DE LES TESEL·LES (FRAGMENTS DE PÍXELS) ───────────────
        // Detectem si s'està destruint un projectil en lloc d'un enemic per ajustar les físiques de l'explosió
        bool isProjectile = sourcePortrait.GetComponent<ProjectileUI>() != null;

        int     count = COLS * ROWS;
        tileRTs     = new RectTransform[count];
        velocities  = new Vector2[count];
        angularVels = new float[count];
        graphics    = new Graphic[count];

        float tileW = srcSize.x / COLS;
        float tileH = srcSize.y / ROWS;

        // Bucle niuat per generar cada cel·la de la graella de fragments
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
            {
                int idx    = r * COLS + c;
                var tileGo = new GameObject($"P{idx}");
                tileGo.transform.SetParent(transform, false);

                var rt = tileGo.AddComponent<RectTransform>();
                // Deixem 1 píxel de marge (gap) entre tesel·les perquè el contrast doni un toc de disseny pixelat premium
                rt.sizeDelta = new Vector2(tileW - 1f, tileH - 1f);
                // Calculem la posició inicial de cada fragment en relació amb el centre del contenidor pare
                rt.anchoredPosition = new Vector2(
                    (c + 0.5f) * tileW - srcSize.x * 0.5f,
                    (r + 0.5f) * tileH - srcSize.y * 0.5f
                );

                // Aspecte visual: si tenim textura d'imatge original, fem servir un RawImage amb un trosset concret (sub-rect UV)
                Graphic g;
                if (tex != null)
                {
                    var raw     = tileGo.AddComponent<RawImage>();
                    raw.texture = tex;
                    // Mapegem exactament quin fragment de la imatge de l'sprite correspon a aquesta posició r, c de la graella
                    raw.uvRect  = new Rect(
                        uv.x + (float)c / COLS * uv.width,
                        uv.y + (float)r / ROWS * uv.height,
                        uv.width  / COLS,
                        uv.height / ROWS
                    );
                    raw.color = tint; // Apliquem el tint passat per paràmetre per homogeneïtzar la paleta de colors
                    g = raw;
                }
                else
                {
                    // Fallback visual: si no hi ha cap textura, pintem quadrats sòlids amb una gamma de degradats per no trencar el disseny
                    var img   = tileGo.AddComponent<Image>();
                    float hue = (float)(c + r) / (COLS + ROWS) * 0.12f + 0.04f;
                    img.color = Color.HSVToRGB(hue, 0.85f, 1f) * tint;
                    g = img;
                }

                // Càlcul de la direcció de l'explosió: radial des del centre cap a enfora, afegint aleatorietat per a un dinamisme orgànic
                Vector2 dir   = rt.anchoredPosition.magnitude > 0.01f
                    ? rt.anchoredPosition.normalized
                    : UnityEngine.Random.insideUnitCircle.normalized;
                    
                float speed = UnityEngine.Random.Range(80f, 250f);
                if (isProjectile) speed *= 2.2f; // Els projectils exploten amb molta més intensitat per donar impressió d'impacte ràpid

                // Vector final de velocitat: impuls radial de dispersió combinat amb un impuls vertical (bias cap amunt)
                velocities[idx]  = dir * speed
                    + new Vector2(
                        UnityEngine.Random.Range(-50f, 50f),
                        UnityEngine.Random.Range(50f, 200f) * (isProjectile ? 1.5f : 1f)
                      );

                // Afegim velocitat de gir (rotació en l'eix Z) molt agressiva per crear un efecte caòtic espectacular
                angularVels[idx] = UnityEngine.Random.Range(-800f, 800f);
                if (isProjectile) angularVels[idx] *= 1.5f;

                tileRTs[idx]     = rt;
                graphics[idx]    = g;

                // Amaguem el fragment inicialment per poder fer que primer vibri el retrat sencer
                tileGo.SetActive(false);
            }
        }

        // ── FASE 1: TREMOLOR (SHAKE) PREVI A L'EXPLOSIÓ ───────────────
        // Els projectils tenen un shake pràcticament instantani (0.12s), mentre que els caps o enemics tremolen més (0.45s)
        float shakeDuration = isProjectile ? 0.12f : 0.45f;
        float elapsedShake = 0f;
        Vector3 origPos = sourcePortrait.rectTransform.anchoredPosition3D;
        
        while (elapsedShake < shakeDuration)
        {
            elapsedShake += Time.deltaTime;
            // Fem un desplaçament pseudoaleatori en un cercle en 2D per simular vibració pre-explosió
            sourcePortrait.rectTransform.anchoredPosition3D = origPos + (Vector3)(UnityEngine.Random.insideUnitCircle * (isProjectile ? 16f : 8f));
            yield return null;
        }
        sourcePortrait.rectTransform.anchoredPosition3D = origPos;

        // Amaguem el retrat original sencer i reactivem de cop tots els fragments que acabem de crear
        sourcePortrait.enabled = false;

        // ── FASE 2: SO D'EXPLOSIÓ (Només per a Enemics, no projectils redundants) ─
        if (!isProjectile)
        {
            var cm = FindFirstObjectByType<CombatManager>();
            if (cm != null) cm.PlayExplosionSound();
        }

        // Activem tots els GameObjects dels fragments que havíem mantingut ocults
        for (int i = 0; i < count; i++)
        {
            if (tileRTs[i] != null) tileRTs[i].gameObject.SetActive(true);
        }

        // ── FASE 3: SIMULACIÓ FÍSICA I CAIGUDA D'EXPLOSIÓ ─────────────────
        // Calculem on es troba el nivell del sòl artificial per a la caiguda.
        // Per als enemics és a la part inferior de la seva pròpia imatge (més un petit marge de 40 píxels).
        float baseFloorY = -srcSize.y / 2f - 40f;
        
        if (isProjectile)
        {
            // Per als projectils, s'utilitza la línia física inferior de "kill" de l'arena per aconseguir un comportament natural.
            var cm = FindFirstObjectByType<CombatManager>();
            float killY = cm != null ? cm.GetDestroyLimitY() : -1200f;
            baseFloorY = killY - srcRT.anchoredPosition.y - 80f;
        }

        // Generem irregularitats al sòl de cada fragment per separat perquè quedin apilades amb alçades diferents de forma orgànica
        float[] individualFloors = new float[count];
        for (int i = 0; i < count; i++)
        {
            individualFloors[i] = baseFloorY + UnityEngine.Random.Range(-30f, 30f);
        }

        bool allStopped = false;
        // Si és un projectil, dupliquem la força de gravetat perquè l'efecte es resolgui molt ràpidament a la pantalla
        float currentGravity = isProjectile ? GRAVITY * 2.2f : GRAVITY;

        // Bucle d'animació física per a la caiguda de cada part de la imatge desintegrada
        while (!allStopped)
        {
            float dt = Time.deltaTime;
            allStopped = true;

            for (int i = 0; i < count; i++)
            {
                if (tileRTs[i] == null) continue;

                // Si el fragment actual es troba per sobre del seu terra determinat, continua caient i rotant
                if (tileRTs[i].anchoredPosition.y > individualFloors[i])
                {
                    allStopped = false;
                    velocities[i].y += currentGravity * dt;
                    tileRTs[i].anchoredPosition += velocities[i] * dt;
                    tileRTs[i].Rotate(0f, 0f, angularVels[i] * dt);
                }
                else
                {
                    // Quan toca terra, el deixem aturat exactament a la línia del sòl i posem les seves forces a zero
                    Vector2 pos = tileRTs[i].anchoredPosition;
                    pos.y = individualFloors[i];
                    tileRTs[i].anchoredPosition = pos;

                    velocities[i] = Vector2.zero;
                    angularVels[i] = 0f;
                }
            }

            yield return null;
        }

        // Un cop reposats tots els fragments a terra, esperem 3 segons abans de destruir completament el sistema per netejar la memòria
        Destroy(gameObject, 3f);
        onDone?.Invoke();
    }
}

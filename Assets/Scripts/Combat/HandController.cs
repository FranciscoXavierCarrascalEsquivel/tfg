using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Control de moviment i estat de la mà del jugador (HandController).
/// Aquest script és el responsable de respondre al teclat de l'usuari durant les fases de "defensa"
/// ( Bullet Hell / Esquiva de bales d'estil Undertale). Permet desplaçar l'avatar del jugador (la mà)
/// dins d'unes vores delimitades, gestionar frames d'invulnerabilitat temporal i aplicar efectes
/// visuals de parpelleig suau.
/// 
/// LÒGICA D'ESQUIVA DEL TFG:
/// - **Moviment multicanal**: Permet seleccionar via inspector si controlem l'avatar mitjançant
///   tecles de disseny estàndard (WASD) o directament per fletxes de navegació clàssiques.
/// - **Detecció dinàmica de límits físics**: Utilitza l'API `GetWorldCorners` del RectTransform per re-ajustar
///   el pivot físic real del dibuix i retenir-lo exactament a dins dels límits del quadre de combat del Canvas,
///   garantint un comportament òptim de col·lisions independentment de la resolució activa de la pantalla.
/// - **Frames d'immunitat de dany**: Activa un temporitzador per a prevenir cops en cadena d'impactes
///   repetits de projectils de forma consecutiva.
/// - **Parpelleig sinusoidal fluid**: Aplica variacions d'opacitat basades en la corba matemàtica cosinus
///   a nivell gràfic per a aconseguir un feedback de dany molt lluent.
/// </summary>
public class HandController : MonoBehaviour
{
    /// <summary>
    /// Modes opcionals de mapeig de tecles direccionals.
    /// </summary>
    public enum InputMode
    {
        WASD,
        Arrows
    }

    [SerializeField] private InputMode inputMode = InputMode.WASD; // Tecles per defecte
    [SerializeField] private float speed = 500f; // Velocitat física base
    public float speedMultiplier = 1f; // Multiplicador dinàmic (ex: bonus de velocitat de la motxilla)

    private float immunityTimer = 0f; // Temporitzador d'invulnerabilitat actiu
    private Coroutine blinkCoroutine; // Ref de parpelleig dinàmic actiu
    public bool IsImmune => immunityTimer > 0f; // Cert si té escut actiu
    
    [Header("Límits Físics del Quadre de Combat")]
    [Tooltip("Límit o vora lateral esquerra.")]
    [SerializeField] private RectTransform leftBound;
    [Tooltip("Límit o vora lateral dreta.")]
    [SerializeField] private RectTransform rightBound;
    [Tooltip("Límit o vora inferior.")]
    [SerializeField] private RectTransform bottomBound;
    [Tooltip("Límit o vora superior.")]
    [SerializeField] private RectTransform topBound;
    
    public bool canMove = false; // El CombatManager dóna o treu el permís de moviment per a controlar les fases del diàleg

    public bool IsMoving { get; private set; } // Flag de moviment de dades per a possibles partícules

    private RectTransform rt;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Update()
    {
        // ── ACTUALITZACIÓ DEL TEMPORITZADOR D'IMMUNITAT ──
        if (immunityTimer > 0f)
        {
            immunityTimer -= Time.deltaTime;
        }

        if (!canMove) 
        {
            IsMoving = false;
            return;
        }

        float h = 0f;
        float v = 0f;

        // ── RECULL D'INPUT DE DADES SEGONS CONFIGURACIÓ ──
        if (inputMode == InputMode.WASD)
        {
            if (Input.GetKey(KeyCode.W)) v += 1f;
            if (Input.GetKey(KeyCode.S)) v -= 1f;
            if (Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.A)) h -= 1f;
        }
        else if (inputMode == InputMode.Arrows)
        {
            if (Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        }
        
        // Normalitzem el vector per a evitar moure'ns el doble de ràpid en diagonals
        Vector2 input = new Vector2(h, v).normalized;
        transform.position += (Vector3)(input * (speed * speedMultiplier) * Time.deltaTime);

        IsMoving = input.sqrMagnitude > 0.01f;

        Vector3 pos = transform.position;

        // ── LIMITACIÓ FÍSICA DINÀMICA AL CANVAS (EIX X) ──
        if (leftBound != null && rightBound != null && rt != null)
        {
            // Determinem quin objecte és l'esquerra i quin la dreta de forma adaptativa
            float boundLeftEdge = Mathf.Min(leftBound.position.x, rightBound.position.x);
            float boundRightEdge = Mathf.Max(leftBound.position.x, rightBound.position.x);

            // Llegim els quatre vèrtexs a la pantalla en píxels del nostre RectTransform
            Vector3[] myCorners = new Vector3[4];
            rt.GetWorldCorners(myCorners);
            float myMinX = Mathf.Min(myCorners[0].x, myCorners[1].x, myCorners[2].x, myCorners[3].x);
            float myMaxX = Mathf.Max(myCorners[0].x, myCorners[1].x, myCorners[2].x, myCorners[3].x);

            // Calculem la distància de seguretat entre el centre de l'objecte i la vora exterior del seu dibuix
            float offsetLeft = Mathf.Abs(transform.position.x - myMinX);
            float offsetRight = Mathf.Abs(myMaxX - transform.position.x);

            float safeMinX = boundLeftEdge + offsetLeft;
            float safeMaxX = boundRightEdge - offsetRight;

            // Protecció de seguretat si els límits s'esquerden o són molt estrets
            if (safeMinX > safeMaxX) safeMaxX = safeMinX;

            pos.x = Mathf.Clamp(pos.x, safeMinX, safeMaxX);
        }

        // ── LIMITACIÓ FÍSICA DINÀMICA AL CANVAS (EIX Y) ──
        if (bottomBound != null && topBound != null && rt != null)
        {
            float boundBottomEdge = Mathf.Min(bottomBound.position.y, topBound.position.y);
            float boundTopEdge = Mathf.Max(bottomBound.position.y, topBound.position.y);

            Vector3[] myCorners = new Vector3[4];
            rt.GetWorldCorners(myCorners);
            float myMinY = Mathf.Min(myCorners[0].y, myCorners[1].y, myCorners[2].y, myCorners[3].y);
            float myMaxY = Mathf.Max(myCorners[0].y, myCorners[1].y, myCorners[2].y, myCorners[3].y);

            float offsetBottom = Mathf.Abs(transform.position.y - myMinY);
            float offsetTop = Mathf.Abs(myMaxY - transform.position.y);

            float safeMinY = boundBottomEdge + offsetBottom;
            float safeMaxY = boundTopEdge - offsetTop;

            if (safeMinY > safeMaxY) safeMaxY = safeMinY;

            pos.y = Mathf.Clamp(pos.y, safeMinY, safeMaxY);
        }

        transform.position = pos;
    }

    private void OnDisable()
    {
        // En desactivar, ens assegurem d'aturar les corrutines visuals i restaurar els alfas al màxim
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        var images = GetComponentsInChildren<Image>(true);
        if (images.Length == 0) images = GetComponents<Image>();
        foreach (var img in images)
        {
            if (img != null && img.gameObject.name != "HandCollider") 
            {
                img.enabled = true;
                Color c = img.color;
                c.a = 1f;
                img.color = c;
            }
        }
    }

    /// <summary>
    /// Dispara l'efecte de frames de seguretat temporals.
    /// </summary>
    public void TriggerImmunity(float duration)
    {
        immunityTimer = duration;
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
        }
        blinkCoroutine = StartCoroutine(BlinkRoutine(duration));
    }

    /// <summary>
    /// Corrutina de parpelleig suau (Blinking).
    /// Utilitza una funció Cosinus per fluctuar la transparència alfa dels sprites de manera molt elegant.
    /// </summary>
    private IEnumerator BlinkRoutine(float duration)
    {
        var images = GetComponentsInChildren<Image>(true);
        if (images.Length == 0) images = GetComponents<Image>();

        List<Image> validImages = new List<Image>();
        foreach (var img in images)
        {
            // Protegim l'objecte col·lisionador de la mà per a evitar problemes secundaris de detecció
            if (img != null && img.gameObject.name != "HandCollider")
            {
                validImages.Add(img);
            }
        }

        float elapsed = 0f;
        float fadeSpeed = 25f; // Velocitat a la qual es realitza la fosa gradual

        while (elapsed < duration)
        {
            // La funció cosinus retorna de [-1 a 1]. La transformem a escala suau positiva de [0.2f a 1f]
            float alpha = Mathf.Lerp(0.2f, 1f, (Mathf.Cos(elapsed * fadeSpeed) + 1f) / 2f);

            foreach (var img in validImages)
            {
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }
            
            yield return null; // Esperem un frame: garanteix foses perfectes a ràtios alts d'FPS
            elapsed += Time.deltaTime;
        }

        // Restaurem l'estat perfecte de color en acabar la invulnerabilitat
        foreach (var img in validImages)
        {
            Color c = img.color;
            c.a = 1f;
            img.color = c;
        }
        blinkCoroutine = null;
    }
}

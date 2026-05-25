using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona el minijoc d'habilitat de combat ("Skill Check") basat en el ritme i la precisió, 
/// a l'estil clàssic de títols com *Undertale*.
/// El jugador ha de prémer una tecla ('E', 'Intro', 'Espai' o fer clic) en el moment exacte 
/// en què una agulla giratòria passa per la zona objectiu (a l'esquerra, 180 graus).
/// El component calcula proceduralment el percentatge de precisió, n'aplica el multiplicador de dany actiu,
/// genera efectes de partícules d'explosió i realitza transicions d'entrada/sortida dinàmiques amb lliscaments (slides).
/// </summary>
public class SkillCheckUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("El component Transform pare que conté tots els elements visuals del minijoc d'habilitat.")]
    [SerializeField] private Transform skillCheckTransform;
    [Tooltip("El pivot o indicador que rota (requereix un RectTransform per alterar el seu angle local Z).")]
    [SerializeField] private RectTransform arrowPivot;
    [Tooltip("Text de la UI de TextMeshPro on es mostrarà el dany causat o les instruccions inicials.")]
    [SerializeField] private TMP_Text damageText;

    [Header("Skill Check Settings")]
    [Tooltip("Velocitat de rotació de la fletxa en graus per segon.")]
    [SerializeField] private float rotationSpeed = 650f;
    [Tooltip("L'angle objectiu que el jugador ha de colpejar. Per exemple, 180 equival al costat esquerre.")]
    [SerializeField] private float targetAngle = 180f;
    [Tooltip("Marge de tolerància en graus (+-) per classificar el cop com a 'Perfect' (màxim dany).")]
    [SerializeField] private float perfectZone = 15f;
    [Tooltip("El dany base assignat si es realitza un encert perfecte exactament al centre.")]
    [SerializeField] private int maxDamage = 20;

    [Header("Visuals Extra")]
    [Tooltip("Sprite utilitzat per mostrar l'animació d'explosió quan s'atura la fletxa.")]
    [SerializeField] private Sprite explosionSprite;
    [Tooltip("Font personalitzada pesada o retro per destacar el número del dany causat.")]
    [SerializeField] private TMPro.TMP_FontAsset customFont;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip attackSound;  // So de tall o impacte inicial en prémer la tecla
    [SerializeField] private AudioClip tickSound;    // So recurrent tipus rellotge mentre gira l'agulla
    [SerializeField] private AudioClip critSound;    // So de cop crític en aconseguir precisió perfecte
    [SerializeField] private AudioClip hitSound;     // So de cop normal en encertar la zona estàndard
    [SerializeField] private AudioClip missSound;    // So de fallada (si s'escau)

    private AudioSource audioSource; // Component per reproduir els efectes sonors interactius

    // Inicialització inicial i auto-detecció de referències
    private void Awake()
    {
        if (skillCheckTransform == null) skillCheckTransform = transform;
        
        // AUTO-DETECCIÓ PREVENTIVA: per si l'estudiant s'ha oblidat d'enllaçar els elements a l'Inspector de Unity
        if (arrowPivot == null)
        {
            Transform foundPivot = transform.Find("ArrowPivot");
            if (foundPivot != null) arrowPivot = foundPivot.GetComponent<RectTransform>();
            else Debug.LogError("ERROR: No s'ha trobat l'ArrowPivot! Assegura't que es diu 'ArrowPivot' a la jerarquia.");
        }

        if (damageText == null)
        {
            Transform foundText = transform.Find("DamageText");
            if (foundText != null) 
            {
                damageText = foundText.GetComponent<TMP_Text>();
            }
            else 
            {
                // Si l'usuari no n'ha creat cap a la UI, el generem dinàmicament per programació per evitar excepcions de punter nul!
                GameObject txtGo = new GameObject("DamageText");
                txtGo.transform.SetParent(skillCheckTransform, false);
                damageText = txtGo.AddComponent<TextMeshProUGUI>();
                
                RectTransform rt = txtGo.GetComponent<RectTransform>();
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(800, 400);
                
                Debug.Log("DAMAGE TEXT GENERAT AUTOMÀTICAMENT al Prefab SkillCheckUI!");
            }
        }
        
        // Creem i configurem un component AudioSource propi per reproduir efectes sonors en format 2D estàndard
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (damageText != null) damageText.text = "";
    }

    private float currentAngle = 0f;            // L'angle actual de l'agulla de precisió
    private float timer = 0f;                   // Temporitzador per evitar pulsacions instantànies accidentals
    private float nextTick = 0f;                // Moment en què s'ha de reproduir el proper so 'tick' de gir
    private float maxWaitTime = 5f;             // Ràtio de seguretat (ja no s'aplica en la versió de joc pausada)
    private bool isChecking = false;            // Bandera que indica si el joc està actiu
    private System.Action<int> onDamageCalculated; // Callback que retorna el dany calculat cap al CombatManager
    private Coroutine blinkCoroutine;           // Corrutina per al parpelleig del text d'instrucció

    private float damageMultiplier = 1f;        // Multiplicador extern que altera el dany final (per ex. per pocions o passives)

    /// <summary>
    /// Enllaça el so d'atac general des de la configuració de l'escena.
    /// </summary>
    public void SetAttackSound(AudioClip clip) => attackSound = clip;

    /// <summary>
    /// Defineix el multiplicador de dany dinàmic associat al nivell d'amistat, equipament o reclutament de la ronda.
    /// </summary>
    public void SetDamageMultiplier(float mul) => damageMultiplier = mul;

    /// <summary>
    /// Mètode principal per activar el minijoc d'habilitat de cop.
    /// </summary>
    /// <param name="callback">Acció de retorn que rebrà el valor final de dany calculat.</param>
    public void StartSkillCheck(System.Action<int> callback)
    {
        onDamageCalculated = callback;
        StartCoroutine(EntryRoutine());
    }

    // ── RUTINA D'ENTRADA A PANTALLA (SLIDE IN) ────────────────────────
    /// <summary>
    /// Corrutina que gestiona la posada en escena de la interfície. 
    /// Realitza un lliscament elàstic des de l'esquerra fins a la seva posició objectiu,
    /// inicialitza el text d'instruccions amb un contorn (outline) d'alta visibilitat en TextMeshPro,
    /// i comença a fer parpellejar el text per advertir al jugador de la interacció.
    /// </summary>
    private IEnumerator EntryRoutine()
    {
        currentAngle = 0f;
        timer = 0f;
        nextTick = 0.1f;
        if (damageText != null) 
        {
            damageText.text = "PRESS E\nOR ENTER";
            if (customFont != null) damageText.font = customFont;
            damageText.alignment = TMPro.TextAlignmentOptions.Center;
            damageText.fontSize = 80;
            damageText.fontStyle = TMPro.FontStyles.Bold;
            damageText.color = Color.white;
            
            // Reajustem la caixa de text a mida fixa per evitar retalls (clipping) horitzontals
            RectTransform textRt = damageText.GetComponent<RectTransform>();
            if (textRt != null)
            {
                textRt.sizeDelta = new Vector2(800, 300);
                textRt.anchoredPosition = new Vector2(0, -380); // Col·locat estratègicament a sota del dial
                textRt.localScale = Vector3.one;
            }

            // Eliminem components Outline d'Unity clàssics si existeixen per evitar interferències amb TMPro
            if (damageText.GetComponent<Outline>() != null) Destroy(damageText.GetComponent<Outline>());
            
            // Important: instanciem el material compartit per modificar només l'aspecte d'aquesta lletra a l'arena,
            // evitant que els canvis de contorn es transmetin globalment a la resta del projecte de Unity.
            damageText.fontSharedMaterial = Instantiate(damageText.fontSharedMaterial);
            damageText.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
            damageText.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
        }
        
        if (arrowPivot != null) arrowPivot.localRotation = Quaternion.Euler(0, 0, currentAngle);

        // --- ANIMACIÓ DE LLISCAMENT INICIAL (EASE OUT CUBIC) ---
        RectTransform rt = skillCheckTransform as RectTransform;
        if (rt != null)
        {
            Vector2 currentTargetPos = rt.anchoredPosition; // La posició final assignada pel combat manager
            Vector2 startPos = currentTargetPos + new Vector2(-1200f, 0f); // Iniciem fora del límit esquerre
            
            rt.anchoredPosition = startPos;
            rt.localScale = Vector3.one;

            float t = 0f;
            float duration = 0.4f;
            while(t < duration)
            {
                t += Time.deltaTime;
                float pct = t / duration;
                
                // Corba d'amortiment suau de tipus EaseOut Cubic
                float ease = 1f - Mathf.Pow(1f - pct, 3f);
                
                rt.anchoredPosition = Vector2.Lerp(startPos, currentTargetPos, ease);
                yield return null;
            }
            rt.anchoredPosition = currentTargetPos;
        }
        else if (skillCheckTransform != null) skillCheckTransform.localScale = Vector3.one;
        
        isChecking = true;
        
        // Llancem el bucle de parpelleig lumínic de la instrucció
        if (damageText != null)
        {
            if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
            blinkCoroutine = StartCoroutine(BlinkTextRoutine());
        }
    }

    /// <summary>
    /// Realitza un parpelleig suau de l'opacitat (Alpha canal) del text d'interacció
    /// mitjançant una ona triangular basat en el rellotge de temps de Unity.
    /// </summary>
    private IEnumerator BlinkTextRoutine()
    {
        while (isChecking)
        {
            float alpha = Mathf.PingPong(Time.time * 2f, 1f);
            if (damageText != null) 
            {
                Color c = damageText.color;
                c.a = alpha;
                damageText.color = c;
            }
            yield return null;
        }
    }

    // Executat a cada frame
    private void Update()
    {
        if (!isChecking) return;

        timer += Time.deltaTime;
        
        // Rotem l'agulla aplicant una velocitat constant negativa per girar en sentit horari
        currentAngle -= rotationSpeed * Time.deltaTime;
        currentAngle %= 360f;
        
        if (arrowPivot != null)
        {
            arrowPivot.localRotation = Quaternion.Euler(0, 0, currentAngle);
        }
        
        // So de 'click' periòdic tipus rellotge mentre es mou l'agulla per augmentar la tensió i el feedback auditiu
        if (timer > nextTick)
        {
            if (tickSound != null && audioSource != null)
            {
                // Modifiquem el pitch (afinació) lleugerament de forma aleatòria per fer el so menys repetitiu i orgànic
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(tickSound);
            }
            nextTick = timer + 0.05f;
        }

        // Deixem un breu marge de seguretat (0.3s) per evitar clics accidentals immediats.
        // El jugador pot colpejar prement: 'E', 'Retorn' (Enter), 'Espai' o fent clic amb el punter a la pantalla.
        if (timer > 0.3f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            FinishCheck();
        }
        // Cancel·lació del minijoc si el jugador decideix prémer la tecla d'escapada (Escape) o retrocés (Backspace)
        else if (timer > 0.1f && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)))
        {
            CancelCheck();
        }
    }

    /// <summary>
    /// Atura immediatament la comprovació de dany sense fer cap càlcul,
    /// retornant un codi de senyal de cancel·lació (-1) al CombatManager.
    /// </summary>
    private void CancelCheck()
    {
        isChecking = false;
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        onDamageCalculated?.Invoke(-1);
    }

    /// <summary>
    /// Finalitza el minijoc, atura la rotació de la fletxa, reprodueix el so d'impuls del cop
    /// i dispara la resolució de càlcul de precisió.
    /// </summary>
    private void FinishCheck()
    {
        isChecking = false;

        // So d'impacte directe immediat
        if (attackSound != null && audioSource != null)
            audioSource.PlayOneShot(attackSound);
        
        // Aturem el parpelleig i forcem l'opacitat del text de dany a 1 per assegurar que sigui totalment visible
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        if (damageText != null)
        {
            Color c = damageText.color;
            c.a = 1f;
            damageText.color = c;
        }

        // Resol la precisió final i executa les animacions d'impacte gràfic
        StartCoroutine(ResolveResultRoutine());
    }

    // ── CÀLCUL I RESOLUCIÓ DE LA PRECISIÓ DE COP ─────────────────────
    /// <summary>
    /// Corrutina de resolució: mesura el delta d'angle absolut de la fletxa respecte de la marca de 180º,
    /// determina si ha estat un 'Perfect' o un 'Hit' corrent, assigna els punts de dany escalats,
    /// dibuixa dinàmicament l'explosió de fons, canvia el text a format massiu (200px) amb un contorn blanc brillant,
    /// fa un punch d'escala física i llisca el panell cap a la dreta abans d'iniciar el callback del combat.
    /// </summary>
    private IEnumerator ResolveResultRoutine()
    {
        // Calculem la distància mínima angular considerant la naturalesa circular dels angles (0 - 360)
        float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle));
        int finalDamage = 0;
        
        if (deltaAngle <= perfectZone)
        {
            // --- COP PERFECTE ---
            // Dany màxim complet multiplicat per la bonificació activa
            finalDamage = Mathf.RoundToInt(maxDamage * damageMultiplier);
            if (critSound != null) audioSource.PlayOneShot(critSound);
            if (damageText != null) 
            {
                damageText.text = $"{finalDamage}";
                damageText.color = Color.red; // Vermell per a impacte súper crític de gran força
            }
        }
        else
        {
            // --- COP NORMAL O MENYS PRECIS ---
            // Dany descendent segons la distància física de la zona perfecta
            float distFromPerfect = deltaAngle - perfectZone;
            float maxFailDist = 180f - perfectZone;
            float accuracyDrop = 1f - (distFromPerfect / maxFailDist);
            
            int maxFailDamage = 10;
            int minFailDamage = 1;
            int baseDmg = Mathf.RoundToInt(Mathf.Lerp(minFailDamage, maxFailDamage, accuracyDrop));
            finalDamage = Mathf.RoundToInt(baseDmg * damageMultiplier);
            
            if (hitSound != null) audioSource.PlayOneShot(hitSound);
            if (damageText != null) 
            {
                damageText.text = $"{finalDamage}";
                damageText.color = Color.green; // Verd per a cops d'esquivada/menys contundents
            }
        }
        
        // --- INSTANCIACIÓ DE L'EXPLOSIÓ DE FONS ---
        if (skillCheckTransform != null)
        {
            GameObject expGo = new GameObject("ExplosionFX");
            expGo.transform.SetParent(skillCheckTransform, false);
            
            Image img = expGo.AddComponent<Image>();
            
            if (explosionSprite != null)
            {
                img.sprite = explosionSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.SetNativeSize();
                
                // Limitem la mida de l'explosió gràfica perquè encaixi de forma harmoniosa amb el disc circular negre
                float w = img.rectTransform.rect.width; float h = img.rectTransform.rect.height;
                if (w > 0 && h > 0) {
                    float fitRatio = Mathf.Min(400f / w, 400f / h);
                    img.rectTransform.sizeDelta = new Vector2(w * fitRatio, h * fitRatio);
                }
            }
            else
            {
                // Failsafe de disseny: quadrat taronja retro brillant en cas de no trobar cap recurs imatge associat
                img.color = new Color(1f, 0.4f, 0f, 0.8f);
                img.rectTransform.sizeDelta = new Vector2(300, 300);
                Debug.LogWarning("Avís: Tens la casella de l'Explosion Sprite buida a dins el Prefab 'SkillCheckUI'!");
            }
            
            img.rectTransform.anchoredPosition = Vector2.zero;
            // Ens assegurem que el gràfic de l'explosió es renderitzi per sobre de la rodella de fons
            expGo.transform.SetAsLastSibling(); 
        }

        // --- DISSENY PREMIUM DEL TEXT DE DANY (Feedback de gran mida) ---
        if (damageText != null)
        {
            if (customFont != null) damageText.font = customFont;
            
            // Destruïm límits d'escalat i auto-sizing del text perquè es vegi gegant sense deformacions
            damageText.enableAutoSizing = false;
            damageText.textWrappingMode = TextWrappingModes.NoWrap; // Sempre en una sola línia de text
            damageText.overflowMode = TMPro.TextOverflowModes.Overflow;
            
            damageText.alignment = TMPro.TextAlignmentOptions.Center;
            damageText.fontSize = 200; // Mida exagerada de 200 píxels
            damageText.fontStyle = TMPro.FontStyles.Bold;

            RectTransform textRt = damageText.GetComponent<RectTransform>();
            if (textRt != null)
            {
                textRt.sizeDelta = new Vector2(800, 400);
                textRt.anchoredPosition = Vector2.zero; // Col·locat exactament al centre geomètric de l'explosió
                textRt.localScale = Vector3.one;
            }

            // Dibuixem un contorn blanc brillant molt visible que separi les lletres del fons vermell/verd
            if (damageText.GetComponent<Outline>() == null)
            {
                Outline outline = damageText.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(3, -3);
                
                Outline outline2 = damageText.gameObject.AddComponent<Outline>();
                outline2.effectColor = Color.white;
                outline2.effectDistance = new Vector2(-3, 3);
            }

            // Portem el text al capdavant de tot el render de la Canvas
            damageText.transform.SetAsLastSibling();
            damageText.ForceMeshUpdate();
        }

        // --- ANIMACIÓ DE COP D'IMPACTE (PUNCH D'ESCALA FÍSICA) ---
        if (skillCheckTransform != null)
        {
            Vector3 startScale = skillCheckTransform.localScale;
            Vector3 targetScale = (deltaAngle <= perfectZone) ? Vector3.one * 1.2f : Vector3.one * 1.05f;
            float punchTimer = 0f;
            while(punchTimer < 0.15f)
            {
                punchTimer += Time.deltaTime;
                skillCheckTransform.localScale = Vector3.Lerp(startScale, targetScale, punchTimer / 0.15f);
                yield return null;
            }
            // Mantenim el número a la pantalla durant 0.8 segons perquè el jugador tingui clar el dany aplicat
            yield return new WaitForSeconds(0.8f);
        }

        // --- ANIMACIÓ DE SORTIDA DE PANTALLA (SLIDE OUT DRETA) ---
        RectTransform rtOut = skillCheckTransform as RectTransform;
        if (rtOut != null)
        {
            Vector2 startPosOut = rtOut.anchoredPosition;
            Vector2 targetPosOut = startPosOut + new Vector2(1200f, 0f); // Moviment ràpid cap a fora de la dreta
            Vector3 startingScaleOut = rtOut.localScale;

            float exitTimer = 0f;
            float exitDuration = 0.35f;
            
            while (exitTimer < exitDuration)
            {
                exitTimer += Time.deltaTime;
                float pct = exitTimer / exitDuration;
                // Corba EaseIn Cubic per a una sortida accelerada dinàmica
                float easeIn = pct * pct * pct;
                
                rtOut.anchoredPosition = Vector2.Lerp(startPosOut, targetPosOut, easeIn);
                rtOut.localScale = Vector3.Lerp(startingScaleOut, Vector3.one, easeIn);
                
                yield return null;
            }
            rtOut.anchoredPosition = targetPosOut;
        }

        if (skillCheckTransform != null) skillCheckTransform.gameObject.SetActive(false);

        // Finalment, notifiquem el resultat al Combat Manager per fer efectiva la deducció de vida
        onDamageCalculated?.Invoke(finalDamage);
    }
}

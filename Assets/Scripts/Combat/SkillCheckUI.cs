using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A skill check mini-game like Undertale. Press E to stop the arrow in the target zone.
/// Max damage given when stopped in the exact center zone.
/// </summary>
public class SkillCheckUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent Transform holding the UI elements.")]
    [SerializeField] private Transform skillCheckTransform;
    [Tooltip("The arrow or cursor that rotates (Needs a RectTransform to rotate its Z angle).")]
    [SerializeField] private RectTransform arrowPivot;
    [Tooltip("The text to display the damage dealt or outcome.")]
    [SerializeField] private TMP_Text damageText;

    [Header("Skill Check Settings")]
    [Tooltip("Speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 650f;
    [Tooltip("The target angle to hit. E.g., 180 = left side, 90 = top.")]
    [SerializeField] private float targetAngle = 180f;
    [Tooltip("How wide is the 'perfect' maximum damage zone (+- degrees).")]
    [SerializeField] private float perfectZone = 15f;
    [Tooltip("The maximum damage awarded for a perfect hit.")]
    [SerializeField] private int maxDamage = 20;

    [Header("Visuals Extra")]
    [Tooltip("Sprite per mostrar l'explosió al fons del resultat.")]
    [SerializeField] private Sprite explosionSprite;
    [Tooltip("Font més maca/cridanera pel text al encertar (p.ex. una font pesada retro).")]
    [SerializeField] private TMPro.TMP_FontAsset customFont;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip attackSound;  // So al prémer E/Intro al minijoc
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private AudioClip critSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip missSound;

    private AudioSource audioSource;

    private void Awake()
    {
        if (skillCheckTransform == null) skillCheckTransform = transform;
        
        // AUTO-DETECCIÓ PER SI T'HAS OBLIDAT D'ARROSSEGAR-HO A L'INSPECTOR:
        if (arrowPivot == null)
        {
            Transform foundPivot = transform.Find("ArrowPivot");
            if (foundPivot != null) arrowPivot = foundPivot.GetComponent<RectTransform>();
            else Debug.LogError("ERROR: No s'ha trobat l'ArrowPivot! Assegurat que es diu 'ArrowPivot' a la Hierarchy.");
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
                // Si l'usuari no n'ha creat cap, el creem automàticament des de 0 per evitar que sigui 'null' invisible!
                GameObject txtGo = new GameObject("DamageText");
                txtGo.transform.SetParent(skillCheckTransform, false);
                damageText = txtGo.AddComponent<TextMeshProUGUI>();
                
                RectTransform rt = txtGo.GetComponent<RectTransform>();
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(800, 400); // Super gran
                
                Debug.Log("DAMAGE TEXT GENERAT AUTOMÀTICAMENT al Prefab SkillCheckUI!");
            }
        }
        
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (damageText != null) damageText.text = "";
    }

    private float currentAngle = 0f;
    private float timer = 0f;
    private float nextTick = 0f;
    private float maxWaitTime = 5f;
    private bool isChecking = false;
    private System.Action<int> onDamageCalculated;
    private Coroutine blinkCoroutine;

    private float damageMultiplier = 1f;

    /// <summary>
    /// Permet al CombatManager passar el so d'atac configurat a la Scene.
    /// </summary>
    public void SetAttackSound(AudioClip clip) => attackSound = clip;

    /// <summary>
    /// Permet passar el potenciador de mal per reclutament (ex: 1.2f per un 20% extra)
    /// </summary>
    public void SetDamageMultiplier(float mul) => damageMultiplier = mul;

    /// <summary>
    /// Iniciem el minijoc cridant-lo des del CombatManager
    /// </summary>
    public void StartSkillCheck(System.Action<int> callback)
    {
        onDamageCalculated = callback;
        StartCoroutine(EntryRoutine());
    }

    private IEnumerator EntryRoutine()
    {
        currentAngle = 0f;
        timer = 0f;
        nextTick = 0.1f;
        if (damageText != null) 
        {
            damageText.text = "PREM E\nO INTRO";
            if (customFont != null) damageText.font = customFont;
            damageText.alignment = TMPro.TextAlignmentOptions.Center;
            damageText.fontSize = 80; // Molt més gran i cridaner
            damageText.fontStyle = TMPro.FontStyles.Bold; // En negreta
            damageText.color = Color.white;
            
            // Fiquem la caixa gran per evitar que faci clipping i el posicionem a SOTA de la rodella
            RectTransform textRt = damageText.GetComponent<RectTransform>();
            if (textRt != null)
            {
                textRt.sizeDelta = new Vector2(800, 300);
                textRt.anchoredPosition = new Vector2(0, -380); // Més avall: -380 en lloc de -260
                textRt.localScale = Vector3.one;
            }

            // El contorn pur a TextMeshPro (Material instanciat en lloc del genèric Outline)
            if (damageText.GetComponent<Outline>() != null) Destroy(damageText.GetComponent<Outline>());
            
            damageText.fontSharedMaterial = Instantiate(damageText.fontSharedMaterial);
            damageText.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f); // Contorn més gruixut
            damageText.fontSharedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
        }
        
        if (arrowPivot != null) arrowPivot.localRotation = Quaternion.Euler(0, 0, currentAngle);

        // --- Animació d'Entrada (Slide Des de l'esquerra) ---
        RectTransform rt = skillCheckTransform as RectTransform;
        if (rt != null)
        {
            Vector2 currentTargetPos = rt.anchoredPosition; // La posició on el combat manager ho ha col·locat
            Vector2 startPos = currentTargetPos + new Vector2(-1200f, 0f); // Fora d'escena a l'esquerra
            
            rt.anchoredPosition = startPos;
            rt.localScale = Vector3.one;

            float t = 0f;
            float duration = 0.4f;
            while(t < duration)
            {
                t += Time.deltaTime;
                float pct = t / duration;
                
                // Moviment suau EaseOut Cubic
                float ease = 1f - Mathf.Pow(1f - pct, 3f);
                
                rt.anchoredPosition = Vector2.Lerp(startPos, currentTargetPos, ease);
                yield return null;
            }
            rt.anchoredPosition = currentTargetPos;
        }
        else if (skillCheckTransform != null) skillCheckTransform.localScale = Vector3.one; // Fallback
        
        isChecking = true;
        
        // Iniciem el parpelleig del text
        if (damageText != null)
        {
            if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
            blinkCoroutine = StartCoroutine(BlinkTextRoutine());
        }
    }

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

    private void Update()
    {
        if (!isChecking) return;

        timer += Time.deltaTime;
        
        // Rotem la fletxa en graus negatius perquè vagi com el sentit del rellotge a Unity!
        currentAngle -= rotationSpeed * Time.deltaTime;
        currentAngle %= 360f;
        
        if (arrowPivot != null)
        {
            arrowPivot.localRotation = Quaternion.Euler(0, 0, currentAngle);
        }
        
        // So de click recurrent
        if (timer > nextTick)
        {
            if (tickSound != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(tickSound);
            }
            nextTick = timer + 0.05f;
        }

        // Esperem minim 0.2 segons per evitar clicks per error i acceptem E / Intro(Return) / Space / Tocar per Càmera
        if (timer > 0.3f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            FinishCheck();
        }
        // NOU: Cancel·lar amb Escape o Backspace
        else if (timer > 0.1f && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)))
        {
            CancelCheck();
        }
        // Prevenció de Softlock
        else if (timer >= maxWaitTime)
        {
            FinishCheck();
        }
    }

    private void CancelCheck()
    {
        isChecking = false;
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        onDamageCalculated?.Invoke(-1); // Mark as cancelled
    }

    private void FinishCheck()
    {
        isChecking = false;

        // So d'atac al moment exacte de polsar E/Intro
        if (attackSound != null && audioSource != null)
            audioSource.PlayOneShot(attackSound);
        
        // Aturem el parpelleig i restaurem totalment l'opacitat del text
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        if (damageText != null)
        {
            Color c = damageText.color;
            c.a = 1f;
            damageText.color = c;
        }

        StartCoroutine(ResolveResultRoutine());
    }

    private IEnumerator ResolveResultRoutine()
    {
        // Resultat: Calculem la diferència d'angle més propera a través del cercle
        float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle));
        int finalDamage = 0;
        
        if (deltaAngle <= perfectZone)
        {
            // Puntuació Perfecta
            finalDamage = Mathf.RoundToInt(maxDamage * damageMultiplier);
            if (critSound != null) audioSource.PlayOneShot(critSound);
            if (damageText != null) 
            {
                damageText.text = $"{finalDamage}";
                damageText.color = Color.red; // Vermell agressiu per cop fort
            }
        }
        else
        {
            // Puntuació segons la llunyania al centre
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
                damageText.color = Color.green; // Verd per cops fluixos/fallats
            }
        }
        
        // --- Dibuixem l'Explosió de Fons ---
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
                
                float w = img.rectTransform.rect.width; float h = img.rectTransform.rect.height;
                if (w > 0 && h > 0) {
                    float fitRatio = Mathf.Min(400f / w, 400f / h); // Un xic més gran perquè l'explosió tapi bé l'esfera
                    img.rectTransform.sizeDelta = new Vector2(w * fitRatio, h * fitRatio);
                }
            }
            else
            {
                // Si l'usuari s'ha oblidat de posar l'imatge a l'Inspector del Prefab, fem un quadrat taronja d'emergència
                img.color = new Color(1f, 0.4f, 0f, 0.8f);
                img.rectTransform.sizeDelta = new Vector2(300, 300);
                Debug.LogWarning("Avís: Tens la casella de l'Explosion Sprite completament buida a dins el teu Prefab 'SkillCheckUI'!");
            }
            
            // L'ancorem al bell mig
            img.rectTransform.anchoredPosition = Vector2.zero;

            // Posem l'explosió de primeres al davant de la rodona negra del taulell!
            expGo.transform.SetAsLastSibling(); 
        }

        // --- Modifiquem el text perquè sigui super cridaner i mai s'amagui ---
        if (damageText != null)
        {
            if (customFont != null) damageText.font = customFont;
            
            // Força la destrucció de qualsevol limitador heretat
            damageText.enableAutoSizing = false;
            damageText.textWrappingMode = TextWrappingModes.NoWrap; // NOU: Una sola línia sempre
            damageText.overflowMode = TMPro.TextOverflowModes.Overflow;
            
            damageText.alignment = TMPro.TextAlignmentOptions.Center;
            damageText.fontSize = 200; // Text massiu i més gran encara (de 150 a 200)
            damageText.fontStyle = TMPro.FontStyles.Bold;

            // Fiquem la caixa on viu el text a mida exagerada pq MAI faci clipping 
            RectTransform textRt = damageText.GetComponent<RectTransform>();
            if (textRt != null)
            {
                textRt.sizeDelta = new Vector2(800, 400); // Super gran
                textRt.anchoredPosition = Vector2.zero; // Ho centrem obligatòriament a sobre de l'explosió
                textRt.localScale = Vector3.one;
            }

            // Afegim resseguit blanc al voltant de les lletres
            if (damageText.GetComponent<Outline>() == null)
            {
                Outline outline = damageText.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.white; // Resseguit Blanc lluminós
                outline.effectDistance = new Vector2(3, -3); // Ajustament de perfil net
                
                // També pots afegir un component exclusiu per fer un segon reforç
                Outline outline2 = damageText.gameObject.AddComponent<Outline>();
                outline2.effectColor = Color.white;
                outline2.effectDistance = new Vector2(-3, 3);
            }

            // Movem el text endavant del tot a la jerarquia per evitar que res el topi
            damageText.transform.SetAsLastSibling();
            
            // Important: demanem que es redibuixi a la targeta gràfica ja!
            damageText.ForceMeshUpdate();
        }

        // --- Animació de mostrar Resultat ---
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
            // Deixem un segon perquè es vegi el text clarament i notem l'impacte
            yield return new WaitForSeconds(0.8f);
        }

        // --- Animació de Sortida (Slide Cap a la dreta) ---
        RectTransform rtOut = skillCheckTransform as RectTransform;
        if (rtOut != null)
        {
            Vector2 startPosOut = rtOut.anchoredPosition;
            Vector2 targetPosOut = startPosOut + new Vector2(1200f, 0f); // Fora d'escena a la dreta
            Vector3 startingScaleOut = rtOut.localScale;

            float exitTimer = 0f;
            float exitDuration = 0.35f;
            
            while (exitTimer < exitDuration)
            {
                exitTimer += Time.deltaTime;
                float pct = exitTimer / exitDuration;
                // Ease In Cubic per marxar esvaint-se ràpidament amb trajectòria
                float easeIn = pct * pct * pct;
                
                rtOut.anchoredPosition = Vector2.Lerp(startPosOut, targetPosOut, easeIn);
                rtOut.localScale = Vector3.Lerp(startingScaleOut, Vector3.one, easeIn); // Tornar pes per precaució de dibuix
                
                yield return null;
            }
            rtOut.anchoredPosition = targetPosOut;
        }

        if (skillCheckTransform != null) skillCheckTransform.gameObject.SetActive(false); // Amaguem completament

        // Informem al Combat Manager del dany triat just quan la interfície ja ha desaparegut per la dreta
        onDamageCalculated?.Invoke(finalDamage);
    }
}

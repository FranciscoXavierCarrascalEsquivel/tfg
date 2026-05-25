using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Gestiona la seqüència cinemàtica d'introducció del joc (Intro Cutscene).
/// Controla la posició inicial del jugador (estirat a terra), canviant manualment els sprites
/// del personatge i deshabilitant el seu Animator durant les fases de diàleg de terra,
/// assegut i dret. Executa efectes de sacsejada (tremolors de d'esforç) per transmetre la sensació
/// que el personatge s'està despertant i intentant aixecar, i utilitza Reflexió (Reflection)
/// per forçar la direcció final de mirada (lastDir) del jugador cap a la dreta de forma neta.
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    [Header("Sprites de la Cinemàtica")]
    [Tooltip("L'sprite del jugador estirat al terra")]
    [SerializeField] private Sprite playerOnFloorSprite;
    [Tooltip("L'sprite del jugador estant assegut")]
    [SerializeField] private Sprite playerSittingSprite;
    
    [Header("Seqüències de Diàlegs")]
    [Tooltip("Diàlegs quan està estirat al terra")]
    [SerializeField] private Interactable.DialogueLine[] dialogueWhileOnFloor;
    [Tooltip("Diàlegs després de canviar a l'sprite d'estar assegut")]
    [SerializeField] private Interactable.DialogueLine[] dialogueWhileSitting;
    [Tooltip("Diàlegs després de posar-se dret")]
    [SerializeField] private Interactable.DialogueLine[] dialogueWhileStanding;
    
    [Header("Efectes Tremolor i Àudio")]
    [Tooltip("Temps que estarà tremolant abans de fer l'esforç d'aixecar-se")]
    [SerializeField] private float trembleDuration = 0.5f;
    [Tooltip("Intensitat del tremolor (desviació de posició original)")]
    [SerializeField] private float trembleIntensity = 0.04f;
    [Tooltip("So opcional que sonarà en fer l'esforç d'aixecar-se")]
    [SerializeField] private AudioClip standUpSound;

    [Header("Temps d'Espera (Ritme Humà)")]
    [Tooltip("Pausa inicial abans de dir la primera paraula al terra")]
    [SerializeField] private float delayBeforeFirstDialogue = 0.5f;
    [Tooltip("Pausa després de seure, just abans de parlar la segon part")]
    [SerializeField] private float delayBeforeSecondDialogue = 0.5f;
    [Tooltip("Pausa de silenci just abans de fer l'últim esforç per aixecar-se")]
    [SerializeField] private float delayBeforeFinalTremble = 0.3f;
    [Tooltip("Pausa després de posar-se dret, just abans de parlar la tercera part")]
    [SerializeField] private float delayBeforeThirdDialogue = 0.5f;

    [Header("Opcions Generals")]
    [Tooltip("Si es marca, la cinemàtica només s'executarà una vegada en tota la sessió.")]
    [SerializeField] private bool playOnlyOnce = true;

    private PlayerController2D playerRef;
    private Animator playerAnim;
    private SpriteRenderer playerSprite;
    
    private bool hasPlayed = false;

    private void Awake()
    {
        // Durant la càrrega inicial, modifiquem els sprites a nivell d'Awake per evitar parpellejos (flickering) del personatge de peu
        if (playOnlyOnce && hasPlayed) return;

        playerRef = FindFirstObjectByType<PlayerController2D>();
        if (playerRef != null)
        {
            playerAnim = playerRef.GetComponent<Animator>();
            playerSprite = playerRef.GetComponent<SpriteRenderer>();

            // Desactivem temporalment l'Animator del model perquè no aixafi l'sprite estirat
            if (playerAnim != null) playerAnim.enabled = false;
            if (playerSprite != null && playerOnFloorSprite != null)
            {
                playerSprite.sprite = playerOnFloorSprite;
            }
        }
    }

    private void Start()
    {
        if (playOnlyOnce && hasPlayed) return;
        
        if (playerRef != null) 
        {
            playerRef.LockMovement(); // Congelem el jugador abans d'iniciar la seqüència
        }
        
        StartCoroutine(PlayCutscene());
    }

    /// <summary>
    /// Corrutina mestra que orquestra tota la cinemàtica d'inici frame a frame.
    /// </summary>
    private IEnumerator PlayCutscene()
    {
        hasPlayed = true;

        if (playerRef == null) yield break;

        DialogueUI dialogueUI = FindFirstObjectByType<DialogueUI>();

        // ==========================================
        // 1. FASE ESTIRAT AL TERRA
        // ==========================================
        if (delayBeforeFirstDialogue > 0f) yield return new WaitForSeconds(delayBeforeFirstDialogue);
        
        if (dialogueUI != null && dialogueWhileOnFloor != null && dialogueWhileOnFloor.Length > 0)
        {
            yield return StartCoroutine(PlayDialogueAndWait(dialogueUI, dialogueWhileOnFloor));
            // Si el jugador ha decidit saltar (Skip) els diàlegs de la introducció
            if (dialogueUI.WasSkipped)
            {
                yield return StartCoroutine(CompleteCutsceneRoutine());
                yield break;
            }
        }
        
        // Tremolor de sacsejada d'intent d'esforç
        yield return StartCoroutine(DoTrembleAndSound());
        
        // ==========================================
        // 2. FASE ASSEGUT
        // ==========================================
        if (playerSprite != null && playerSittingSprite != null)
        {
            playerSprite.sprite = playerSittingSprite;
        }

        if (delayBeforeSecondDialogue > 0f) yield return new WaitForSeconds(delayBeforeSecondDialogue);

        if (dialogueUI != null && dialogueWhileSitting != null && dialogueWhileSitting.Length > 0)
        {
            yield return StartCoroutine(PlayDialogueAndWait(dialogueUI, dialogueWhileSitting));
            if (dialogueUI.WasSkipped)
            {
                yield return StartCoroutine(CompleteCutsceneRoutine());
                yield break;
            }
        }

        // ==========================================
        // 3. SEGON TREMOLOR I ESFORÇ FINAL
        // ==========================================
        if (delayBeforeFinalTremble > 0f) yield return new WaitForSeconds(delayBeforeFinalTremble);
        
        yield return StartCoroutine(DoTrembleAndSound());

        // ==========================================
        // 4. AIXECAR-SE D'EMPEUS (Orientació cap a la dreta)
        // ==========================================
        if (playerAnim != null) 
        {
            playerAnim.enabled = true; // Reactivem el sistema d'animacions normal
            
            // Per comoditat en la posada en escena, utilitzem Reflexió per a sobreescriure 
            // la direcció de mirada del personatge i fer que miri a la dreta sense canviar cap codi de control
            FieldInfo lastDirField = typeof(PlayerController2D).GetField("lastDir", BindingFlags.NonPublic | BindingFlags.Instance);
            if (lastDirField != null)
            {
                lastDirField.SetValue(playerRef, new Vector2(1f, 0f));
            }
        }

        // ==========================================
        // 5. FASE DRET
        // ==========================================
        if (delayBeforeThirdDialogue > 0f) yield return new WaitForSeconds(delayBeforeThirdDialogue);

        if (dialogueUI != null && dialogueWhileStanding != null && dialogueWhileStanding.Length > 0)
        {
            yield return StartCoroutine(PlayDialogueAndWait(dialogueUI, dialogueWhileStanding));
            if (dialogueUI.WasSkipped)
            {
                yield return StartCoroutine(CompleteCutsceneRoutine());
                yield break;
            }
        }

        // ==========================================
        // 6. FINAL DE LA CINEMÀTICA (Retorn de control)
        // ==========================================
        yield return StartCoroutine(CompleteCutsceneRoutine());
    }

    /// <summary>
    /// Corrutina de tancament segur de la cinemàtica, assegurant que el personatge quedi despert,
    /// d'empeus mirant en la direcció correcta i amb els controls de moviment totalment actius.
    /// </summary>
    private IEnumerator CompleteCutsceneRoutine()
    {
        // En cas que s'hagi saltat (Skip) a la meitat, forcem l'aixecament amb una sacsejada de seguretat
        if (playerAnim != null && !playerAnim.enabled) 
        {
            yield return StartCoroutine(DoTrembleAndSound());

            playerAnim.enabled = true;
            FieldInfo lastDirField = typeof(PlayerController2D).GetField("lastDir", BindingFlags.NonPublic | BindingFlags.Instance);
            if (lastDirField != null)
            {
                lastDirField.SetValue(playerRef, new Vector2(1f, 0f));
            }
        }
        playerRef.UnlockMovement();
    }

    /// <summary>
    /// Realitza un moviment oscil·lant frenètic i aleatori de la posició del personatge durant la sacsejada.
    /// </summary>
    private IEnumerator DoTrembleAndSound()
    {
        PlayerInteractor.IsShaking = true; // Bloquegem interaccions del món
        Vector3 originalPos = playerRef.transform.position;
        float elapsed = 0f;
        
        while (elapsed < trembleDuration)
        {
            elapsed += Time.deltaTime;
            float offsetX = Random.Range(-1f, 1f) * trembleIntensity;
            float offsetY = Random.Range(-1f, 1f) * trembleIntensity;
            
            playerRef.transform.position = originalPos + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }
        
        // Estabilitzem el jugador en la seva posició original estable
        playerRef.transform.position = originalPos;
        PlayerInteractor.IsShaking = false;

        // Reproduïm so d'esforç si n'hi ha un assignat
        if (standUpSound != null)
        {
            ItemSoundPlayer.Play(standUpSound);
        }
    }

    /// <summary>
    /// Envia el contingut de diàleg i es manté en bucle fins que rep la senyal de tancament.
    /// </summary>
    private IEnumerator PlayDialogueAndWait(DialogueUI dialogueUI, Interactable.DialogueLine[] dialogueLines)
    {
        bool dialogueFinished = false;
        
        System.Action onClosed = null;
        onClosed = () => 
        {
            dialogueFinished = true;
            dialogueUI.OnDialogueClosed -= onClosed; 
        };
        dialogueUI.OnDialogueClosed += onClosed;

        dialogueUI.StartDialogue(dialogueLines);

        while (!dialogueFinished)
        {
            yield return null;
        }
    }
}

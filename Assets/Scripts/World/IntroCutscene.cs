using System.Collections;
using System.Reflection;
using UnityEngine;

public class IntroCutscene : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("L'sprite del jugador estirat al terra")]
    [SerializeField] private Sprite playerOnFloorSprite;
    [Tooltip("L'sprite del jugador estant assegut")]
    [SerializeField] private Sprite playerSittingSprite;
    
    [Header("Diàlegs")]
    [Tooltip("Diàlegs quan està estirat al terra")]
    [SerializeField] private Interactable.DialogueLine[] dialogueWhileOnFloor;
    [Tooltip("Diàlegs després de canviar a l'sprite d'estar assegut")]
    [SerializeField] private Interactable.DialogueLine[] dialogueWhileSitting;
    [Tooltip("Diàlegs després de posar-se dret")]
    [SerializeField] private Interactable.DialogueLine[] dialogueWhileStanding;
    
    [Header("Efecte Tremolor & Audio")]
    [Tooltip("Temps que estarà tremolant abans de posar-se dret")]
    [SerializeField] private float trembleDuration = 0.5f;
    [Tooltip("Volum del brunzit (quant es desvia de la seva posició)")]
    [SerializeField] private float trembleIntensity = 0.04f;
    [Tooltip("So opcional que sonara al tremolar/aixecar-se")]
    [SerializeField] private AudioClip standUpSound;

    [Header("Temps d'Espera (Ritme)")]
    [Tooltip("Pausa inicial abans de dir la primera paraula al terra")]
    [SerializeField] private float delayBeforeFirstDialogue = 0.5f;
    [Tooltip("Pausa després de seure, just abans de parlar la segona part")]
    [SerializeField] private float delayBeforeSecondDialogue = 0.5f;
    [Tooltip("Pausa de silenci just abans de fer l'últim esforç per aixecar-se")]
    [SerializeField] private float delayBeforeFinalTremble = 0.3f;
    [Tooltip("Pausa després de posar-se dret, just abans de parlar la tercera part")]
    [SerializeField] private float delayBeforeThirdDialogue = 0.5f;

    [Header("Opcions generals")]
    [Tooltip("Si vols que la cinemàtica passi només 1 únic cop. Deixa'l desmarcat si estàs provant.")]
    [SerializeField] private bool playOnlyOnce = true;

    private PlayerController2D playerRef;
    private Animator playerAnim;
    private SpriteRenderer playerSprite;
    
    private static bool hasPlayed = false;

    private void Awake()
    {
        // Ens assegurem de canviar l'sprite INSTANTANÍAMENT durant la càrrega inicial!
        if (playOnlyOnce && hasPlayed) return;

        playerRef = FindFirstObjectByType<PlayerController2D>();
        if (playerRef != null)
        {
            playerAnim = playerRef.GetComponent<Animator>();
            playerSprite = playerRef.GetComponent<SpriteRenderer>();

            // Desactivem l'animator al primeríssim frame possible i canviem sprite
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
            playerRef.LockMovement(); // Em bloquegem aquí, quan el seu Awake segur ja està inicialitzat
        }
        
        StartCoroutine(PlayCutscene());
    }

    private IEnumerator PlayCutscene()
    {
        hasPlayed = true;

        if (playerRef == null) yield break;

        DialogueUI dialogueUI = FindFirstObjectByType<DialogueUI>();

        // 1. FASE ESTIRAT AL TERRA
        // Pausa inicial abans de parlar
        if (delayBeforeFirstDialogue > 0f) yield return new WaitForSeconds(delayBeforeFirstDialogue);
        
        if (dialogueUI != null && dialogueWhileOnFloor != null && dialogueWhileOnFloor.Length > 0)
        {
            yield return StartCoroutine(PlayDialogueAndWait(dialogueUI, dialogueWhileOnFloor));
            if (dialogueUI.WasSkipped)
            {
                yield return StartCoroutine(CompleteCutsceneRoutine());
                yield break;
            }
        }
        
        // PRIMER TREMOLOR ABANS DE SEURE
        yield return StartCoroutine(DoTrembleAndSound());
        
        // 2. FASE ASSEGUT
        if (playerSprite != null && playerSittingSprite != null)
        {
            playerSprite.sprite = playerSittingSprite;
        }

        // Segona pausa humana entre asseure's i parlar de nou
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

        // SEGON TREMOLOR ABANS D'AIXECAR-SE DEFINITIVAMENT
        // Pausa final de silenci
        if (delayBeforeFinalTremble > 0f) yield return new WaitForSeconds(delayBeforeFinalTremble);
        
        yield return StartCoroutine(DoTrembleAndSound());

        // 4. AIXECAR-SE I MIRAR A LA DRETA
        if (playerAnim != null) 
        {
            playerAnim.enabled = true; // Activar el sistema normal del jugador
            
            // Fem us de reflection per forçar l'animator i que faci que el personatge quedi mirant a la dreta 
            // per pura comoditat visual del jugador
            FieldInfo lastDirField = typeof(PlayerController2D).GetField("lastDir", BindingFlags.NonPublic | BindingFlags.Instance);
            if (lastDirField != null)
            {
                lastDirField.SetValue(playerRef, new Vector2(1f, 0f));
            }
        }

        // 5. FASE DRET
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

        // 6. FI! Tornem els controls
        yield return StartCoroutine(CompleteCutsceneRoutine());
    }

    private IEnumerator CompleteCutsceneRoutine()
    {
        // Si encara no s'ha aixecat (animator deshabilitat), fem l'animació d'esforç (tremolor)
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

    private IEnumerator DoTrembleAndSound()
    {
        Vector3 originalPos = playerRef.transform.position;
        float elapsed = 0f;
        
        while (elapsed < trembleDuration)
        {
            elapsed += Time.deltaTime;
            // Moviment aleatori frenètic en eixos X i Y per simular tremolor/esforç
            float offsetX = Random.Range(-1f, 1f) * trembleIntensity;
            float offsetY = Random.Range(-1f, 1f) * trembleIntensity;
            
            playerRef.transform.position = originalPos + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }
        
        // Assegurem que torni a la posició original estable exacta
        playerRef.transform.position = originalPos;

        // Reproduïm el so just al moment exacte d'acabar el tremolor i fer el canvi!
        if (standUpSound != null)
        {
            ItemSoundPlayer.Play(standUpSound);
        }
    }

    // Helper per encapsular l'espera de qualsevol diàleg llarg
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

        // Esperem bucle fins que ens avisin que ja no està obert
        while (!dialogueFinished)
        {
            yield return null;
        }
    }
}

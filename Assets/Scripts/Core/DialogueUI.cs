using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private GameObject panel;          // El panell que conté tot el diàleg.
    [SerializeField] private TMP_Text dialogueText;     // El component de text on s'escriu el contingut.
    [SerializeField] private Image portraitImage;       // La imatge on es mostra el retrat del personatge.
    [SerializeField] private Animator portraitAnimator; // Animator opcional si el retrat té animacions.

    [Header("Panel Animation")]
    [SerializeField] private RectTransform panelRect;   // Referència al RectTransform per moure el panell.
    [SerializeField] private CanvasGroup panelGroup;    // Per controlar l'opacitat del panell.
    [SerializeField] private float animDuration = 0.15f; // Durada de l'animació d'entrada/sortida.
    [SerializeField] private float slidePixels = 40f;    // Distància que es desplça el panell en l'animació.
    [SerializeField] private bool animateOnShow = true;  // Si s'ha d'animar en aparèixer.

    [Header("Typewriter")]
    [SerializeField] private float charsPerSecond = 40f;
    [SerializeField] private bool skipSpaces = true;
    [SerializeField] private int soundEveryNChars = 1;

    [Header("Typing Sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] typingClips;
    [Range(0f, 0.4f)][SerializeField] private float pitchRandom = 0.05f;
    [Range(0f, 1f)][SerializeField] private float volume = 0.8f;

    private Coroutine typingRoutine;
    private Coroutine animRoutine;

    private string fullText;
    private bool isOpen;
    private bool isTyping;
    private int typedCount;

    // ✅ Event per avisar quan es tanca el diàleg (quan s'acaben totes les línies)
    public System.Action OnDialogueClosed;

    private Vector2 shownPos;

    // -----------------------
    // ✅ SEQÜÈNCIA DE DIÀLEG
    // -----------------------
    private Interactable.DialogueLine[] sequence;
    private int seqIndex;
    private bool inSequence;

    private void Awake()
    {
        if (panel != null && panelRect == null) panelRect = panel.GetComponent<RectTransform>();
        if (panel != null && panelGroup == null) panelGroup = panel.GetComponent<CanvasGroup>();

        if (!audioSource) audioSource = GetComponent<AudioSource>();

        if (panel != null && panelGroup == null)
            panelGroup = panel.AddComponent<CanvasGroup>();

        if (panelRect != null) shownPos = panelRect.anchoredPosition;

        ForceHidden();
    }

    public bool IsOpen => isOpen;
    public bool IsTyping => isTyping;

    // ✅ Nou: iniciar diàleg amb múltiples línies
    public void StartDialogue(Interactable.DialogueLine[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            Hide(); // res a mostrar
            return;
        }

        sequence = lines;
        seqIndex = 0;
        inSequence = true;

        // Mostrem el panell i iniciem la primera línia amb animació d'entrada
        ShowInternal(sequence[0], playInAnim: true);
    }

    // ✅ Per compatibilitat: un sol missatge
    public void Show(string text, Sprite portrait = null, RuntimeAnimatorController portraitAnim = null)
    {
        inSequence = false;
        sequence = null;
        seqIndex = 0;

        // mostra normal (entrada si toca)
        ShowInternal(new Interactable.DialogueLine
        {
            text = text,
            portrait = portrait,
            portraitAnimator = portraitAnim
        }, playInAnim: true);
    }

    private void ShowInternal(Interactable.DialogueLine line, bool playInAnim)
    {
        isOpen = true;
        fullText = line?.text ?? "";
        typedCount = 0;

        // Retrat (sprite)
        if (portraitImage != null)
        {
            if (line != null && line.portrait != null)
                portraitImage.sprite = line.portrait;

            portraitImage.enabled = (portraitImage.sprite != null);
        }

        // Retrat animat (opcional)
        if (portraitAnimator != null)
        {
            // si ens passen controller, l'apliquem; si no, el deixem com estava
            if (line != null && line.portraitAnimator != null)
                portraitAnimator.runtimeAnimatorController = line.portraitAnimator;

            portraitAnimator.enabled = (portraitAnimator.runtimeAnimatorController != null);
        }

        if (panel != null) panel.SetActive(true);

        if (playInAnim && animateOnShow) PlayIn();
        else ForceShown();

        // typewriter
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeRoutine(fullText));
    }

    public void Hide()
    {
        isOpen = false;
        inSequence = false;
        sequence = null;
        seqIndex = 0;

        isTyping = false;

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;

        if (dialogueText) dialogueText.text = "";

        PlayOut();

        OnDialogueClosed?.Invoke();
    }

    // ✅ Ara fa: skip si escriu, sinó avança línia; només tanca quan s'acaben totes
    public void AdvanceOrSkip()
    {
        if (!isOpen) return;

        if (isTyping)
        {
            // Si estem escrivint, tallem la rutina i mostrem tot el text de cop
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;

            isTyping = false;
            if (dialogueText) dialogueText.text = fullText;
            return;
        }

        // si estem en seqüència, passem a la següent línia sense animació de sortida/entrada
        if (inSequence && sequence != null)
        {
            seqIndex++;

            if (seqIndex < sequence.Length)
            {
                // mateix panell, només canviem text/retrat i tornem a escriure
                ShowInternal(sequence[seqIndex], playInAnim: false);
                return;
            }

            // s'han acabat les línies → ara sí tanquem
            Hide();
            return;
        }

        // diàleg normal d'una sola línia
        Hide();
    }

    private IEnumerator TypeRoutine(string text)
    {
        isTyping = true;
        if (dialogueText) dialogueText.text = "";

        float delay = 1f / Mathf.Max(1f, charsPerSecond);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (dialogueText) dialogueText.text += c;

            bool shouldSound = true;
            if (skipSpaces && char.IsWhiteSpace(c)) shouldSound = false;

            if (shouldSound)
            {
                typedCount++;
                if (soundEveryNChars <= 1 || (typedCount % soundEveryNChars == 0))
                    PlayRandomTypingSound();
            }

            yield return new WaitForSecondsRealtime(delay);
        }

        isTyping = false;
        typingRoutine = null;
    }

    private void PlayRandomTypingSound()
    {
        if (!audioSource) return;
        if (typingClips == null || typingClips.Length == 0) return;

        var clip = typingClips[Random.Range(0, typingClips.Length)];
        float pitch = 1f + Random.Range(-pitchRandom, pitchRandom);

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    // -----------------------
    // Animació del panell
    // -----------------------
    private void PlayIn()
    {
        if (panelRect == null || panelGroup == null) return;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateIn());
    }

    private void PlayOut()
    {
        if (panelRect == null || panelGroup == null)
        {
            if (panel) panel.SetActive(false);
            return;
        }

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateOut());
    }

    private IEnumerator AnimateIn()
    {
        panelGroup.alpha = 0f;
        panelRect.anchoredPosition = shownPos - new Vector2(0f, slidePixels);

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, animDuration));

            float eased = 1f - Mathf.Pow(1f - u, 3f);

            panelGroup.alpha = eased;
            panelRect.anchoredPosition = Vector2.Lerp(
                shownPos - new Vector2(0f, slidePixels),
                shownPos,
                eased
            );

            yield return null;
        }

        panelGroup.alpha = 1f;
        panelRect.anchoredPosition = shownPos;
        animRoutine = null;
    }

    private IEnumerator AnimateOut()
    {
        float startAlpha = panelGroup.alpha;
        Vector2 startPos = panelRect.anchoredPosition;
        Vector2 endPos = shownPos - new Vector2(0f, slidePixels);

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, animDuration));

            float eased = Mathf.Pow(1f - u, 3f);

            panelGroup.alpha = Mathf.Lerp(0f, startAlpha, eased);
            panelRect.anchoredPosition = Vector2.Lerp(endPos, startPos, eased);

            yield return null;
        }

        panelGroup.alpha = 0f;
        panelRect.anchoredPosition = endPos;

        if (panel) panel.SetActive(false);

        animRoutine = null;
    }

    private void ForceHidden()
    {
        if (panelRect != null) panelRect.anchoredPosition = shownPos - new Vector2(0f, slidePixels);
        if (panelGroup != null) panelGroup.alpha = 0f;
        if (panel != null) panel.SetActive(false);

        if (dialogueText) dialogueText.text = "";
        isOpen = false;
        isTyping = false;
        inSequence = false;
    }

    private void ForceShown()
    {
        if (panelGroup != null) panelGroup.alpha = 1f;
        if (panelRect != null) panelRect.anchoredPosition = shownPos;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Seqüència de Finalització de la Demo (DemoEndSequence).
/// Aquest script és el cervell cinematogràfic que orquestra la transició de final de joc.
/// Bloqueja completament el control del jugador i de la música, genera un glitch amb sacsejada
/// de pantalla, desencadena una explosió digital i genera dinàmicament una animació retro de
/// transmissió de dades via xarxa (Ordinador A -> Ordinador B a través d'un sobre de correu).
/// Posteriorment, recull les estadístiques de la partida de l'inventari (Kills vs Recruits)
/// i calcula quin dels 4 finals ha assolit el jugador (Observer, Genocide, Pacifist o Mixed).
/// Finalment, manipula i personalitza la DialogueUI per mostrar un monòleg centrat en pantalla,
/// un art conceptual en primer pla, els crèdits finals de "To Be Continued" i carrega el menú principal.
/// </summary>
public class DemoEndSequence : MonoBehaviour
{
    [Header("Elements de la Interfície (UI)")]
    public CanvasGroup glitchCanvasGroup; // Grup de Canvas per controlar opacitat dels glitches i fons
    public Image glitchOverlay;           // Imatge de fons que pot canviar de blanc a negre
    public TextMeshProUGUI systemText;    // Text d'estat de terminal verda

    [Header("Sprites Personalitzats de l'Animació (Opcional)")]
    public Sprite hostASprite;           // Icona d'ordinador origen
    public Sprite hostBSprite;           // Icona d'ordinador destí
    public Sprite packetSprite;          // Icona del sobre de correu/dades
    public Sprite oblivionFinalSprite;   // Imatge genèrica per a l'Oblit

    [Header("Il·lustracions de cadascun dels Finals")]
    public Sprite endingObserverSprite;  // Final Observador (0 morts, 0 reclutats)
    public Sprite endingGenocideSprite;  // Final Genocida (només morts, tothom eliminat)
    public Sprite endingPacifistSprite;  // Final Pacifista (només reclutats, tothom estalviat)
    public Sprite endingMixedSprite;     // Final Mixte (una barreja de tots dos)

    [Header("Àudios i Efectes Sonors")]
    public AudioClip glitchSound;        // Soroll de glitch de circuit elèctric
    public AudioClip travelSound;        // Soroll continu de transferència
    public AudioClip packetArrivedSound; // Xiulet de connexió establerta
    public AudioClip explosionSound;     // Esclat final del bucle de codi
    public AudioClip finalMonologueMusic;// Música dramàtica de fons per al monòleg
    public AudioSource audioSource;      // Emissor d'àudio dedicat

    [Header("Sacsejada de Càmera")]
    public Camera mainCamera;            // Càmera principal que tremolarà
    public float shakeIntensity = 0.5f;  // Intensitat màxima del tremolor

    /// <summary>
    /// Punt d'entrada de la seqüència. Bloqueja controls i arrenca la corrutina mestra.
    /// </summary>
    public void StartEndingSequence()
    {
        // Setup de seguretat de l'AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) 
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Congelem el jugador i desactivem preventivament tots els seus sistemes d'input i detecció
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            player.LockMovement();
            player.enabled = false;
        }

        var movement = FindFirstObjectByType<PlayerMovement2D>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        var interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null)
        {
            interactor.enabled = false;
        }

        // Engeguem la seqüència
        StartCoroutine(SequenceRoutine());
    }

    /// <summary>
    /// Atura gradualment qualsevol font de música o bucles d'àudio actius en el mapa de joc actual.
    /// </summary>
    public void StopBackgroundMusic()
    {
        var sceneMusic = FindFirstObjectByType<SceneMusic>();
        if (sceneMusic != null) sceneMusic.StopMusic();
        
        var loopMusic = FindFirstObjectByType<TriggerMusicLoopSection2D>();
        if (loopMusic != null) StartCoroutine(loopMusic.FadeOutAndStop(0.5f));

        var mTriggers = FindObjectsByType<MusicChangeTrigger>(FindObjectsSortMode.None);
        foreach (var t in mTriggers) 
        {
            if (t.IsPlaying()) t.FadeOutAndStop(0.5f);
        }
    }

    /// <summary>
    /// Seqüència de passes temporitzades que componen la cinemàtica de final de la demo.
    /// </summary>
    private IEnumerator SequenceRoutine()
    {
        var dialogueUI = FindFirstObjectByType<DialogueUI>();
        
        // Bloquegem el panell de diàleg perquè el jugador no pugui tancar-lo abans d'hora premint la barra o la E
        if (dialogueUI != null)
        {
            dialogueUI.canAdvance = false;
            dialogueUI.canSkip = false;
        }

        // ==========================================
        // 1. INICI DEL GLITCH I SACSEJADA
        // ==========================================
        if (audioSource != null && glitchSound != null)
        {
            audioSource.clip = glitchSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        glitchCanvasGroup.alpha = 0f; 

        Vector3 originalCamPos = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        float glitchDuration = 4.5f; // Durada dels tremolors mentre la línia de diàleg d'esclat és en pantalla
        float elapsed = 0f;

        while (elapsed < glitchDuration)
        {
            elapsed += Time.deltaTime;

            if (mainCamera != null)
            {
                float offsetX = Random.Range(-shakeIntensity, shakeIntensity);
                float offsetY = Random.Range(-shakeIntensity, shakeIntensity);
                mainCamera.transform.position = originalCamPos + new Vector3(offsetX, offsetY, 0);
            }

            yield return null;
        }

        // Tanquem de forma forçada la interfície de diàlegs ordinaris i en restaurem els valors normals
        if (dialogueUI != null)
        {
            dialogueUI.Hide();
            dialogueUI.canAdvance = true;
            dialogueUI.canSkip = true;
        }

        if (mainCamera != null) mainCamera.transform.position = originalCamPos;
        if (audioSource != null) audioSource.Stop();

        // ==========================================
        // 2. DETONACIÓ FÍSICA I TRANSICIÓ A FOS
        // ==========================================
        if (audioSource != null && explosionSound != null)
        {
            audioSource.PlayOneShot(explosionSound);
        }

        glitchOverlay.color = Color.white;
        systemText.text = ""; 
        glitchCanvasGroup.alpha = 0f; 

        // Ràpid esclat de llum blanca (0.05 segons)
        float flashInDuration = 0.05f;
        float elapsedF = 0f;
        while (elapsedF < flashInDuration)
        {
            elapsedF += Time.deltaTime;
            glitchCanvasGroup.alpha = elapsedF / flashInDuration;
            yield return null;
        }
        glitchCanvasGroup.alpha = 1f;

        // Fos gradual de blanc a negre absolut
        float flashOutDuration = 2.5f;
        elapsedF = 0f;
        while (elapsedF < flashOutDuration)
        {
            elapsedF += Time.deltaTime;
            glitchOverlay.color = Color.Lerp(Color.white, Color.black, elapsedF / flashOutDuration);
            yield return null;
        }
        glitchOverlay.color = Color.black;

        yield return new WaitForSeconds(1f); // Pausa dramàtica en el buit fosc

        // ==========================================
        // 3. SECCIÓ TERMINAL GREEN (Transferència de Dades)
        // ==========================================
        systemText.color = new Color(0.2f, 0.8f, 0.2f); // Verd tipus matriu/terminal retro
        systemText.text = "ESTABLISHING CONNECTION TO NEW HOST...";

        yield return new WaitForSeconds(2f);
        systemText.text = "SENDING PACKET VIA EMAIL...";
        yield return new WaitForSeconds(1f);
        systemText.text = ""; 
        
        // -- CREACIÓ COMPONENT A COMPONENT DE L'ANIMACIÓ DE VIATGE EN PANTALLA --
        GameObject animContainer = new GameObject("TravelAnimation");
        animContainer.transform.SetParent(glitchCanvasGroup.transform, false);
        RectTransform animRT = animContainer.AddComponent<RectTransform>();
        animRT.anchorMin = new Vector2(0, 0);
        animRT.anchorMax = new Vector2(1, 1);
        animRT.offsetMin = animRT.offsetMax = Vector2.zero;
        
        CanvasGroup animCanvasGroup = animContainer.AddComponent<CanvasGroup>();
        animCanvasGroup.alpha = 0f; 

        // Dibuix de la Línia de Viatge (Cable)
        GameObject lineObj = new GameObject("ConnectionLine");
        lineObj.transform.SetParent(animRT, false);
        RectTransform lineRT = lineObj.AddComponent<RectTransform>();
        lineRT.sizeDelta = new Vector2(800f, 6f);
        lineRT.anchoredPosition = new Vector2(0, -50f);
        Image lineImg = lineObj.AddComponent<Image>();
        lineImg.color = new Color(0.2f, 0.6f, 0.2f, 0.5f);

        // Instanciació de l'Ordinador A (Esquerra)
        GameObject hostA = new GameObject("HostA");
        hostA.transform.SetParent(animRT, false);
        RectTransform hostART = hostA.AddComponent<RectTransform>();
        hostART.sizeDelta = new Vector2(150f, 150f);
        hostART.anchoredPosition = new Vector2(-400f, -50f);
        Image hostAImg = hostA.AddComponent<Image>();
        if (hostASprite != null) hostAImg.sprite = hostASprite;
        else hostAImg.color = Color.gray;
        hostAImg.preserveAspect = true;

        // Ombrejat verd actiu inicial
        Outline outlineA = hostA.AddComponent<Outline>();
        outlineA.effectColor = new Color(0.2f, 0.8f, 0.2f, 1f); 
        outlineA.effectDistance = new Vector2(4f, -4f);
        
        GameObject textAGO = new GameObject("TextA");
        textAGO.transform.SetParent(hostART, false);
        TextMeshProUGUI textA = textAGO.AddComponent<TextMeshProUGUI>();
        textA.text = "LOCAL\nHOST";
        textA.alignment = TextAlignmentOptions.Center;
        textA.color = Color.white;
        textA.fontSize = 24;
        SetFont(textA);
        textA.rectTransform.anchoredPosition = new Vector2(0, 100f);

        // Instanciació de l'Ordinador B (Dreta)
        GameObject hostB = new GameObject("HostB");
        hostB.transform.SetParent(animRT, false);
        RectTransform hostBRT = hostB.AddComponent<RectTransform>();
        hostBRT.sizeDelta = new Vector2(150f, 150f);
        hostBRT.anchoredPosition = new Vector2(400f, -50f);
        Image hostBImg = hostB.AddComponent<Image>();
        if (hostBSprite != null) hostBImg.sprite = hostBSprite;
        else hostBImg.color = Color.gray;
        hostBImg.preserveAspect = true;

        GameObject textBGO = new GameObject("TextB");
        textBGO.transform.SetParent(hostBRT, false);
        TextMeshProUGUI textB = textBGO.AddComponent<TextMeshProUGUI>();
        textB.text = "DESTINATION\nHOST";
        textB.alignment = TextAlignmentOptions.Center;
        textB.color = Color.white;
        textB.fontSize = 24;
        SetFont(textB);
        textB.rectTransform.anchoredPosition = new Vector2(0, 100f);

        // Sobre de correu volant (Paquet de dades)
        GameObject packetObj = new GameObject("EmailPacket");
        packetObj.transform.SetParent(animRT, false);
        RectTransform packetRT = packetObj.AddComponent<RectTransform>();
        packetRT.sizeDelta = new Vector2(60f, 50f);
        packetRT.anchoredPosition = new Vector2(-400f, -50f);
        Image packetImg = packetObj.AddComponent<Image>();
        if (packetSprite != null) packetImg.sprite = packetSprite;
        else packetImg.color = Color.yellow;
        packetImg.preserveAspect = true;

        // FADE IN del quadre d'animació complet
        float fadeDuration = 1.5f;
        float fElapsed = 0f;
        while (fElapsed < fadeDuration)
        {
            fElapsed += Time.deltaTime;
            animCanvasGroup.alpha = fElapsed / fadeDuration;
            yield return null;
        }
        animCanvasGroup.alpha = 1f;

        if (outlineA != null) Destroy(outlineA);

        if (audioSource != null && travelSound != null)
        {
            audioSource.clip = travelSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Animar el trajecte del sobre de A a B (7 segons, lent per generar tensió)
        float travelDuration = 7f; 
        float tElapsed = 0f;
        Vector2 startPos = new Vector2(-400f, -50f);
        Vector2 endPos = new Vector2(400f, -50f);

        while (tElapsed < travelDuration)
        {
            tElapsed += Time.deltaTime;
            float t = tElapsed / travelDuration;
            
            packetRT.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            
            // Efecte estroboscòpic/parpelleig del canal verd
            lineImg.color = new Color(0.2f, 0.8f, 0.2f, Random.Range(0.4f, 1f));

            yield return null;
        }

        packetRT.anchoredPosition = endPos;
        
        if (audioSource != null && audioSource.clip == travelSound)
        {
            audioSource.Stop();
        }

        if (audioSource != null && packetArrivedSound != null)
        {
            audioSource.PlayOneShot(packetArrivedSound);
        }

        // Ombrejat verd actiu a l'ordinador B de destí
        Outline outlineB = hostB.AddComponent<Outline>();
        outlineB.effectColor = new Color(0.2f, 0.8f, 0.2f, 1f); 
        outlineB.effectDistance = new Vector2(4f, -4f);

        yield return new WaitForSeconds(2f);

        // FADE OUT de la connexió
        fElapsed = 0f;
        while (fElapsed < fadeDuration)
        {
            fElapsed += Time.deltaTime;
            animCanvasGroup.alpha = 1f - (fElapsed / fadeDuration);
            yield return null;
        }

        systemText.text = "";
        Destroy(animContainer); 
        
        yield return new WaitForSeconds(1.5f);

        // ==========================================
        // 4. PROCESSAMENT DE CÀLCULS I TRIADE DE FINALS
        // ==========================================
        int totalKills = 0;
        int totalRecruits = 0;
        int totalMaxPopulation = 0;

        // Obtenim de forma robusta la població total per comprovar final Pacifista/Genocida absolut
        EnemyProfile[] allEnemies = Resources.LoadAll<EnemyProfile>("Enemies");
        foreach (var p in allEnemies)
        {
            totalMaxPopulation += p.maxRecruitLimit;
        }
        
        // Es treu de l'equació el boss per mantenir-se en els paràmetres de reclutament de l'Overworld ordinari
        totalMaxPopulation = Mathf.Max(1, totalMaxPopulation - 1);
        
        if (PlayerInventory.Instance != null)
        {
            foreach (var kv in PlayerInventory.Instance.KilledEnemies) totalKills += kv.Value;
            foreach (var kv in PlayerInventory.Instance.RecruitedEnemies) totalRecruits += kv.Value;
        }

        string finalMonologue = "";
        Sprite finalSprite = null;

        // Triem diàlegs i gràfics segons les accions i l'ètica de joc de l'usuari
        if (totalKills == 0 && totalRecruits == 0)
        {
            // final 1: OBSERVADOR (Passivitat absoluta)
            finalMonologue = "A true observer. You drifted through this world without taking a single life, nor saving one. You simply watched as the code unfolded. Did you think your inaction makes you innocent? When the Great Deletion comes, your hands will be clean, but you will be just as empty.";
            finalSprite = endingObserverSprite;
        }
        else if (totalRecruits == 0 && totalKills >= totalMaxPopulation && totalMaxPopulation > 0)
        {
            // final 2: GENOCIDA (Neteja total)
            finalMonologue = "Oh, look at you... tearing through them like a virus. You've purged every single one of them. You're doing my job for me, little anomaly! If you keep slaughtering with such efficiency, perhaps when the Great Deletion comes, you will be rewarded. It's so entertaining to watch the world bleed out through your hands.";
            finalSprite = endingGenocideSprite;
        }
        else if (totalKills == 0 && totalRecruits >= totalMaxPopulation && totalMaxPopulation > 0)
        {
            // final 3: PACIFISTA / RECLUTADOR (Amistat i rescat total)
            finalMonologue = "Hah. You actually think saving every single one of them matters? You've collected them like toys, believing you can rescue a world already marked for deletion. It's almost poetic... how desperately you fight the inevitable. But make no mistake: your little 'friends' will be erased just the same. And I will be watching.";
            finalSprite = endingPacifistSprite;
        }
        else
        {
            // final 4: MIXTE (Gris/Pragmàtic)
            finalMonologue = "How amusing. You spare some, yet you ruthlessly delete others when they stand in your way. You mock me, but every life you take only feeds your strength... making you more like ME. Keep playing the hero while you feast on their code. Who knows? One day, we might just be on the same side.";
            finalSprite = endingMixedSprite;
        }

        // Reprodueix el so dramàtic propi del monòleg
        if (audioSource != null && finalMonologueMusic != null)
        {
            audioSource.clip = finalMonologueMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        // ==========================================
        // 5. MANIPULACIÓ EXCLUSIVA DE DIÀLEG I TEATRALITAT
        // ==========================================
        if (dialogueUI != null)
        {
            // Ocultem la targeta de diàleg convencional
            var panelTransform = dialogueUI.transform.Find("DynamicDialoguePanel");
            if (panelTransform != null)
            {
                var bgImage = panelTransform.GetComponent<Image>();
                if (bgImage != null) bgImage.enabled = false;
            }

            // Reduïm la velocitat de text a nivells didàctics molt expressius
            float originalSpeed = dialogueUI.charsPerSecond;
            dialogueUI.charsPerSecond = 10f; 

            Interactable.DialogueLine oblivLine = new Interactable.DialogueLine();
            oblivLine.speakerName = ""; 
            oblivLine.text = finalMonologue;
            oblivLine.delayBeforeLine = 0f;
            oblivLine.showOnTop = false; 

            dialogueUI.canAdvance = false;

            // Arrenquem la bafarada modificant paràmetres visuals internament
            dialogueUI.StartDialogue(new Interactable.DialogueLine[] { oblivLine }, animateIn: true);

            if (dialogueUI.dialogueText != null)
            {
                dialogueUI.dialogueText.alignment = TextAlignmentOptions.Center;
            }

            GameObject oblImgObj = null;
            Image oblImg = null;

            var dynamicPanel = GameObject.Find("DynamicDialoguePanel");
            if (dynamicPanel != null)
            {
                // Forcem que el panell de diàleg s'estiri a pantalla sencera per a un centrat de text correcte
                var panelRT = dynamicPanel.GetComponent<RectTransform>();
                if (panelRT != null)
                {
                    panelRT.anchorMin = Vector2.zero;
                    panelRT.anchorMax = Vector2.one;
                    panelRT.offsetMin = Vector2.zero;
                    panelRT.offsetMax = Vector2.zero;
                }

                // Ubiquem el bloc de text a la meitat inferior
                if (dialogueUI.dialogueText != null)
                {
                    var textRT = dialogueUI.dialogueText.GetComponent<RectTransform>();
                    if (textRT != null)
                    {
                        textRT.anchorMin = new Vector2(0.1f, 0.05f);
                        textRT.anchorMax = new Vector2(0.9f, 0.6f);
                        textRT.offsetMin = Vector2.zero;
                        textRT.offsetMax = Vector2.zero;
                    }
                }

                var bgImage = dynamicPanel.GetComponent<Image>();
                if (bgImage != null) bgImage.enabled = false;
                
                var bgOutline = dynamicPanel.GetComponent<Outline>();
                if (bgOutline != null) bgOutline.enabled = false;
                
                var divider = dynamicPanel.transform.Find("DividerLine");
                if (divider != null) divider.gameObject.SetActive(false);

                // Creem el quadre per dibuixar la il·lustració final en gran a la meitat superior de la pantalla
                oblImgObj = new GameObject("OblivionImage");
                oblImgObj.transform.SetParent(dynamicPanel.transform, false);
                var oblRT = oblImgObj.AddComponent<RectTransform>();
                oblRT.anchorMin = new Vector2(0.5f, 0.75f);
                oblRT.anchorMax = new Vector2(0.5f, 0.75f);
                oblRT.anchoredPosition = new Vector2(0f, 0f); 
                oblRT.sizeDelta = new Vector2(700f, 700f); 
                oblImg = oblImgObj.AddComponent<Image>();
                
                if (finalSprite != null) oblImg.sprite = finalSprite;
                else if (oblivionFinalSprite != null) oblImg.sprite = oblivionFinalSprite; 
                
                oblImg.preserveAspect = true;
                oblImg.color = new Color(1, 1, 1, 0f); // Inicialment invisible per fer fade-in amb el text
            }

            // FADE IN DE LA IMATGE: augmenta opacitat en paral·lel al ritme d'escriptura del text
            float typingTime = (float)finalMonologue.Length / 10f; 
            float el = 0f;
            
            while (dialogueUI.IsOpen && dialogueUI.IsTyping)
            {
                el += Time.deltaTime;
                float progress = Mathf.Clamp01(el / typingTime);
                
                // Corba d'acceleració cúbica per fer el fade-in de l'art molt dramàtic i fluid
                float easedAlpha = Mathf.Pow(progress, 3f); 
                
                if (oblImg != null) oblImg.color = new Color(1, 1, 1, easedAlpha);
                yield return null;
            }

            if (oblImg != null) oblImg.color = new Color(1, 1, 1, 1f);

            // Quedem bloquejats a l'espera que l'usuari finalitzi premint la tecla d'acció ('E')
            while (!Input.GetKeyDown(KeyCode.E))
            {
                yield return null;
            }

            // FADE OUT global del text i la imatge
            float fadeOutDur = 2f;
            float f = 0f;
            var txt = dialogueUI.dialogueText;
            Color origTxtColor = txt != null ? txt.color : Color.white;

            while (f < fadeOutDur)
            {
                f += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (f / fadeOutDur));
                if (txt != null) txt.color = new Color(origTxtColor.r, origTxtColor.g, origTxtColor.b, alpha);
                if (oblImg != null) oblImg.color = new Color(1, 1, 1, alpha);
                yield return null;
            }

            dialogueUI.canAdvance = true;
            dialogueUI.Hide();
            dialogueUI.charsPerSecond = originalSpeed; // Restaurem la velocitat de text

            yield return new WaitForSeconds(1.5f);

            // ==========================================
            // 6. TARGETA DE CRÈDITS (To Be Continued...)
            // ==========================================
            GameObject tbcObj = new GameObject("ToBeContinuedText");
            tbcObj.transform.SetParent(systemText.transform.parent, false);
            var tbcRT = tbcObj.AddComponent<RectTransform>();
            tbcRT.anchorMin = Vector2.zero;
            tbcRT.anchorMax = Vector2.one;
            tbcRT.offsetMin = Vector2.zero;
            tbcRT.offsetMax = Vector2.zero;
            var tbcText = tbcObj.AddComponent<TextMeshProUGUI>();
            tbcText.font = systemText.font;
            tbcText.alignment = TextAlignmentOptions.Center;
            tbcText.fontSize = 80;
            tbcText.color = new Color(1, 1, 1, 0f);
            tbcText.text = "TO BE CONTINUED...";
            SetFont(tbcText);

            float tbcFadeIn = 2f;
            float timeCount = 0f;
            while(timeCount < tbcFadeIn)
            {
                timeCount += Time.deltaTime;
                tbcText.color = new Color(1, 1, 1, Mathf.Clamp01(timeCount / tbcFadeIn));
                yield return null;
            }
            tbcText.color = Color.white;

            yield return new WaitForSeconds(1f);

            // SUBTÍTOL SORPRESA (Or Not)
            GameObject orNotObj = new GameObject("OrNotText");
            orNotObj.transform.SetParent(tbcObj.transform, false);
            var orRT = orNotObj.AddComponent<RectTransform>();
            orRT.anchorMin = new Vector2(0.5f, 0.5f);
            orRT.anchorMax = new Vector2(0.5f, 0.5f);
            orRT.anchoredPosition = new Vector2(0f, -80f);
            orRT.sizeDelta = new Vector2(800f, 100f);
            var orText = orNotObj.AddComponent<TextMeshProUGUI>();
            orText.font = systemText.font;
            orText.alignment = TextAlignmentOptions.Center;
            orText.fontSize = 40;
            orText.color = new Color(1, 1, 1, 0f);
            orText.text = "OR NOT";
            SetFont(orText);

            float orFadeIn = 1.5f;
            timeCount = 0f;
            while(timeCount < orFadeIn)
            {
                timeCount += Time.deltaTime;
                orText.color = new Color(1, 1, 1, Mathf.Clamp01(timeCount / orFadeIn));
                yield return null;
            }
            orText.color = Color.white;

            yield return new WaitForSeconds(3f);

            // FADE OUT de la lletra i de la cançó de final simultàniament
            float finalFadeOut = 2f;
            float initialVolume = (audioSource != null) ? audioSource.volume : 0f;
            timeCount = 0f;
            while(timeCount < finalFadeOut)
            {
                timeCount += Time.deltaTime;
                float a = Mathf.Clamp01(1f - (timeCount / finalFadeOut));
                tbcText.color = new Color(1, 1, 1, a);
                orText.color = new Color(1, 1, 1, a);
                
                if (audioSource != null) audioSource.volume = initialVolume * a;
                
                yield return null;
            }

            if (audioSource != null) { audioSource.Stop(); audioSource.volume = initialVolume; }

            yield return new WaitForSeconds(1f);

            // Carrega de tornada segura del Menú Principal del joc
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    /// <summary>
    /// Intenta carregar la font pixelada retro en la build de forma robusta i segura de fallades.
    /// </summary>
    private void SetFont(TextMeshProUGUI t)
    {
        TMP_FontAsset f = null;
#if UNITY_EDITOR
        f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/determination SDF.asset")
            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/PixelOperator SDF.asset") 
            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/PixelOperator SDF.asset");
#endif
        if (f == null)
        {
            f = Resources.Load<TMP_FontAsset>("Fonts/determination SDF") 
                ?? Resources.Load<TMP_FontAsset>("determination SDF")
                ?? Resources.Load<TMP_FontAsset>("Fonts/PixelOperator SDF") 
                ?? Resources.Load<TMP_FontAsset>("PixelOperator SDF");
        }
        if (f != null) t.font = f;
    }
}

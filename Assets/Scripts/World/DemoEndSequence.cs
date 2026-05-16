using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DemoEndSequence : MonoBehaviour
{
    [Header("UI Elements")]
    public CanvasGroup glitchCanvasGroup;
    public Image glitchOverlay;
    public TextMeshProUGUI systemText;
    
    [Header("Sprites Personalitzats (Opcional)")]
    public Sprite hostASprite;
    public Sprite hostBSprite;
    public Sprite packetSprite;
    public Sprite oblivionFinalSprite;

    [Header("Audio")]
    public AudioClip glitchSound;
    public AudioClip travelSound;
    public AudioClip packetArrivedSound;
    public AudioClip explosionSound;
    public AudioClip finalMonologueMusic;
    public AudioSource audioSource;

    [Header("Camera Shake")]
    public Camera mainCamera;
    public float shakeIntensity = 0.5f;

    public void StartEndingSequence()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) 
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Bloquegem el jugador
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null) player.enabled = false;

        StartCoroutine(SequenceRoutine());
    }

    // Afegeix això al Unity Event del primer diàleg (o On Requirement Met) per parar la música de fons
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

    private IEnumerator SequenceRoutine()
    {
        var dialogueUI = FindFirstObjectByType<DialogueUI>();
        
        // Bloquegem el diàleg perquè l'usuari no el pugui tancar manualment
        if (dialogueUI != null)
        {
            dialogueUI.canAdvance = false;
            dialogueUI.canSkip = false;
        }

        // 1. Inici del Glitch (Tremolor i Soroll en Bucle MENTRE el diàleg és visible)
        if (audioSource != null && glitchSound != null)
        {
            audioSource.clip = glitchSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Assegurem que el canvas del glitch no tapa el diàleg encara, però deixem que la càmera tremoli
        glitchCanvasGroup.alpha = 0f; 

        Vector3 originalCamPos = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        float glitchDuration = 4.5f; // Temps que l'usuari té per llegir l'últim diàleg mentre tremola
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

        // Tancar el diàleg automàticament
        if (dialogueUI != null)
        {
            dialogueUI.Hide();
            // Restaurem els controls del diàleg pel futur
            dialogueUI.canAdvance = true;
            dialogueUI.canSkip = true;
        }

        // Aturar el tremolor i el so
        if (mainCamera != null) mainCamera.transform.position = originalCamPos;
        if (audioSource != null) audioSource.Stop();

        // 2. EXPLOSIÓ I FADE A NEGRE
        if (audioSource != null && explosionSound != null)
        {
            audioSource.PlayOneShot(explosionSound);
        }

        glitchOverlay.color = Color.white;
        systemText.text = ""; // Netegem qualsevol text previ
        glitchCanvasGroup.alpha = 0f; 

        // Flash In molt ràpid (blanca)
        float flashInDuration = 0.05f;
        float elapsedF = 0f;
        while (elapsedF < flashInDuration)
        {
            elapsedF += Time.deltaTime;
            glitchCanvasGroup.alpha = elapsedF / flashInDuration;
            yield return null;
        }
        glitchCanvasGroup.alpha = 1f;

        // Fade Out de blanc cap a negre lentament
        float flashOutDuration = 2.5f;
        elapsedF = 0f;
        while (elapsedF < flashOutDuration)
        {
            elapsedF += Time.deltaTime;
            glitchOverlay.color = Color.Lerp(Color.white, Color.black, elapsedF / flashOutDuration);
            yield return null;
        }
        glitchOverlay.color = Color.black;

        yield return new WaitForSeconds(1f); // Pausa dramàtica en negre

        systemText.color = new Color(0.2f, 0.8f, 0.2f); // Verd terminal
        systemText.text = "ESTABLISHING CONNECTION TO NEW HOST...";

        yield return new WaitForSeconds(2f);
        systemText.text = "SENDING PACKET VIA EMAIL...";
        yield return new WaitForSeconds(1f);
        systemText.text = ""; // Amaguem el text durant l'animació per no embrutar
        
        // -- CREACIÓ DINÀMICA DE L'ANIMACIÓ DE VIATGE --
        GameObject animContainer = new GameObject("TravelAnimation");
        animContainer.transform.SetParent(glitchCanvasGroup.transform, false);
        RectTransform animRT = animContainer.AddComponent<RectTransform>();
        animRT.anchorMin = new Vector2(0, 0);
        animRT.anchorMax = new Vector2(1, 1);
        animRT.offsetMin = animRT.offsetMax = Vector2.zero;
        
        CanvasGroup animCanvasGroup = animContainer.AddComponent<CanvasGroup>();
        animCanvasGroup.alpha = 0f; // Comencem transparents per fer el Fade In

        // Crear Línia de Connexió
        GameObject lineObj = new GameObject("ConnectionLine");
        lineObj.transform.SetParent(animRT, false);
        RectTransform lineRT = lineObj.AddComponent<RectTransform>();
        lineRT.sizeDelta = new Vector2(800f, 6f);
        lineRT.anchoredPosition = new Vector2(0, -50f);
        Image lineImg = lineObj.AddComponent<Image>();
        lineImg.color = new Color(0.2f, 0.6f, 0.2f, 0.5f);

        // Crear Ordinador A (Esquerra)
        GameObject hostA = new GameObject("HostA");
        hostA.transform.SetParent(animRT, false);
        RectTransform hostART = hostA.AddComponent<RectTransform>();
        hostART.sizeDelta = new Vector2(150f, 150f);
        hostART.anchoredPosition = new Vector2(-400f, -50f);
        Image hostAImg = hostA.AddComponent<Image>();
        if (hostASprite != null) hostAImg.sprite = hostASprite;
        else hostAImg.color = Color.gray;
        hostAImg.preserveAspect = true;

        // Reseguir el d'origen (Outline) a l'inici
        Outline outlineA = hostA.AddComponent<Outline>();
        outlineA.effectColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Verd
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

        // Crear Ordinador B (Dreta)
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

        // Crear el Paquet (Correu)
        GameObject packetObj = new GameObject("EmailPacket");
        packetObj.transform.SetParent(animRT, false);
        RectTransform packetRT = packetObj.AddComponent<RectTransform>();
        packetRT.sizeDelta = new Vector2(60f, 50f);
        packetRT.anchoredPosition = new Vector2(-400f, -50f);
        Image packetImg = packetObj.AddComponent<Image>();
        if (packetSprite != null) packetImg.sprite = packetSprite;
        else packetImg.color = Color.yellow;
        packetImg.preserveAspect = true;

        // FADE IN DE L'ANIMACIÓ
        float fadeDuration = 1.5f;
        float fElapsed = 0f;
        while (fElapsed < fadeDuration)
        {
            fElapsed += Time.deltaTime;
            animCanvasGroup.alpha = fElapsed / fadeDuration;
            yield return null;
        }
        animCanvasGroup.alpha = 1f;

        // Treiem el verd de l'equip d'origen al començar a viatjar
        if (outlineA != null) Destroy(outlineA);

        // Reproduïm l'efecte de so de viatge només ara
        if (audioSource != null && travelSound != null)
        {
            audioSource.clip = travelSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Animar el sobre movent-se de A a B MÉS LENTAMENT
        float travelDuration = 7f; 
        float tElapsed = 0f;
        Vector2 startPos = new Vector2(-400f, -50f);
        Vector2 endPos = new Vector2(400f, -50f);

        while (tElapsed < travelDuration)
        {
            tElapsed += Time.deltaTime;
            float t = tElapsed / travelDuration;
            
            // Moviment suau i constant
            packetRT.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            
            // Parpelleig de la línia
            lineImg.color = new Color(0.2f, 0.8f, 0.2f, Random.Range(0.4f, 1f));

            yield return null;
        }

        packetRT.anchoredPosition = endPos;
        
        // Aturem el so de viatge només arribar
        if (audioSource != null && audioSource.clip == travelSound)
        {
            audioSource.Stop();
        }

        // Reproduïm l'àudio d'arribada si el tenim
        if (audioSource != null && packetArrivedSound != null)
        {
            audioSource.PlayOneShot(packetArrivedSound);
        }

        // Reseguir el de destí en acabar
        Outline outlineB = hostB.AddComponent<Outline>();
        outlineB.effectColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Verd
        outlineB.effectDistance = new Vector2(4f, -4f);

        yield return new WaitForSeconds(2f);

        // FADE OUT DE L'ANIMACIÓ DE VIATGE
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

        // 3. Monòleg final de l'Oblit amb els 4 finals
        int totalKills = 0;
        int totalRecruits = 0;
        int totalMaxPopulation = 0;

        // Carreguem tots els perfils d'enemic per calcular la població total del joc (sumant els seus límits)
        EnemyProfile[] allEnemies = Resources.LoadAll<EnemyProfile>("Enemies");
        foreach (var p in allEnemies)
        {
            totalMaxPopulation += p.maxRecruitLimit;
        }
        
        // Restem 1 per no comptar el boss final (o un enemic especial) segons petició de l'usuari
        totalMaxPopulation = Mathf.Max(1, totalMaxPopulation - 1);
        
        if (PlayerInventory.Instance != null)
        {
            foreach (var kv in PlayerInventory.Instance.KilledEnemies) totalKills += kv.Value;
            foreach (var kv in PlayerInventory.Instance.RecruitedEnemies) totalRecruits += kv.Value;
        }

        string finalMonologue = "";

        if (totalKills == 0 && totalRecruits == 0)
        {
            // Ignorant (No reclutar i no matar)
            finalMonologue = "A true observer. You drifted through this world without taking a single life, nor saving one. You simply watched as the code unfolded. Did you think your inaction makes you innocent? When the Great Deletion comes, your hands will be clean, but you will be just as empty.";
        }
        else if (totalRecruits == 0 && totalKills >= totalMaxPopulation && totalMaxPopulation > 0)
        {
            // Genocida (Matar-los a tots i no reclutar ningú)
            finalMonologue = "Oh, look at you... tearing through them like a virus. You've purged every single one of them. You're doing my job for me, little anomaly! If you keep slaughtering with such efficiency, perhaps when the Great Deletion comes, you will be rewarded. It's so entertaining to watch the world bleed out through your hands.";
        }
        else if (totalKills == 0 && totalRecruits >= totalMaxPopulation && totalMaxPopulation > 0)
        {
            // Reclutador (Reclutar a tothom i no matar ningú)
            finalMonologue = "Hah. You actually think saving every single one of them matters? You've collected them like toys, believing you can rescue a world already marked for deletion. It's almost poetic... how desperately you fight the inevitable. But make no mistake: your little 'friends' will be erased just the same. And I will be watching.";
        }
        else
        {
            // Mixte (Fer una mica de tot, o no arribar al màxim d'un sol tipus)
            finalMonologue = "How amusing. You spare some, yet you ruthlessly delete others when they stand in your way. You mock me, but every life you take only feeds your strength... making you more like ME. Keep playing the hero while you feast on their code. Who knows? One day, we might just be on the same side.";
        }

        // Nova música pel monòleg final
        if (audioSource != null && finalMonologueMusic != null)
        {
            audioSource.clip = finalMonologueMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (dialogueUI != null)
        {
            // Ocultem la targeta de fons
            var panelTransform = dialogueUI.transform.Find("DynamicDialoguePanel");
            if (panelTransform != null)
            {
                var bgImage = panelTransform.GetComponent<Image>();
                if (bgImage != null) bgImage.enabled = false;
            }

            // Fem el text molt més lent (guardem l'original per si de cas)
            float originalSpeed = dialogueUI.charsPerSecond;
            dialogueUI.charsPerSecond = 10f; // Bastant lent

            Interactable.DialogueLine oblivLine = new Interactable.DialogueLine();
            oblivLine.speakerName = ""; // Sense nom, directament l'Oblit
            // No necessitem \n\n perquè usarem TextAlignmentOptions.Center
            oblivLine.text = finalMonologue;
            oblivLine.delayBeforeLine = 0f;
            oblivLine.showOnTop = false; // Al mig de la pantalla en negre

            // Bloquegem que es pugui tancar automàticament o amb la E de forma estàndard
            dialogueUI.canAdvance = false;

            // Esperem que es mostri i comenci a escriure el diàleg final
            dialogueUI.StartDialogue(new Interactable.DialogueLine[] { oblivLine }, animateIn: true);

            // Ara que ja s'ha creat (StartDialogue), l'ocultem i centrem
            if (dialogueUI.dialogueText != null)
            {
                dialogueUI.dialogueText.alignment = TextAlignmentOptions.Center;
            }

            GameObject oblImgObj = null;
            Image oblImg = null;

            var dynamicPanel = GameObject.Find("DynamicDialoguePanel");
            if (dynamicPanel != null)
            {
                // Fem que el panell ocupi tota la pantalla perquè el text quedi al centre real
                var panelRT = dynamicPanel.GetComponent<RectTransform>();
                if (panelRT != null)
                {
                    panelRT.anchorMin = Vector2.zero;
                    panelRT.anchorMax = Vector2.one;
                    panelRT.offsetMin = Vector2.zero;
                    panelRT.offsetMax = Vector2.zero;
                }

                // Baixem una mica el text cap a la meitat inferior perquè la imatge tingui molt espai
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
                
                // Ocultar separador si existeix
                var divider = dynamicPanel.transform.Find("DividerLine");
                if (divider != null) divider.gameObject.SetActive(false);

                // Creem la imatge molt més gran i a la part superior
                oblImgObj = new GameObject("OblivionImage");
                oblImgObj.transform.SetParent(dynamicPanel.transform, false);
                var oblRT = oblImgObj.AddComponent<RectTransform>();
                oblRT.anchorMin = new Vector2(0.5f, 0.75f);
                oblRT.anchorMax = new Vector2(0.5f, 0.75f);
                oblRT.anchoredPosition = new Vector2(0f, 0f); // Just al centre del 75% superior
                oblRT.sizeDelta = new Vector2(700f, 700f); // Molt més gran!
                oblImg = oblImgObj.AddComponent<Image>();
                if (oblivionFinalSprite != null) oblImg.sprite = oblivionFinalSprite;
                oblImg.preserveAspect = true;
                oblImg.color = new Color(1, 1, 1, 0f); // Invisible inicialment
            }

            // FADE IN DE LA IMATGE mentre s'escriu el text
            float typingTime = (float)finalMonologue.Length / 10f; // Temps estimat
            float el = 0f;
            
            while (dialogueUI.IsOpen && dialogueUI.IsTyping)
            {
                el += Time.deltaTime;
                float progress = Mathf.Clamp01(el / typingTime);
                
                // Apliquem una corba exponencial (ex: quadrat o cub) perquè triga molt a veure's al principi
                float easedAlpha = Mathf.Pow(progress, 3f); 
                
                if (oblImg != null) oblImg.color = new Color(1, 1, 1, easedAlpha);
                yield return null;
            }

            // Assegurem alpha complet un cop acabi d'escriure
            if (oblImg != null) oblImg.color = new Color(1, 1, 1, 1f);

            // Esperar que l'usuari premi la E per tancar
            while (!Input.GetKeyDown(KeyCode.E))
            {
                yield return null;
            }

            // FADE OUT DEL TEXT I LA IMATGE
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

            // Un cop ha fet fade out, netegem i tanquem oficialment el diàleg sense restaurar el color
            dialogueUI.canAdvance = true;
            dialogueUI.Hide();

            // Restaurem la velocitat al tancar
            dialogueUI.charsPerSecond = originalSpeed;

            yield return new WaitForSeconds(1.5f);

            // TÍTOL FINAL: TO BE CONTINUED...
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

            // TÍTOL SECUNDARI: OR NOT
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

            // FADE OUT DE TOTS DOS TEXTOS I MÚSICA
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

            // Finalment, passem al Main Menu
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // Quan s'acabi el viatge i el monòleg de the oblivion, s'ocuparà ell mateix de canviar d'escena.
    }

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

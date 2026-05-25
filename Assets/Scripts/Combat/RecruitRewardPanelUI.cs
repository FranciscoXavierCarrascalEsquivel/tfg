using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gestiona la interfície gràfica del panell de recompensa que apareix un cop s'ha reclutat un enemic amb èxit.
/// Aquest panell es construeix de manera completament procedural (UI per codi) per assegurar que sigui robust,
/// i mostra en gran l'objecte o bonificació obtinguda, un text descriptiu sense accents (per evitar fallades de la font pixel),
/// i un botó animat '[E]' que espera la confirmació del jugador per tancar el panell i continuar la partida.
/// </summary>
public class RecruitRewardPanelUI : MonoBehaviour
{
    // ─── PATRÓ FACTORY PER A LA CREACIÓ DINÀMICA ─────────────────────
    /// <summary>
    /// Mètode estàtic per instanciar el panell directament a sobre del Canvas actiu sense necessitat de prefabs preconfigurats.
    /// D'aquesta manera s'eviten enllaços trencats a l'editor de Unity.
    /// </summary>
    /// <param name="canvasParent">El component Transform del pare del Canvas on es renderitzarà el panell.</param>
    /// <param name="rewardSprite">Sprite gràfic de la recompensa obtinguda (icona).</param>
    /// <param name="rewardText">Descripció textual de l'efecte o objecte obtingut.</param>
    /// <param name="enemyName">Nom de l'enemic derrotat/reclutat.</param>
    /// <param name="rewardSound">Efecte de so que sonarà en obrir-se el panell.</param>
    /// <param name="onDone">Callback a executar quan l'usuari tanqui el panell.</param>
    public static RecruitRewardPanelUI Create(
        Transform canvasParent,
        Sprite rewardSprite,
        string rewardText,
        string enemyName,
        AudioClip rewardSound,
        Action onDone)
    {
        // Creem un nou objecte buit de la UI que representarà tot el panell
        var go = new GameObject("RecruitRewardPanel");
        go.transform.SetParent(canvasParent, false);
        go.transform.SetAsLastSibling(); // Ens assegurem que es dibuixi per sobre de qualsevol altre component gràfic

        // Afegim aquest component per començar a processar la inicialització i configuració
        var panel = go.AddComponent<RecruitRewardPanelUI>();
        panel.rewardSprite = rewardSprite;
        panel.rewardText   = rewardText;
        panel.enemyName    = enemyName;
        panel.rewardSound  = rewardSound;
        panel.onDone       = onDone;
        return panel;
    }

    // ── VARIABLES I CONTEXT DE DADES ─────────────────────────────────
    private Sprite rewardSprite;          // Emmagatzema el recurs de l'sprite
    private string rewardText;            // Emmagatzema la cadena de descripció
    private string enemyName;             // Emmagatzema el nom de l'enemic
    private AudioClip rewardSound;        // Emmagatzema el so a reproduir
    private Action onDone;                // Callback de tancament

    private bool waitingForInput = false; // Bandera de control per saber si estem esperant la tecla E/Enter/Espai
    private RectTransform cardRect;       // Referència de transformació de la targeta informativa central
    private TMP_FontAsset pixelFont;      // Font pixelada carregada dinàmicament
    private RectTransform ePromptRT;      // RectTransform de la icona del botó [E] interactiu
    private RectTransform rewardIconRT;   // RectTransform de la icona central de recompensa

    // Inicialització en carregar-se el script
    private void Start()
    {
        // El motor de text pot fallar si rep accents en fonts pixelades fetes a mida.
        // Per tant, apliquem una funció de neteja preventiva per treure accents, dièresis i caràcters no ASCII.
        rewardText = RemoveAccents(rewardText);
        enemyName  = RemoveAccents(enemyName);

        // Construïm tota la jerarquia d'elements visuals del panell a través de programació
        Build();
        
        // Si hem especificat un so de recompensa vàlid, el reproduïm usant un AudioSource temporal en format 2D (spatialBlend = 0)
        if (rewardSound != null)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = rewardSound;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.Play();
        }

        // Executem l'animació d'entrada elàstica de la targeta des de la part inferior
        StartCoroutine(AnimateIn());
    }

    // Comprovacions a cada fotograma de joc
    private void Update()
    {
        // Animació del botó [E] (simula una pulsació mecànica 3D o de teclat retro a través de programació)
        if (waitingForInput && ePromptRT != null)
        {
            float cycle = Time.unscaledTime * 4.5f;
            bool isPressed = (cycle % 2f) > 1.4f; // Alterna entre premut i relaxat en funció del temps de rellotge real
            var top = ePromptRT.Find("Top") as RectTransform;
            if (top != null) 
            {
                // El desplacem 4 píxels cap avall en l'eix Y si està "premut", simulant profunditat
                top.anchoredPosition = isPressed ? Vector2.zero : new Vector2(0f, 4f);
            }
        }

        // Bloquegem l'escala i posició de la icona de recompensa per mantenir-la estàtica, segons requeriments de disseny de la interfície
        if (rewardIconRT != null)
        {
            rewardIconRT.anchoredPosition = new Vector2(0f, -20f);
            rewardIconRT.localScale = Vector3.one;
        }

        // Si encara no estem acceptant input (perquè s'està reproduint l'animació d'entrada), sortim immediatament
        if (!waitingForInput) return;

        // Comprovem si el jugador prem la tecla E, la tecla de retorn o l'espai
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            waitingForInput = false;
            // Executem la corrutina de sortida abans d'autodestruir l'objecte
            StartCoroutine(AnimateOut());
        }
    }

    // ────────────────────────────────────────────────────────────────
    // CONSTRUCCIÓ PROCEDURAL DE LA INTERFÍCIE (UI BY CODE)
    // ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Construeix de forma procedural tots els elements de la interfície gràfica d'usuari
    /// (fons semi-transparent, targeta informativa, marcs pixelats, títols, subtítols, descripcions
    /// i prompts d'interacció), assegurant un estil visual "retro" d'alta qualitat.
    /// </summary>
    private void Build()
    {
        // Intentem carregar les fonts preferides pel projecte de forma progressiva
        pixelFont = LoadFont("determination SDF");
        if (pixelFont == null) pixelFont = LoadFont("PixelOperator SDF");
        if (pixelFont == null) pixelFont = LoadFont("8bitoperator_jve SDF");
        // Si no en trobem cap d'específica, recorrem a la font per defecte de TMPro del sistema
        if (pixelFont == null) pixelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        // ── OVERLAY DE PANTALLA COMPLETA ──────────────────────────────
        // Fem que el contenidor principal ocupi el 100% de la pantalla
        var selfRT = gameObject.AddComponent<RectTransform>();
        selfRT.anchorMin = Vector2.zero;
        selfRT.anchorMax = Vector2.one;
        selfRT.offsetMin = Vector2.zero;
        selfRT.offsetMax = Vector2.zero;

        // Afegim un fons fosc intens semi-transparent per enfosquir l'acció del combat del fons
        var bgImg = gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.01f, 0.005f, 0.03f, 0.92f);

        // ── OMBRA DE LA TARGETA (SHADOW FRAME) ────────────────────────
        // Un petit truc visual que dona una qualitat premium excel·lent és col·locar un rectangle negre
        // lleugerament desplaçat (12 píxels) per sota de la targeta per simular una ombra física de UI.
        float cardH = 580f;
        var shadow = NewChild("Shadow", transform);
        var shadowRT = shadow.GetComponent<RectTransform>();
        shadowRT.anchorMin = new Vector2(0.15f, 0.5f);
        shadowRT.anchorMax = new Vector2(0.85f, 0.5f);
        shadowRT.offsetMin = new Vector2(12f, -cardH / 2f - 12f);
        shadowRT.offsetMax = new Vector2(12f, cardH / 2f - 12f);
        shadow.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

        // ── TARGETA INFORMATIVA CENTRAL ──────────────────────────────
        var card = NewChild("Card", transform);
        cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.15f, 0.5f);
        cardRect.anchorMax = new Vector2(0.85f, 0.5f);
        cardRect.offsetMin = new Vector2(0f, -cardH / 2f);
        cardRect.offsetMax = new Vector2(0f, cardH / 2f);
        cardRect.anchoredPosition = new Vector2(0f, 0f);

        // Assignem un color de fons fosc/lila característic del nostre estil
        card.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.18f, 1f);

        // ── MARCS PIXELATS (ESTIL RETRO) ──────────────────────────────
        // Afegim una vora groga brillant pixelada i detalls als quatre cantons per reforçar el disseny pixel-art
        Color goldColor = new Color(1f, 0.90f, 0.15f, 1f);
        AddPixelBorder(card, goldColor, 8f);
        AddCornerSquares(card, goldColor, 16f);

        // ── CAPÇALERA DE RECLUTAMENT ──────────────────────────────────
        float headerH = 110f;
        var header = NewChild("Header", card.transform);
        var hRT = header.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.offsetMin = new Vector2(0f, -headerH);
        hRT.offsetMax = Vector2.zero;
        header.AddComponent<Image>().color = new Color(0.28f, 0.12f, 0.55f, 1f);
        
        // Bordó daurat brillant per al separador de la capçalera
        AddPixelBorder(header, goldColor, 4f);

        // Text principal del títol ("COLLECTION COMPLETE!")
        var titleGo = NewChild("Title", header.transform);
        var titleRT = titleGo.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero;
        titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(0f, 5f);
        titleRT.offsetMax = Vector2.zero;
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        SetFont(titleTxt, 54f, new Color(1f, 0.95f, 0.4f), FontStyles.Bold, TextAlignmentOptions.Center);
        titleTxt.text = $"*  COLLECTION COMPLETE!  *";
        
        // Subtítol que indica de quin enemic hem obtingut la recompensa permanent
        var subGo = NewChild("SubTitle", card.transform);
        var subRT = subGo.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0f, 1f); subRT.anchorMax = new Vector2(1f, 1f);
        subRT.offsetMin = new Vector2(20f, -(headerH + 60f));
        subRT.offsetMax = new Vector2(-20f, -headerH);
        var subTxt = subGo.AddComponent<TextMeshProUGUI>();
        SetFont(subTxt, 34f, new Color(0.85f, 0.85f, 0.95f), FontStyles.Normal, TextAlignmentOptions.Center);
        subTxt.text = $"You've obtained {enemyName.ToUpper()}'s reward";

        // ── EFECTE DE GLOW DE FONS (RERE L'SPRITE) ─────────────────────
        // Genera una sensació mística mitjançant un cercle difuminat semi-transparent per darrere del premi
        var glowGo = NewChild("Glow", card.transform);
        var glowRT = glowGo.GetComponent<RectTransform>();
        glowRT.anchorMin = glowRT.anchorMax = new Vector2(0.5f, 0.5f);
        glowRT.sizeDelta = new Vector2(400f, 400f);
        glowRT.anchoredPosition = new Vector2(0f, -20f);
        var glowImg = glowGo.AddComponent<Image>();
        glowImg.color = new Color(1f, 0.9f, 0.45f, 0.35f);
        glowImg.sprite = GetSoftCircleSprite();
        glowImg.raycastTarget = false; // Ens assegurem que no bloquegi esdeveniments de punter

        // ── SPRITE DE LA RECOMPENSA (ICONOGRÀFIC) ─────────────────────
        if (rewardSprite != null)
        {
            var iconGo = NewChild("RewardIcon", card.transform);
            rewardIconRT = iconGo.GetComponent<RectTransform>();
            rewardIconRT.anchorMin = rewardIconRT.anchorMax = new Vector2(0.5f, 0.5f);
            rewardIconRT.sizeDelta = new Vector2(220f, 220f);
            rewardIconRT.anchoredPosition = new Vector2(0f, -20f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = rewardSprite;
            iconImg.preserveAspect = true; // Impedeix que la imatge es deformi
        }

        // ── TEXT DE DESCRIPCIÓ DE LA RECOMPENSA ───────────────────────
        var descGo = NewChild("Desc", card.transform);
        var descRT = descGo.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0.1f, 0f);
        descRT.anchorMax = new Vector2(0.9f, 0.35f);
        descRT.offsetMin = new Vector2(0f, 40f);
        descRT.offsetMax = new Vector2(0f, 0f);
        var descTxt = descGo.AddComponent<TextMeshProUGUI>();
        SetFont(descTxt, 42f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        // Habilitem redimensionament automàtic per evitar desbordaments en descripcions llargues
        descTxt.enableAutoSizing = true;
        descTxt.fontSizeMin = 24f;
        descTxt.fontSizeMax = 44f;
        descTxt.text = !string.IsNullOrEmpty(rewardText) ? rewardText : "You've gained a permanent upgrade!";

        // ── BOTÓ INTERACTIU [E] DINÀMIC ──────────────────────────────
        var eBase = NewChild("E_Prompt", card.transform);
        ePromptRT = eBase.GetComponent<RectTransform>();
        ePromptRT.anchorMin = ePromptRT.anchorMax = new Vector2(1f, 0f); 
        ePromptRT.pivot = new Vector2(1f, 0f);
        ePromptRT.sizeDelta = new Vector2(50f, 50f);
        ePromptRT.anchoredPosition = new Vector2(-25f, 25f);
        
        // Ombra posterior de color fosc per donar sensació de volum al botó
        var pBot = NewChild("Base", eBase.transform);
        var pBotRT = pBot.GetComponent<RectTransform>();
        pBotRT.anchorMin = Vector2.zero; pBotRT.anchorMax = Vector2.one; pBotRT.offsetMin = pBotRT.offsetMax = Vector2.zero;
        var pBotImg = pBot.AddComponent<Image>();
        pBotImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        pBotImg.sprite = GetRoundedSprite();
        pBotImg.type = Image.Type.Sliced;
        
        // Tapeta frontal premible del botó (de color clar)
        var pTop = NewChild("Top", eBase.transform);
        var ptRT = pTop.GetComponent<RectTransform>();
        ptRT.anchorMin = Vector2.zero; ptRT.anchorMax = Vector2.one; ptRT.offsetMin = ptRT.offsetMax = Vector2.zero;
        ptRT.anchoredPosition = new Vector2(0f, 4f);
        var ptImg = pTop.AddComponent<Image>();
        ptImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        ptImg.sprite = GetRoundedSprite();
        ptImg.type = Image.Type.Sliced;
        
        // Lletra central 'E' inserida dins de la tapeta del botó
        var etGo = NewChild("T", pTop.transform);
        TxtFill(etGo.GetComponent<RectTransform>(), "E", 32f, Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
        
        // S'amaga per defecte (escala 0) fins que finalitzi l'animació d'entrada elàstica
        ePromptRT.localScale = Vector3.zero;
    }

    /// <summary>
    /// Omple un component RectTransform generant-hi un TextMeshPro amb les propietats de disseny indicades.
    /// </summary>
    private TextMeshProUGUI TxtFill(RectTransform rt, string text, float size, Color col, FontStyles style, TextAlignmentOptions align)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        SetFont(t, size, col, style, align);
        t.text = text;
        return t;
    }

    private Sprite generatedSoftCircle;
    /// <summary>
    /// Genera dinàmicament per programació una textura circular suau (radial gradient fade-out) 
    /// per utilitzar-la com a aura luminosa ("glow") de fons, evitant haver de carregar recursos externs.
    /// </summary>
    private Sprite GetSoftCircleSprite()
    {
        if (generatedSoftCircle != null) return generatedSoftCircle;
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size / 2f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / center));
                alpha = Mathf.Pow(alpha, 2.5f); // Aplica corba exponencial per a un difuminat súper net
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        generatedSoftCircle = Sprite.Create(tex, new Rect(0,0,size,size), Vector2.one*0.5f);
        return generatedSoftCircle;
    }

    private Sprite generatedRoundedSprite;
    /// <summary>
    /// Genera per programació un sprite quadrat de 16x16 amb les cantonades tallades a nivell de píxel.
    /// Configurat com a Sliced, actua com a marc perfecte de 9-slicing per als botons retro de la interfície.
    /// </summary>
    private Sprite GetRoundedSprite()
    {
        if (generatedRoundedSprite != null) return generatedRoundedSprite;
        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color w = Color.white; Color c = new Color(1f, 1f, 1f, 0f);
        for (int y=0; y<size; y++) {
            for (int x=0; x<size; x++) {
                // Tallem els píxels dels extrems de la imatge per donar forma arrodonida pixel-art
                bool corner = (x==0 && y==0) || (x==size-1 && y==0) || (x==0 && y==size-1) || (x==size-1 && y==size-1)
                    || (x==1 && y==0) || (x==0 && y==1) || (x==size-2 && y==0) || (x==size-1 && y==1);
                tex.SetPixel(x, y, corner ? c : w);
            }
        }
        tex.Apply();
        // Utilitzem un pivot central i una vora simètrica de 4 píxels en 9-slice (Vector4)
        generatedRoundedSprite = Sprite.Create(tex, new Rect(0,0,size,size), Vector2.one*0.5f, 100f, 0, SpriteMeshType.FullRect, new Vector4(4,4,4,4));
        return generatedRoundedSprite;
    }

    // ────────────────────────────────────────────────────────────────
    // CORRUTINES D'ANIMACIÓ DE LA TARGETA (EASINGS DINÀMICS)
    // ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Anima l'entrada de la targeta des de sota de la pantalla aplicant un efecte de rebot
    /// elàstic (Elastic Ease Out) per aportar un gran dinamisme visual i qualitat professional.
    /// Un cop finalitzat el moviment, fa un pop a escala 1 del botó interactiu [E].
    /// </summary>
    private IEnumerator AnimateIn()
    {
        Vector2 target = cardRect.anchoredPosition;
        cardRect.localScale = new Vector3(0.5f, 0.5f, 1f);
        // Inicialitzem la targeta molt avall a fora de la finestra
        cardRect.anchoredPosition = target + new Vector2(0f, -800f);

        float dur = 0.7f;
        float elapsed = 0f;
        Vector2 from = cardRect.anchoredPosition;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / dur);
            
            // Càlcul de la corba elàstica d'anada i tornada ràpida (Rebot de moll)
            float e = 1f - Mathf.Cos(p * Mathf.PI * 0.5f);
            if (p < 1f) {
                float s = 1.70158f;
                float p2 = p - 1f;
                e = (p2 * p2 * ((s + 1f) * p2 + s) + 1f);
            }
            cardRect.anchoredPosition = Vector2.LerpUnclamped(from, target, e);
            cardRect.localScale = Vector3.LerpUnclamped(new Vector3(0.5f, 0.5f, 1f), Vector3.one, e);
            yield return null;
        }
        cardRect.anchoredPosition = target;
        cardRect.localScale = Vector3.one;

        // Esperem un instant curt abans de mostrar la indicació de tecla en un efecte d'escalat ràpid
        yield return new WaitForSeconds(0.4f);
        float eDur = 0.3f; float eElapsed = 0f;
        while(eElapsed < eDur) {
            eElapsed += Time.deltaTime;
            ePromptRT.localScale = Vector3.one * (eElapsed/eDur * 1.25f);
            if (ePromptRT.localScale.x > 1f) ePromptRT.localScale = Vector3.one;
            yield return null;
        }
        ePromptRT.localScale = Vector3.one;

        // Un cop presentat tot correctament, activem l'espera de tecles
        waitingForInput = true;
    }

    /// <summary>
    /// Corrutina de transició de sortida que augmenta lleugerament la mida de la targeta
    /// abans de destruir l'objecte per a un tancament fluid i lliure de talls bruscos.
    /// </summary>
    private IEnumerator AnimateOut()
    {
        float dur = 0.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            cardRect.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.1f, 1.1f, 1f), p);
            yield return null;
        }
        
        InvokeDone();
        Destroy(gameObject);
    }

    /// <summary>
    /// Executa de manera segura la funció callback final i buida la seva referència per evitar dobles crides.
    /// </summary>
    private void InvokeDone()
    {
        if (onDone != null)
        {
            onDone.Invoke();
            onDone = null;
        }
    }

    private void OnDestroy()
    {
        // Seguretat per al motor de joc: si per algun motiu extern es destrueix l'objecte (per ex. canvis de nivell),
        // ens assegurem de notificar el final al gestor per evitar bloquejar el fil de flux.
        InvokeDone();
    }

    // ─── ALTRES HELPERS ──────────────────────────────────────────────
    /// <summary>
    /// Mètode que neteja accents, dièresis i caràcters especials d'un text i els canvia per caràcters
    /// ASCII compatibles per evitar que les fonts pixel-art (que tenen un set de glifs limitat) mostrin errors o buits.
    /// </summary>
    private string RemoveAccents(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string result = text;
        string[] accents = { "à", "á", "è", "é", "ì", "í", "ò", "ó", "ù", "ú", "À", "Á", "È", "É", "Ì", "Í", "Ò", "Ó", "Ù", "Ú", "ç", "Ç", "·" };
        string[] normal  = { "a", "a", "e", "e", "i", "i", "o", "o", "u", "u", "A", "A", "E", "E", "I", "I", "O", "O", "U", "U", "c", "C", "." };
        for (int i = 0; i < accents.Length; i++)
            result = result.Replace(accents[i], normal[i]);
        return result;
    }

    /// <summary>
    /// Configura les propietats gràfiques bàsiques de la font per a un TextMeshPro.
    /// </summary>
    private void SetFont(TMP_Text txt, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        if (pixelFont != null) txt.font = pixelFont;
        txt.fontSize = size;
        txt.color = color;
        txt.fontStyle = style;
        txt.alignment = align;
    }

    /// <summary>
    /// Mètode abreujat per generar un fill (objecte secundari) amb RectTransform vinculat al pare donat.
    /// </summary>
    private GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    /// <summary>
    /// Afegeix una línia horitzontal divisòria de color sòlid per decorar seccions.
    /// </summary>
    private void AddHLine(GameObject parent, float y, Color col)
    {
        var go = NewChild("HLine", parent.transform);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.5f);
        rt.anchorMax = new Vector2(0.92f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 6f);
        rt.anchoredPosition = new Vector2(0f, y);
        go.AddComponent<Image>().color = col;
    }

    /// <summary>
    /// Dibuixa quatre franges d'imatge als costats per dibuixar una vora pixelada de gruix ajustable.
    /// </summary>
    private void AddPixelBorder(GameObject parent, Color col, float thick)
    {
        void MakeSide(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var g = NewChild(name, parent.transform);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = offsetMin; r.offsetMax = offsetMax;
            g.AddComponent<Image>().color = col;
        }
        MakeSide("B_Top",   new Vector2(0,1), new Vector2(1,1), new Vector2(0,-thick), new Vector2(0,0));
        MakeSide("B_Bot",   new Vector2(0,0), new Vector2(1,0), new Vector2(0,0),      new Vector2(0,thick));
        MakeSide("B_Left",  new Vector2(0,0), new Vector2(0,1), new Vector2(0,0),      new Vector2(thick,0));
        MakeSide("B_Right", new Vector2(1,0), new Vector2(1,1), new Vector2(-thick,0), new Vector2(0,0));
    }

    /// <summary>
    /// Afegeix detalls de quadrats decoratius als extrems/cantons de la targeta (decoració estil retro arcade).
    /// </summary>
    private void AddCornerSquares(GameObject parent, Color col, float size)
    {
        void MakeCorner(string name, Vector2 anchor, Vector2 pivot)
        {
            var g = NewChild(name, parent.transform);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = anchor; r.anchorMax = anchor;
            r.pivot = pivot;
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = Vector2.zero;
            g.AddComponent<Image>().color = col;
        }
        MakeCorner("C_TL", new Vector2(0,1), new Vector2(0,1));
        MakeCorner("C_TR", new Vector2(1,1), new Vector2(1,1));
        MakeCorner("C_BL", new Vector2(0,0), new Vector2(0,0));
        MakeCorner("C_BR", new Vector2(1,0), new Vector2(1,0));
    }

    /// <summary>
    /// Mètode segur per localitzar fonts d'editor o recursos a la carpeta Assets o Resources en temps d'execució.
    /// Protegit amb condicionals del compilador (#if UNITY_EDITOR) per evitar problemes en el moment del Build.
    /// </summary>
    private TMP_FontAsset LoadFont(string fontName)
    {
#if UNITY_EDITOR
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{fontName}.asset");
        if (f != null) return f;
        f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            $"Assets/TextMesh Pro/Resources/Fonts & Materials/{fontName}.asset");
        if (f != null) return f;
#endif
        var loaded = Resources.Load<TMP_FontAsset>($"Fonts & Materials/{fontName}");
        if (loaded != null) return loaded;
        return Resources.Load<TMP_FontAsset>($"Fonts/{fontName}") ?? Resources.Load<TMP_FontAsset>(fontName);
    }
}

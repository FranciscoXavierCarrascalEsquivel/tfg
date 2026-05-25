using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema d'efectes visuals de partícules de pluja d'estrelles vermelles (Red Falling Stars FX).
/// Genera dinàmicament una textura difuminada radial per codi (evitant dependències de fitxers externs),
/// utilitza tècniques d'optimització d'Unity com el material compartit per reduir crides de dibuix (Draw Calls),
/// i implementa un paral·laxi fals molt orgànic on la mida, color i velocitat de cada partícula estan lligades
/// al seu nivell de profunditat simulat (depth).
/// </summary>
public class RedFallingStarsFX : MonoBehaviour
{
    /// <summary>
    /// Estructura de dades que representa una única estrella en moviment.
    /// </summary>
    [System.Serializable]
    public class Star
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float speed;
    }

    [Header("Configuració d'Estrelles")]
    [Tooltip("Nombre total de partícules de pluja en pantalla simultàniament.")]
    public int starCount = 75; 
    
    [Tooltip("Direcció de caiguda de la pluja. Ex: (-1, -1) significa avall i a l'esquerra.")]
    public Vector2 fallDirection = new Vector2(-1f, -1f);
    
    [Tooltip("Velocitat mínima (per a les estrelles simulades com a més llunyanes)")]
    public float minSpeed = 0.5f;
    [Tooltip("Velocitat màxima (per a les estrelles simulades com a més properes)")]
    public float maxSpeed = 3.5f;
    
    [Tooltip("Mida d'escala mínima de la partícula")]
    public float minSize = 0.1f;
    [Tooltip("Mida d'escala màxima de la partícula")]
    public float maxSize = 0.35f;

    [Header("Nivells de Radiància (Colors)")]
    [Tooltip("Color de l'estrella més distant/profunda (més fosc i transparent)")]
    public Color darkestRed = new Color(0.3f, 0f, 0f, 0.4f);
    [Tooltip("Color de l'estrella en primer pla (més intens i brillant)")]
    public Color brightestRed = new Color(1f, 0.2f, 0.2f, 0.9f);

    [Header("Àrea del Volum (Dimensions)")]
    [Tooltip("Amplada del volum en unitats del món on s'espargiran les estrelles.")]
    public float areaWidth = 40f;
    [Tooltip("Alçada del volum en unitats del món on s'espargiran les estrelles.")]
    public float areaHeight = 30f;

    [Header("Ordenació de Render (Sorting)")]
    [Tooltip("Nom de la capa d'ordenació per al dibuix.")]
    public string sortingLayerName = "Default";
    [Tooltip("Ordre de dibuix a la capa (s'ajusta a un valor positiu alt per a sobreposar-se al mapa)")]
    public int sortingOrder = -50;

    // Recursos interns compartits o generats
    private List<Star> stars = new List<Star>();
    private Texture2D dotTexture;
    private Sprite dotSprite;
    private Material sharedStarMaterial;

    private void Start()
    {
        // 1) Creem l'sprite circular difuminat per programació
        CreateDotSprite();
        
        // 2) Cache de material per evitar sobrecost de Draw Calls i allocs (Batching estàtic)
        sharedStarMaterial = new Material(Shader.Find("Sprites/Default"));
        
        // 3) Instanciem el grup inicial d'estrelles amb posicions inicials disperses
        for (int i = 0; i < starCount; i++)
        {
            SpawnStar(true);
        }
    }

    /// <summary>
    /// Dibuixa de forma procedimental a nivell de píxels un punt radial transparent (Soft Particle).
    /// </summary>
    private void CreateDotSprite()
    {
        int size = 16;
        dotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Calculem la distància de cada píxel respecte al centre del rectangle de 16x16
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                if (dist <= radius) 
                {
                    // Degradat exponencial de transparència per aconseguir una aparença arrodonida i suau
                    float alpha = 1f - (dist / radius); 
                    alpha = Mathf.Pow(alpha, 0.6f); 
                    dotTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    dotTexture.SetPixel(x, y, Color.clear);
                }
            }
        }
        dotTexture.Apply();
        
        // El Pixel Per Unit (PPU) es fixa exactament igual a la mida en píxels (16) perquè el sprite per defecte mesuri exactament 1x1 unitats d'Unity
        dotSprite = Sprite.Create(dotTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// Instancia un objecte de partícula, li assigna el component de dibuix, el material optimitzat compartit, i el registra a la llista de control.
    /// </summary>
    private void SpawnStar(bool randomPosition)
    {
        GameObject starObj = new GameObject("StarDot");
        starObj.transform.SetParent(transform);
        
        SpriteRenderer sr = starObj.AddComponent<SpriteRenderer>();
        sr.sprite = dotSprite;
        sr.sharedMaterial = sharedStarMaterial; // Ús de material compartit

        // S'ajusta manualment per forçar la seva visualització per davant del terreny 2D
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = 50; 
        
        Star star = new Star
        {
            transform = starObj.transform,
            renderer = sr
        };
        
        ResetStar(star, randomPosition);
        stars.Add(star);
    }

    /// <summary>
    /// Recicla o inicia una estrella configurant paràmetres visuals basats en profunditat (Parallax).
    /// </summary>
    private void ResetStar(Star star, bool initRandomPosition)
    {
        float halfW = areaWidth / 2f;
        float halfH = areaHeight / 2f;

        if (initRandomPosition)
        {
            // En arrencar, distribuïm les partícules homogèniament per tot el volum
            star.transform.localPosition = new Vector3(
                Random.Range(-halfW, halfW), 
                Random.Range(-halfH, halfH), 
                0f
            );
        }
        else
        {
            // Quan es recicla, calculem la vora oposada a la direcció de caiguda per fer reaparèixer la partícula
            float dirX = Mathf.Sign(fallDirection.x);
            float dirY = Mathf.Sign(fallDirection.y);
            
            float probHorizontal = areaWidth / (areaWidth + areaHeight);
            bool spawnHorizontalEdge = Random.value < probHorizontal;

            if (spawnHorizontalEdge)
            {
                // Apareix a la part superior (si cau cap avall) o inferior (si puja)
                float spawnY = (dirY < 0) ? halfH : -halfH;
                star.transform.localPosition = new Vector3(Random.Range(-halfW, halfW), spawnY, 0f);
            }
            else
            {
                // Apareix a l'extrem dret (si viatja a l'esquerra) o esquerre (si viatja a la dreta)
                float spawnX = (dirX < 0) ? halfW : -halfW;
                star.transform.localPosition = new Vector3(spawnX, Random.Range(-halfH, halfH), 0f);
            }
        }

        // --- SISTEMA FALS PARALLAX DE PROFUNDITAT ---
        // Interpolant una sola variable aleatòria controladora aconseguim que tot quadre harmònicament
        float depth = Random.value; // 0 = Punts distants (petits, lents i foscos), 1 = Primer pla (grans, ràpids i brillants)
        
        // Ajust d'escala
        float currentSize = Mathf.Lerp(minSize, maxSize, depth);
        star.transform.localScale = new Vector3(currentSize, currentSize, 1f);
        
        // Ajust de velocitat de caiguda
        star.speed = Mathf.Lerp(minSpeed, maxSpeed, depth);
        
        // Ajust de color de degradat vermell
        star.renderer.color = Color.Lerp(darkestRed, brightestRed, depth);
    }

    private void Update()
    {
        // Vector de moviment constant aplicant delta de temps
        Vector3 movement = fallDirection.normalized * Time.deltaTime;
        float halfW = areaWidth * 0.5f;
        float halfH = areaHeight * 0.5f;
        float dirX = fallDirection.x;
        float dirY = fallDirection.y;

        int count = stars.Count;
        for (int i = 0; i < count; i++)
        {
            Star star = stars[i];
            // Desplacem l'estrella multiplicant per la seva pròpia velocitat de parallax
            star.transform.localPosition += movement * star.speed;

            // Comprovacions de límits per comprovar si ha sortit de l'àrea visible/límit
            Vector3 pos = star.transform.localPosition;
            bool outOfX = (dirX < 0 && pos.x < -halfW) || (dirX > 0 && pos.x > halfW);
            bool outOfY = (dirY < 0 && pos.y < -halfH) || (dirY > 0 && pos.y > halfH);

            if (outOfX || outOfY)
            {
                // Reciclem la partícula i la reubiquem
                ResetStar(star, false);
            }
        }
    }

    /// <summary>
    /// Dibuixa de forma gràfica la caixa de volum a la finestra d'Escena de l'Editor d'Unity per a calibratge visual fàcil.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawWireCube(transform.position, new Vector3(areaWidth, areaHeight, 1f));
    }
}

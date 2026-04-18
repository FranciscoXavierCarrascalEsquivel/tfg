using System.Collections.Generic;
using UnityEngine;

public class RedFallingStarsFX : MonoBehaviour
{
    [System.Serializable]
    public class Star
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float speed;
    }

    [Header("Star Settings")]
    [Tooltip("Quants punts / estrelles vols tenir alhora")]
    public int starCount = 150;
    
    [Tooltip("Direcció en la que cauen. Ex: (-1, -1) significa avall-esquerra.")]
    public Vector2 fallDirection = new Vector2(-1f, -1f);
    
    [Tooltip("Velocitat mínima (punts més llunyans)")]
    public float minSpeed = 0.5f;
    [Tooltip("Velocitat màxima (punts més propers)")]
    public float maxSpeed = 3.5f;
    
    [Tooltip("Mida mínima del punt")]
    public float minSize = 0.1f;
    [Tooltip("Mida màxima del punt")]
    public float maxSize = 0.35f;

    [Header("Colors")]
    [Tooltip("Radiance i transparència de l'estrella més llunyana/més fosca")]
    public Color darkestRed = new Color(0.3f, 0f, 0f, 0.4f);
    [Tooltip("Radiance i transparència de l'estrella més propera/brillant")]
    public Color brightestRed = new Color(1f, 0.2f, 0.2f, 0.9f);

    [Header("Boundaries")]
    [Tooltip("Amplada on apareixen es estrelles. Assegurat que cobreix el teu escenari/càmera")]
    public float areaWidth = 40f;
    [Tooltip("Llargada on apareixen les estrelles")]
    public float areaHeight = 30f;

    [Header("Sorting (Capes)")]
    [Tooltip("La capa on es renderitzaran les estrelles (assegurat que el fons no les tapi!)")]
    public string sortingLayerName = "Default";
    [Tooltip("L'ordre dins la capa (nombre negatiu per anar enrere)")]
    public int sortingOrder = -50;

    private List<Star> stars = new List<Star>();
    private Texture2D dotTexture;
    private Sprite dotSprite;

    private void Start()
    {
        CreateDotSprite();
        
        for (int i = 0; i < starCount; i++)
        {
            SpawnStar(true);
        }
    }

    private void CreateDotSprite()
    {
        // Creem un punt difuminat (tipus estrella suau) a través de codi, així no necessites sprites externs!
        int size = 16;
        dotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                if (dist <= radius) 
                {
                    // Fem un difuminat que es fa transparent a mesura que s'allunya del centre
                    float alpha = 1f - (dist / radius); 
                    alpha = Mathf.Pow(alpha, 0.6f); // Corba suau de suavitzat
                    dotTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    dotTexture.SetPixel(x, y, Color.clear);
                }
            }
        }
        dotTexture.Apply();
        
        // Fem que el píxels per unit (PPU) sigui exactament igual al 'size', així per defecte el sprite mesura exactament 1x1 unitats de Unity.
        // D'aquesta manera, l'escala que posis a l'Inspector (0.1 a 0.35) serà literalment la mida a la pantalla.
        dotSprite = Sprite.Create(dotTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void SpawnStar(bool randomPosition)
    {
        GameObject starObj = new GameObject("StarDot");
        starObj.transform.SetParent(transform);
        
        SpriteRenderer sr = starObj.AddComponent<SpriteRenderer>();
        sr.sprite = dotSprite;
        
        // Assegurem que el material sigui el correcte a URP
        sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        // FORCEM que es dibuixi per sobre de TOT (ignorem l'inspector un moment)
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = 50; // Nombre alt positiu perquè passi per davant!
        
        Star star = new Star
        {
            transform = starObj.transform,
            renderer = sr
        };
        
        ResetStar(star, randomPosition);
        stars.Add(star);
    }

    private void ResetStar(Star star, bool initRandomPosition)
    {
        float halfW = areaWidth / 2f;
        float halfH = areaHeight / 2f;

        if (initRandomPosition)
        {
            // Posicionem aleatòriament a tota la caixa a l'inici
            star.transform.localPosition = new Vector3(
                Random.Range(-halfW, halfW), 
                Random.Range(-halfH, halfH), 
                0f
            );
        }
        else
        {
            // Quan surten del límit, reciclem i fem que apareguin "als limits contraris"
            float dirX = Mathf.Sign(fallDirection.x);
            float dirY = Mathf.Sign(fallDirection.y);
            
            float probHorizontal = areaWidth / (areaWidth + areaHeight);
            bool spawnHorizontalEdge = Random.value < probHorizontal;

            if (spawnHorizontalEdge)
            {
                // Apareix a dalt si cau avall, o abaix si puja
                float spawnY = (dirY < 0) ? halfH : -halfH;
                star.transform.localPosition = new Vector3(Random.Range(-halfW, halfW), spawnY, 0f);
            }
            else
            {
                // Apareix a la dreta si cau esquerra, o esquerra si cau dreta
                float spawnX = (dirX < 0) ? halfW : -halfW;
                star.transform.localPosition = new Vector3(spawnX, Random.Range(-halfH, halfH), 0f);
            }
        }

        // PARALLAX FALS MOLT SATISFACTORI:
        // Vinculant velocitat, color i mida aconseguim un efecte de profunditat de camp.
        float depth = Random.value; // 0 = Les més llunyanes, 1 = Les més properes
        
        // Mida
        float currentSize = Mathf.Lerp(minSize, maxSize, depth);
        star.transform.localScale = new Vector3(currentSize, currentSize, 1f);
        
        // Velocitat més lenta si està lluny, més ràpida si està prop
        star.speed = Mathf.Lerp(minSpeed, maxSpeed, depth);
        
        // Color: més fosc/transparent si està lluny, més vibrant si està aprop
        star.renderer.color = Color.Lerp(darkestRed, brightestRed, depth);
    }

    private void Update()
    {
        // Un normalize per garantir que la velocitat escala exactament com els paràmetres ho demanen
        Vector3 movement = fallDirection.normalized * Time.deltaTime;
        
        float halfW = areaWidth / 2f;
        float halfH = areaHeight / 2f;

        foreach (var star in stars)
        {
            star.transform.localPosition += movement * star.speed;

            // Reciclem si surt fora dels límits establerts
            Vector3 pos = star.transform.localPosition;
            bool outOfX = (fallDirection.x < 0 && pos.x < -halfW) || (fallDirection.x > 0 && pos.x > halfW);
            bool outOfY = (fallDirection.y < 0 && pos.y < -halfH) || (fallDirection.y > 0 && pos.y > halfH);

            if (outOfX || outOfY)
            {
                ResetStar(star, false); // Reseta al limit per simular un loop infinit
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Dibuixem una quadrat al Unity Editor perquè vegis clarament per on passen les estrelles
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawWireCube(transform.position, new Vector3(areaWidth, areaHeight, 1f));
    }
}

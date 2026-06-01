# Desenvolupament d'un prototip de videojoc 2D amb mecàniques de combat inspirades en conceptes informàtics

Aquest repositori conté el projecte Unity del **Projecte de Fi de Grau** de Francisco Xavier Carrascal Esquivel, desenvolupat dins del Grau en Enginyeria Informàtica de la Universitat de Girona.

El projecte consisteix en un prototip funcional d'un videojoc 2D de tipus RPG que combina exploració, interacció amb personatges, diàlegs tradicionals, inventari, botiga, combat i integració amb intel·ligència artificial generativa per generar diàlegs dinàmics en alguns NPCs.

## Informació del projecte

- **Autor:** Francisco Xavier Carrascal Esquivel
- **Tutor:** Dr. Gustavo Ariel Patow
- **Grau:** Enginyeria Informàtica
- **Departament:** Informàtica, Matemàtica Aplicada i Estadística
- **Curs acadèmic:** 2025-2026
- **Motor de videojocs:** Unity 6.3 LTS
- **Versió de Unity:** 6000.3.2f1
- **Llenguatge principal:** C#
- **Repositori de l'API d'IA:** <https://github.com/FranciscoXavierCarrascalEsquivel/api-reason-and-ruin>

## Descripció general

El videojoc planteja un món fictici format per idees oblidades, projectes inacabats i creacions abandonades. El jugador controla un protagonista que es desperta en aquest entorn sense recordar clarament qui és ni com hi ha arribat.

Durant la partida, el jugador pot explorar l'escenari, interactuar amb objectes, parlar amb NPCs, participar en combats i descobrir fragments de la narrativa. Un dels elements principals del prototip és la integració d'IA generativa en alguns personatges, que poden respondre de manera dinàmica als missatges del jugador mantenint un rol, una personalitat i un context narratiu definits.

La IA no substitueix tota la narrativa escrita manualment, sinó que complementa determinades interaccions perquè siguin més flexibles i menys repetitives.

## Objectius principals

Els objectius principals del projecte són:

- Desenvolupar un prototip jugable d'un videojoc 2D RPG amb Unity.
- Implementar moviment del jugador, col·lisions i exploració de l'escenari.
- Crear un sistema d'interacció amb NPCs i objectes.
- Implementar diàlegs tradicionals escrits manualment.
- Desenvolupar un sistema de combat senzill amb diferents accions.
- Integrar converses dinàmiques amb intel·ligència artificial generativa.
- Separar el videojoc del servei d'IA mitjançant una arquitectura client-servidor.
- Validar la viabilitat tècnica i narrativa de la integració d'IA dins d'un videojoc narratiu.

## Funcionalitats implementades

### Exploració i moviment

El jugador pot moure's per l'escenari principal del videojoc i explorar l'entorn. El sistema de col·lisions impedeix travessar parets, objectes sòlids o límits del mapa.

### Interacció amb NPCs i objectes

El joc permet interactuar amb personatges no jugables i objectes propers. Aquesta interacció pot activar diàlegs, obrir sistemes de joc o iniciar altres esdeveniments.

### Diàlegs tradicionals

Alguns personatges utilitzen diàlegs predefinits escrits manualment. Aquest sistema permet controlar millor la progressió narrativa i assegurar que determinats personatges transmetin informació concreta al jugador.

### Diàlegs amb IA generativa

Alguns NPCs poden activar un mode de conversa generativa. En aquest mode, el jugador escriu un missatge i el joc envia una petició a una API externa. Aquesta API construeix el prompt amb la informació del personatge, el context narratiu i les normes de comportament, consulta el model d'IA i retorna la resposta al videojoc.

Aquest sistema permet que el personatge respongui preguntes lliures mantenint el seu rol i la seva personalitat.

### Sistema de combat

El prototip inclou un sistema de combat per torns amb diferents opcions d'acció:

- **Fight:** atacar l'enemic.
- **Reason:** intentar una resolució pacífica o narrativa.
- **Item:** utilitzar objectes de l'inventari.
- **Flee:** intentar fugir del combat.

També s'han implementat atacs enemics amb projectils i patrons bàsics d'atac.

### Inventari i objectes

El jugador pot obtenir, guardar, comprar i utilitzar objectes. L'inventari dona suport a altres sistemes del joc, com l'exploració, la botiga i el combat.

### Botiga

El prototip inclou una interfície de botiga que permet comprar objectes i integrar-los dins del sistema d'inventari del jugador.

### Gestió d'errors

El joc contempla errors bàsics relacionats amb la comunicació amb el servei d'IA, com ara problemes de connexió o absència de resposta del servidor.

## Arquitectura del sistema

El projecte segueix una arquitectura separada entre el videojoc i la intel·ligència artificial.

```text
Jugador
   |
   v
Videojoc Unity
   |
   | Petició HTTP
   v
API FastAPI
   |
   | Consulta al model
   v
Ollama / llama3.1:8b
   |
   | Resposta generada
   v
API FastAPI
   |
   | Resposta JSON
   v
Videojoc Unity
```

Unity s'encarrega de la part visual, interactiva i jugable. L'API externa s'encarrega de rebre el missatge del jugador, construir el prompt, consultar el model d'IA i retornar la resposta generada.

Aquesta separació permet mantenir el joc més lleuger i facilita futures ampliacions, com substituir el model d'IA, executar-lo en un altre ordinador o desplegar-lo en un servidor remot.

## Tecnologies utilitzades

- **Unity 6.3 LTS 6000.3.2f1**
- **C#**
- **Python**
- **FastAPI**
- **Ollama**
- **llama3.1:8b**
- **Git i GitHub**
- **LaTeX i Overleaf** per a la documentació acadèmica

## Requisits

Per obrir i executar el projecte des de Unity:

- Unity 6.3 LTS 6000.3.2f1 o versió compatible.
- Sistema operatiu compatible amb Unity.
- Git, si es vol clonar el repositori.
- L'API d'IA en execució, només si es vol provar la funcionalitat de diàlegs generatius.

Per jugar sense IA, el prototip pot funcionar amb les funcionalitats locals del videojoc, però les converses generatives requereixen tenir l'API activa.

## Instal·lació i execució des de Unity

1. Clonar aquest repositori:

```bash
git clone https://github.com/FranciscoXavierCarrascalEsquivel/tfg.git
```

2. Obrir Unity Hub.

3. Afegir el projecte clonat des de l'opció **Add project from disk**.

4. Obrir el projecte amb **Unity 6.3 LTS 6000.3.2f1**.

5. Comprovar que les escenes principals estiguin incloses a **File > Build Settings**.

6. Obrir l'escena inicial o principal del projecte.

7. Executar el joc amb el botó **Play** de l'editor.

8. Si es vol provar la IA generativa, posar en marxa abans el repositori de l'API:

```text
https://github.com/FranciscoXavierCarrascalEsquivel/api-reason-and-ruin
```

## Generació d'un executable

El projecte també es pot compilar per generar una versió executable.

Passos generals:

1. Obrir el projecte amb Unity.
2. Anar a **File > Build Settings**.
3. Afegir les escenes necessàries a l'apartat **Scenes In Build**.
4. Seleccionar la plataforma de destí, per exemple Windows.
5. Prémer **Build** o **Build And Run**.
6. Escollir una carpeta de sortida.
7. Executar el fitxer generat.

Si es vol utilitzar la funcionalitat d'IA generativa des de l'executable, l'API externa també haurà d'estar en funcionament i la URL configurada correctament dins del projecte.

## Configuració de la connexió amb l'API

El videojoc es comunica amb una API externa desenvolupada amb FastAPI. Aquesta API és l'encarregada de connectar amb Ollama i el model `llama3.1:8b`.

En el projecte Unity, la URL de l'API i el token es configuren des de l'Inspector en els camps corresponents del component encarregat de la comunicació amb la IA. Cal revisar especialment:

- **Ai Api Url**
- **Ai Api Token**

La URL dependrà d'on s'estigui executant l'API. En local, habitualment serà una adreça semblant a:

```text
http://localhost:8000/chat
```

Si l'API s'executa en un altre ordinador o servidor, cal utilitzar l'adreça corresponent.

## Relació amb el repositori de l'API

Aquest repositori només conté el videojoc Unity. La part d'intel·ligència artificial es troba en un repositori separat:

```text
https://github.com/FranciscoXavierCarrascalEsquivel/api-reason-and-ruin
```

Perquè els diàlegs generatius funcionin correctament, cal tenir l'API executant-se i configurada amb Ollama.

## Vídeos del projecte

- **Vídeo de demostració del producte:**  
  <https://drive.google.com/drive/folders/1yyajLQiiMFiQTjuPNXET50n2Sm0utfY4?usp=sharing>

- **Vídeo dels aspectes rellevants del codi:**  
  <https://drive.google.com/drive/folders/1TxbgxhaLbUUadsJHxdCe6-6zC2HQ3b50?usp=sharing>

## Estat del projecte

Aquest projecte és un prototip acadèmic desenvolupat com a Projecte de Fi de Grau. No es tracta d'un videojoc comercial complet, sinó d'una versió funcional orientada a demostrar les mecàniques principals i la viabilitat d'integrar IA generativa dins d'un videojoc 2D RPG.

## Limitacions actuals

Algunes limitacions del prototip són:

- No disposa d'un sistema complet de guardat i càrrega de partida.
- La memòria narrativa dels NPCs és limitada.
- La robustesa del servei d'IA es podria millorar en futures versions.
- El sistema de combat podria ampliar-se amb més patrons, enemics i opcions.
- El poliment visual, sonor i narratiu encara podria millorar-se.
- Les proves amb usuaris són limitades pel context acadèmic del projecte.

## Treball futur

Possibles línies de millora:

- Afegir guardat i càrrega de partida.
- Millorar la integració amb IA generativa.
- Incorporar memòria narrativa persistent per als NPCs.
- Ampliar el sistema de combat.
- Millorar l'inventari i l'economia del joc.
- Afegir més escenaris, personatges i contingut narratiu.
- Fer proves amb usuaris.
- Desplegar l'API d'IA en un servidor més robust.

## Autor

**Francisco Xavier Carrascal Esquivel**  
Grau en Enginyeria Informàtica  
Universitat de Girona

## Llicència

Aquest repositori forma part d'un projecte acadèmic. La llicència d'ús no està especificada.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class Move : MonoBehaviour
{
    // Objeto principal que se moverá entre marcadores
    public GameObject model;

    // Marcador principal (donde inicia el modelo y al cual regresa tras reiniciar)
    public ObserverBehaviour mainMarker;

    // Marcadores secundarios donde se colocarán objetos aleatorios (prefabs)
    public ObserverBehaviour[] ImageTargets;

    // Prefabs que se colocarán de forma aleatoria sobre los marcadores secundarios
    public GameObject[] objectPrefabs;

    // Panel que se muestra al ganar
    public GameObject winPanel;

    // Panel que se muestra al perder
    public GameObject losePanel;

    // Panel de instrucciones que aparece al iniciar la aplicación
    public GameObject instructionsPanel;

    // Velocidad del movimiento del objeto principal entre marcadores
    public float speed = 1.0f;

    // Bandera que indica si el objeto se está moviendo (evita múltiples movimientos simultáneos)
    private bool isMoving = false;

    // Guarda la posición local original del modelo (como hijo del marcador principal)
    private Vector3 modelStartLocalPosition;

    // Guarda la rotación local original del modelo (como hijo del marcador principal)
    private Quaternion modelStartLocalRotation;

    // Diccionario que relaciona cada marcador con su objeto instanciado
    private Dictionary<ObserverBehaviour, GameObject> targetToObject = new();

    // Referencia al marcador que tiene el objeto "prohibido" (por ejemplo, el enemigo final)
    private ObserverBehaviour satTarget;

    // Conjunto que guarda los marcadores que ya han sido visitados por el jugador
    private HashSet<ObserverBehaviour> visitedTargets = new();

    void Start()
    {
        // Guarda la posición y rotación original del modelo como hijo del marcador principal
        modelStartLocalPosition = model.transform.localPosition;
        modelStartLocalRotation = model.transform.localRotation;

        // Oculta los paneles de resultado al comenzar
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Si hay un panel de instrucciones, lo dejamos activo y esperamos a que el jugador lo cierre
        // El juego no comienza hasta que se presione el botón de iniciar
    }

    // Este método debe vincularse al botón de "Iniciar" en el panel de instrucciones
    public void HideInstructions()
    {
        if (instructionsPanel != null)
        {
            instructionsPanel.SetActive(false);
        }

        // Una vez ocultadas las instrucciones, se realiza la asignación aleatoria de objetos
        AssignRandomObjectsToTargets();
    }

    // Método que se ejecuta al presionar el botón de un marcador para moverse a ese marcador
    public void moveToSpecificMarker(int targetIndex)
    {
        if (!isMoving)
        {
            StartCoroutine(MoveToTarget(targetIndex));
        }
    }

    // Corrutina que mueve el modelo al marcador indicado por el índice
    private IEnumerator MoveToTarget(int targetIndex)
    {
        isMoving = true;

        ObserverBehaviour target = ImageTargets[targetIndex];

        // Verifica que el marcador esté siendo rastreado por la cámara
        if (target == null ||
            (target.TargetStatus.Status != Status.TRACKED &&
             target.TargetStatus.Status != Status.EXTENDED_TRACKED))
        {
            Debug.LogWarning("El marcador seleccionado no está siendo rastreado.");
            isMoving = false;
            yield break;
        }

        // Movimiento interpolado desde la posición actual hasta el marcador destino
        Vector3 startPosition = model.transform.position;
        Vector3 endPosition = target.transform.position;
        float journey = 0;

        while (journey < 1f)
        {
            journey += Time.deltaTime * speed;
            model.transform.position = Vector3.Lerp(startPosition, endPosition, journey);
            yield return null;
        }

        isMoving = false;

        // Activa el objeto que corresponde al marcador (si existe)
        if (targetToObject.ContainsKey(target))
        {
            targetToObject[target].SetActive(true);
        }

        // Marca el marcador como visitado
        visitedTargets.Add(target);

        // Verifica si el marcador es el "enemigo final"
        if (target == satTarget)
        {
            // Si ya se visitaron todos los demás marcadores, se gana
            if (visitedTargets.Count == ImageTargets.Length)
            {
                Debug.Log("GANASTE: completaste todos los marcadores antes del destino final.");
                if (winPanel != null) winPanel.SetActive(true);
            }
            else
            {
                Debug.Log("PERDISTE: fuiste al destino final demasiado pronto.");
                if (losePanel != null) losePanel.SetActive(true);
            }
        }

        // Desactiva el botón de ese marcador para que no pueda volver a usarse
        Transform canvas = target.transform.Find("Canvas");
        if (canvas != null)
        {
            Transform button = canvas.transform.Find("Button");
            if (button != null)
            {
                button.gameObject.SetActive(false);
            }
        }
    }

    // Reinicia el juego completamente
    public void RestartGame()
    {
        // Oculta los paneles de victoria o derrota
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Recoloca el modelo en el marcador principal, en su posición y rotación original
        model.transform.SetParent(null);
        model.transform.SetParent(mainMarker.transform);
        model.transform.localPosition = modelStartLocalPosition;
        model.transform.localRotation = modelStartLocalRotation;

        // Limpia el registro de marcadores visitados
        visitedTargets.Clear();

        // Destruye los objetos instanciados anteriormente
        foreach (var obj in targetToObject.Values)
        {
            Destroy(obj);
        }
        targetToObject.Clear();

        // Reactiva los botones en todos los marcadores
        foreach (ObserverBehaviour target in ImageTargets)
        {
            Transform canvas = target.transform.Find("Canvas");
            if (canvas != null)
            {
                Transform button = canvas.transform.Find("Button");
                if (button != null)
                {
                    button.gameObject.SetActive(true);
                }
            }
        }

        // Asigna nuevamente los objetos aleatorios
        AssignRandomObjectsToTargets();
    }

    // Asigna aleatoriamente prefabs a los marcadores secundarios
    public void AssignRandomObjectsToTargets()
    {
        List<ObserverBehaviour> availableTargets = new();

        // Se agregan solo los marcadores que no son el marcador principal
        foreach (ObserverBehaviour target in ImageTargets)
        {
            if (target != mainMarker)
            {
                availableTargets.Add(target);
            }
        }

        // Mezcla aleatoria de marcadores y prefabs
        ShuffleList(availableTargets);
        List<GameObject> shuffledPrefabs = new(objectPrefabs);
        ShuffleList(shuffledPrefabs);

        int pairCount = Mathf.Min(availableTargets.Count, shuffledPrefabs.Count);

        for (int i = 0; i < pairCount; i++)
        {
            GameObject instance = Instantiate(shuffledPrefabs[i]);
            ObserverBehaviour target = availableTargets[i];

            // Se asigna como hijo del marcador y se ajusta posición/rotación/escala
            instance.transform.SetParent(target.transform);
            instance.transform.localPosition = new Vector3(-0.6f, 0f, 0f);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(false); // Solo se muestra si el marcador es visitado

            targetToObject[target] = instance;

            // Se registra cuál marcador contiene el objeto prohibido (por nombre)
            if (shuffledPrefabs[i].name.ToLower().Contains("sat"))
            {
                satTarget = target;
            }
        }
    }

    // Mezcla aleatoria de listas genéricas (algoritmo de Fisher-Yates)
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rand];
            list[rand] = temp;
        }
    }
}

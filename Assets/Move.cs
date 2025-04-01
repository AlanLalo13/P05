using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class Move : MonoBehaviour
{
    // El objeto principal que se moverá entre los marcadores
    public GameObject model;

    // El marcador principal donde debe empezar y regresar el objeto al reiniciar
    public ObserverBehaviour mainMarker;

    // Lista de marcadores donde se colocarán los prefabs (EXCLUYENDO el marcador principal)
    public ObserverBehaviour[] ImageTargets;

    // Lista de objetos (prefabs) que se van a instanciar de forma aleatoria
    public GameObject[] objectPrefabs;

    // Panel de victoria
    public GameObject winPanel;

    // Panel de derrota
    public GameObject losePanel;

    // Velocidad de movimiento entre marcadores
    public float speed = 1.0f;

    // Controla si el objeto se está moviendo
    private bool isMoving = false;

    // Guarda la posición y rotación original del modelo principal (para reinicio)
    private Vector3 modelStartLocalPosition;
    private Quaternion modelStartLocalRotation;

    // Relaciona cada marcador con el objeto que se instanció en él
    private Dictionary<ObserverBehaviour, GameObject> targetToObject = new Dictionary<ObserverBehaviour, GameObject>();

    // Marcador al que se le asignó el objeto \"sat\" (para detectar derrota)
    private ObserverBehaviour satTarget;

    // Marcadores que ya fueron visitados por el objeto principal
    private HashSet<ObserverBehaviour> visitedTargets = new HashSet<ObserverBehaviour>();

    void Start()
    {
        // Guardar la posición y rotación inicial del modelo como hijo del marcador principal
        modelStartLocalPosition = model.transform.localPosition;
        modelStartLocalRotation = model.transform.localRotation;

        // Ocultar los paneles de victoria o derrota
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Asignar aleatoriamente los prefabs a los marcadores (excepto el principal)
        AssignRandomObjectsToTargets();
    }

    // Método que se llama desde los botones de los marcadores para mover el modelo
    public void moveToSpecificMarker(int targetIndex)
    {
        if (!isMoving)
        {
            StartCoroutine(MoveToTarget(targetIndex));
        }
    }

    // Corrutina que mueve el modelo al marcador seleccionado
    private IEnumerator MoveToTarget(int targetIndex)
    {
        isMoving = true;

        // Obtener el marcador destino
        ObserverBehaviour target = ImageTargets[targetIndex];

        // Validar si está siendo detectado por Vuforia
        if (target == null ||
            (target.TargetStatus.Status != Status.TRACKED &&
             target.TargetStatus.Status != Status.EXTENDED_TRACKED))
        {
            Debug.LogWarning("El marcador seleccionado no está siendo rastreado.");
            isMoving = false;
            yield break;
        }

        // Movimiento interpolado del objeto principal hacia el marcador destino
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

        // Activar el objeto instanciado en ese marcador (si lo hay)
        if (targetToObject.ContainsKey(target))
        {
            targetToObject[target].SetActive(true);
        }

        // Marcar el marcador como visitado
        visitedTargets.Add(target);

        // Verificar condición de victoria o derrota si es el SAT
        if (target == satTarget)
        {
            if (visitedTargets.Count == ImageTargets.Length)
            {
                Debug.Log("GANASTE: visitaste todos los marcadores antes del SAT.");
                if (winPanel != null) winPanel.SetActive(true);
            }
            else
            {
                Debug.Log("PERDISTE: fuiste al SAT antes de visitar los demás.");
                if (losePanel != null) losePanel.SetActive(true);
            }
        }

        // Ocultar el botón del marcador actual para evitar que vuelva a usarse
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

    // Método que reinicia todo el juego (modelo, objetos y botones)
    public void RestartGame()
    {
        // Ocultar paneles de resultado
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Restaurar el objeto principal al marcador principal
        model.transform.SetParent(null); // Desvincular temporalmente
        model.transform.SetParent(mainMarker.transform); // Volver a ser hijo del marcador principal
        model.transform.localPosition = modelStartLocalPosition;
        model.transform.localRotation = modelStartLocalRotation;

        // Limpiar lista de visitados
        visitedTargets.Clear();

        // Eliminar objetos instanciados previos
        foreach (var obj in targetToObject.Values)
        {
            Destroy(obj);
        }
        targetToObject.Clear();

        // Reactivar todos los botones de los marcadores
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

        // Reinstanciar objetos de forma aleatoria
        AssignRandomObjectsToTargets();
    }

    // Asigna los objetos aleatoriamente a los marcadores (excepto el principal)
    public void AssignRandomObjectsToTargets()
    {
        List<ObserverBehaviour> availableTargets = new List<ObserverBehaviour>();

        // Solo agregamos los marcadores que NO son el marcador principal
        foreach (ObserverBehaviour target in ImageTargets)
        {
            if (target != mainMarker)
            {
                availableTargets.Add(target);
            }
        }

        // Mezclar listas
        ShuffleList(availableTargets);
        List<GameObject> shuffledPrefabs = new List<GameObject>(objectPrefabs);
        ShuffleList(shuffledPrefabs);

        int pairCount = Mathf.Min(availableTargets.Count, shuffledPrefabs.Count);

        for (int i = 0; i < pairCount; i++)
        {
            // Instanciar prefab como hijo del marcador
            GameObject instance = Instantiate(shuffledPrefabs[i]);
            ObserverBehaviour target = availableTargets[i];

            instance.transform.SetParent(target.transform);
            instance.transform.localPosition = new Vector3(-0.6f, 0f, 0f); // Desplazamiento visual
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(false); // Oculto hasta que el jugador llegue

            targetToObject[target] = instance;

            // Detectar si es el objeto \"sat\" para registrar su marcador
            if (shuffledPrefabs[i].name.ToLower().Contains("sat"))
            {
                satTarget = target;
            }
        }
    }

    // Utilidad para mezclar listas
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

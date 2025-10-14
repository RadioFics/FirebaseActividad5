using System.Collections;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Obstáculos")]
    [SerializeField] private GameObject[] obstacles;

    [Header("Configuración de altura para aves")]
    [SerializeField] private string birdTag = "Bird";
    [SerializeField] private float minBirdHeight = 1.2f;
    [SerializeField] private float maxBirdHeight = 2.2f;

    [Header("Tiempos de aparición")]
    [SerializeField] private float minTime = 0.6f;
    [SerializeField] private float maxTime = 1.8f;

    void Start()
    {
        StartCoroutine(SpawnObstacle());
    }

    private IEnumerator SpawnObstacle()
    {
        while (true)
        {
            int randomIndex = Random.Range(0, obstacles.Length);
            float randomTime = Random.Range(minTime, maxTime);

            GameObject prefab = obstacles[randomIndex];
            Vector3 spawnPos = transform.position;

            if (prefab.CompareTag(birdTag))
            {
                float randomHeight = Random.Range(minBirdHeight, maxBirdHeight);
                spawnPos.y += randomHeight;
            }

            Instantiate(prefab, spawnPos, Quaternion.identity);
            yield return new WaitForSeconds(randomTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Destroy(collision.gameObject);
    }
}

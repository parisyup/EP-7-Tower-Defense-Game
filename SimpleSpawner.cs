using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleSpawner : MonoBehaviour
{
    public List<waveConfig> waves = new();
    public int alive;

    bool running;
    int currentWaveIndex;

    Coroutine loop;

    private void Start()
    {
        startWaves();
    }

    private void OnDisable()
    {
        running = false;
        StopAllCoroutines();
    }

    public void startWaves()
    {
        if (waves == null || running) return;

        StopAllCoroutines();
        alive = 0;
        currentWaveIndex = -1;
        running = true;
        loop = StartCoroutine(RunLoop());
    }

    IEnumerator RunLoop()
    {
        for (int w = 0; w < waves.Count; w++) 
        {
            currentWaveIndex = w;
            waveConfig wave = waves[w];

            yield return StartCoroutine(SpawnRoutine(wave));

            while (alive > 0) yield return null;
        }


        running = false;
        loop = null;
    }

    IEnumerator SpawnRoutine(waveConfig wave)
    {
        int totalEnemiesInWave = 0;
        List<int> kindsOfEnemies = new List<int>();
        for (int x = 0; x < wave.enemyPrefab.Count && running; x++) 
        {
            totalEnemiesInWave += wave.totalToSpawn[x];
            kindsOfEnemies.Add(wave.totalToSpawn[x]);
        }

        for (int i = 0; i < totalEnemiesInWave && running; i++)
        {
            Vector3 pos = transform.position;

            if (wave.spawnRadius > 0)
            {
                var off = Random.insideUnitSphere * wave.spawnRadius;
                pos += new Vector3(off.x, 0f, off.y);
            }

            int nextUp = UnityEngine.Random.Range(0, kindsOfEnemies.Count);
            var go = Instantiate(wave.enemyPrefab[nextUp], pos, Quaternion.identity);

            kindsOfEnemies[nextUp]--;

            if (kindsOfEnemies[nextUp] <= 0) kindsOfEnemies.RemoveAt(nextUp);

            var life = go.GetComponent<EnemyController>();
            life.Died += handleDeath;
            life.targets = wave.targets;

            alive++;

            if (wave.intervalSeconds > 0f) yield return new WaitForSeconds(wave.intervalSeconds);
            else yield return null;
        }
    }

    void handleDeath (EnemyController relay)
    {
        relay.Died -= handleDeath;
        alive = Mathf.Max(0, alive - 1);
    }
}

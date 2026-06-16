using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlowingLeavesA : MonoBehaviour
{

    private bool leafRespawning = false;
    public static int leafCount;
    public static float leafSpeedShared;

    public Vector3 leafArea;
    public GameObject windDirection;
    public int maximumLeaves = 200;
    public float leafSpeed = 1;
    public float leafSpawnDelayRate = 1.0f;
    public int leafLifetime;
    public GameObject leafSingleAnim;
    public GameObject leafTypeA;
    public GameObject leafTypeB;
    public GameObject leafTypeC;
    public GameObject leafTypeD;
    public float minScale = 0.5f;
    public float maxScale = 20.0f;

    void Start()
    {

        leafSpeedShared = leafSpeed;
        leafSingleAnim.transform.rotation = windDirection.transform.rotation;
        leafSpeed = Random.Range(leafSpeed - 0.5f, leafSpeed + 0.5f);

    }

    void Update()

    {

        if (leafRespawning == false)
        {

            StartCoroutine("RespawnLeaves");

        }

    }


    IEnumerator RespawnLeaves()
    {

        leafRespawning = true;

        if (leafCount < maximumLeaves)
        {

            leafSingleAnim.transform.localScale = new Vector3(0, 0, 0);

            leafCount += 1;


            // Randomly choose 1 of the 4 leaves

            leafTypeA.SetActive(false);
            leafTypeB.SetActive(false);
            leafTypeC.SetActive(false);
            leafTypeD.SetActive(false);

            int chooseLeafType;

            chooseLeafType = Random.Range(1, 5);

            if (chooseLeafType == 1) { leafTypeA.SetActive(true); }
            if (chooseLeafType == 2) { leafTypeB.SetActive(true); }
            if (chooseLeafType == 3) { leafTypeC.SetActive(true); }
            if (chooseLeafType == 4) { leafTypeD.SetActive(true); }

            GameObject copy = Instantiate(leafSingleAnim);

            copy.transform.rotation = windDirection.transform.rotation;
            copy.transform.localScale = new Vector3(0, 0, 0);
            copy.transform.position = new Vector3(0, 0, 0);

            // Spawn leaf in a random location
            Vector3 position = new Vector3(Random.Range(-leafArea.x, leafArea.x), 0, Random.Range(-leafArea.z, leafArea.z));
            copy.transform.Translate(position);

            Destroy(copy, leafLifetime);

            // Give the leaf a random size

            float scale = Random.Range(minScale, maxScale);
            Vector3 fromScale = new Vector3(0, 0, 0);
            Vector3 toScale = new Vector3(scale, scale, scale);

            yield return new WaitForSeconds(leafSpawnDelayRate);

            leafRespawning = false;

            copy.transform.GetChild(0).gameObject.SetActive(true);

            // Time is the time in seconds for the new leaf to appear
            int time = 1;
            float currentTime = 0.0f;

            while (currentTime <= time)
            {
                copy.transform.localScale = Vector3.Lerp(fromScale, toScale, currentTime / time);
                currentTime += Time.deltaTime;
                yield return 0;
            }

        }

        if (leafCount < 0)
        { leafCount = 0; }

        yield return new WaitForSeconds(leafSpawnDelayRate);

        leafRespawning = false;

    }

}
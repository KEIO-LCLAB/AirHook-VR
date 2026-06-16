using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlowingLeavesB : MonoBehaviour
{

    private float leafPosition;
    private Animation anim;

    public GameObject leaf;
    public GameObject leafParent;
    public GameObject windDirection;

    public void Start()
    {

        // Set the leaf animation to a random start from and animation speed
        anim = leaf.GetComponent<Animation>();
        anim["leafanim"].time = Random.Range(0, 20);
        anim["leafanim"].speed = Random.Range(0.3f, 2.0f);
        anim.Play("leafanim");
       
    }

    public void Update()
    {

        leafPosition += BlowingLeavesA.leafSpeedShared;
        transform.position += transform.forward * Time.deltaTime * BlowingLeavesA.leafSpeedShared;

    }

    private void OnDestroy()
    {
  
        BlowingLeavesA.leafCount -= 1;
        if (BlowingLeavesA.leafCount < 0)
        { BlowingLeavesA.leafCount = 0; }

    }

}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkCube : MonoBehaviour
{
    public float angularSpeed = 200;
    public float linearSpeed = 100;
    public string id = string.Empty;
    public bool mainCube = false;
    // Start is called before the first frame update
    void Start()
    {
    }

    public void Setup(string _id)
    {
        id = _id;
    }

    // Update is called once per frame
    void Update()
    {
        var angle = angularSpeed * Time.deltaTime;
        this.transform.Rotate(0,angle,0);
        if(mainCube){
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                this.transform.position += new Vector3(-linearSpeed * Time.deltaTime, 0, 0);
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                this.transform.position += new Vector3(linearSpeed * Time.deltaTime, 0, 0);
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                this.transform.position += new Vector3(0, linearSpeed * Time.deltaTime, 0);
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                this.transform.position += new Vector3(0, -linearSpeed * Time.deltaTime, 0);

        }
    }

    // Changes Color Every Second - on server Update message
    public void ChangeColor(float r, float g, float b)
    {
        this.gameObject.GetComponent<Renderer>().material.color = 
            new Color(
                r,
                g,
                b,
                1.0f
            );        

    }
}

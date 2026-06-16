using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.IO.Ports;
using System.Threading;

public class ArduinoControl : MonoBehaviour
{
    string analog_data;

    public SerialPort sp = new SerialPort("COM3", 9600);

    void Awake() // Awake() is called before Start()
    {
        analog_data = "0.0";

        //Open the serial stream
        sp.Open();
        sp.WriteLine("1"); //Makes serail available on the Arduino
        sp.DtrEnable = true;
        sp.RtsEnable = true;
        sp.WriteTimeout = 300;
        sp.ReadTimeout = 5000;

        Thread thread = new Thread(Run);
        thread.Start();

        Debug.Log("Received data from Arduino " + analog_data);
    }

    void Start()
    {

    }

    void Run()
    {
        try
        {
            analog_data = sp.ReadLine();
            Debug.Log("Received data from Arduino " + analog_data);
        }
        catch (TimeoutException t)
        {
            Debug.Log("Arduino Read timeout" + t);
        }

        while (true)
        {
            //reading incoming string from Arduino to Unity
            analog_data = sp.ReadLine();

        }
    }

    // Update is called once per frame
    void Update()
    {
        if (sp.IsOpen)
        {
            analog_data = sp.ReadLine();
            Debug.Log("Received data from Arduino " + analog_data);
        }
            
    
    }



}

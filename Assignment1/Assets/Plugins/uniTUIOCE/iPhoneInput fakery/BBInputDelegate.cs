using UnityEngine;
using System.Collections;
using TUIO;
using System.Collections.Generic;

public class BBInputDelegate : MonoBehaviour
{
	public GameObject TouchReceiver { get; set; }
		
	protected BBInputController TuioInput { get; set; }

    public virtual void setup()
    {
    }

    public void Awake()
	{
		TuioInput = new BBInputController();
		TuioInput.CollectEvents = true;
				
		DontDestroyOnLoad(this);
		setup();
	}
    void Update()
	{
		processEvents();
	}
    public void processEvents()
    {
        ArrayList events = TuioInput.GetAndClearCursorEvents();
        if (TouchReceiver != null)
        {
            TouchReceiver.SendMessage("processEvents", events);
        }
    }
	void OnApplicationQuit()
	{
		if (TuioInput != null)
		{
			TuioInput.CollectEvents = false;
			TuioInput.Disconnect();
		}
	}
	

}

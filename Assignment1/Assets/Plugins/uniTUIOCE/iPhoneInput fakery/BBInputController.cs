
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TUIO;

public class BBInputController : TuioListener {
    private TuioClient client;

    public ArrayList activeCursorEvents { get; } = new ArrayList();

    public bool CollectEvents { get; set; }
    private object ObjectSync { get; } = new object();


    public BBInputController() 
	{
		client = new TuioClient(3333);
		client.addTuioListener(this);
		client.connect();
	}


	public ArrayList GetAndClearCursorEvents() {
		ArrayList bufferList;
		lock(ObjectSync) {
			bufferList = new ArrayList(activeCursorEvents);
			activeCursorEvents.Clear();
		}
		return bufferList;
	}

	public void Disconnect() 
	{
		client.disconnect();
		client.removeTuioListener(this);
	}

	public int currentFrame()
	{
		return client.currentFrameNumber();		
	}
	
	public string getStatusString()
	{
		return client.getStatusString();		
	}
	public void addTuioObject(TuioObject o) {
	}
	
	public void updateTuioObject(TuioObject o) {
	}
	
	public void removeTuioObject(TuioObject o) {
	}
	public void addTuioCursor(TuioCursor tuioCursor)
	{
        AddCursorEvent(tuioCursor, BBCursorState.Add);
	}

    public void updateTuioCursor(TuioCursor tuioCursor)
	{
	    AddCursorEvent(tuioCursor, BBCursorState.Update);
	}

    public void removeTuioCursor(TuioCursor tuioCursor)
	{
	    AddCursorEvent(tuioCursor, BBCursorState.Remove);
	}

    public void AddCursorEvent(TuioCursor tuioCursor, BBCursorState bbCursorState)
    {
        lock (ObjectSync)
        {
            if (CollectEvents)
            {
                activeCursorEvents.Add(new BBCursorEvent(tuioCursor, bbCursorState));
            }
        }
    }

    public void refresh(TuioTime ftime)
    {
	}
}
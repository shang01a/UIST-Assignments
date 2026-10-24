using TUIO;

public class BBCursorEvent 
{
	public TuioCursor cursor;
	public BBCursorState state;
	
	public BBCursorEvent(TuioCursor cursor,BBCursorState state) {
		this.cursor = cursor;
		this.state = state;
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class MessageEventArgs : EventArgs
{
    /// <summary>
    /// Get the message from this event.
    /// </summary>
    public string Message
    { get; private set; }

    /// <summary>
    /// Create a new Messag Event Argument.
    /// </summary>
    /// <param name="message">The message being sent.</param>
    public MessageEventArgs(string message)
    {
        Message = message;
    }
}

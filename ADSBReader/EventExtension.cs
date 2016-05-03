using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

static class EventExtension
{
    public static void SafeInvoke<T>(this EventHandler<T> theEvent, object sender, T e) where T : EventArgs
    {
        EventHandler<T> handler = theEvent;

        if (handler != null)
        {
            handler.Invoke(sender, e);
        }
    }
}

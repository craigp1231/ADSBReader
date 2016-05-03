using System;

public class GenericEventArgs<T> : EventArgs
{
    /// <summary>
    /// Get the value stored for this generic event.
    /// </summary>
    public T Value
    { get; private set; }

    /// <summary>
    /// Create a new GenericEvent for your desired data type.
    /// </summary>
    /// <param name="arg">The data to store in Value.</param>
    public GenericEventArgs(T arg)
    {
        Value = arg;
    }
}

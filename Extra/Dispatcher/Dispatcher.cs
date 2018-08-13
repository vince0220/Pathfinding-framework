using System;
using System.Collections.Generic;

public class Dispatcher : IDispatcher
{
    private static Dispatcher instance;

    public static Dispatcher Instance
    {
        get
        {
            if (instance == null)
            {
                // Instance singleton on first use.
                instance = new Dispatcher();
            }

            return instance;
        }
    }

    public List<Action> pending = new List<Action>();


    //
    // Schedule code for execution in the main-thread.
    //
    public void Invoke(Action fn)
    {
        lock (pending)
        {
            pending.Add(fn);
        }
    }

    //
    // Execute pending actions.
    //
    public void InvokePending()
    {
        lock (pending)
        {
            foreach (var func in pending)
            {
                func();
            }

            pending.Clear();
        }
    }
} 
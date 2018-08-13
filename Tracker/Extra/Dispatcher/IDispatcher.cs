using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDispatcher
{
    void Invoke(System.Action fn);
}

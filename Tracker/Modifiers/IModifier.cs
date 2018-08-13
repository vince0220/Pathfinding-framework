using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IModifier<T> {
	Stack<T> ModifyPath(Stack<T> Path);
}

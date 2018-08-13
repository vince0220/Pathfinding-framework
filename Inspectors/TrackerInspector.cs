#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Tracker{
	[CustomEditor(typeof(TrackerManager))]
	public class TrackerInspector : Editor {
		public override void OnInspectorGUI ()
		{
			base.DrawDefaultInspector ();
		}
	}
}
#endif
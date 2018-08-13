#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Tracker;

[CustomEditor(typeof(Tracker.Grids.GridGraph))]
public class GridGraphInspector : Editor {
	public override void OnInspectorGUI ()
	{
		// get target
		Tracker.Grids.GridGraph Graph = (Tracker.Grids.GridGraph)target;

		// render
		EditorGUILayout.LabelField ("Terrain Settings", EditorStyles.boldLabel);
		Graph.TerrainLayer = LayerMaskField (new GUIContent("Terrain Layer","The layer(s) that are used to raycast for the terrain height and slope"),Graph.TerrainLayer,false);
		Graph.MaxSlope = EditorGUILayout.Slider (new GUIContent("Max Slope","The maximal slope a agent can walk on"),Graph.MaxSlope, 0f, 90f);

		EditorGUILayout.Space ();
		EditorGUILayout.LabelField ("Grid Settings", EditorStyles.boldLabel);
		Graph.GridExtends = EditorGUILayout.Vector3Field (new GUIContent("Grid Extends","The extends of the grid. Its half the actual size of the grid graph"),Graph.GridExtends);
		Graph.GridCenter = EditorGUILayout.Vector3Field (new GUIContent("Grid Center","The center of the grid graph"),Graph.GridCenter);

		float Min = Mathf.Min (Graph.GridExtends.x, Graph.GridExtends.y);
		Graph.NodeSize = EditorGUILayout.Slider (new GUIContent("Grid Node Size","The size of the nodes inside the grid graph"),Graph.NodeSize, Mathf.Min(0.01f,Min), 10f);

		EditorGUILayout.Space ();
		EditorGUILayout.LabelField ("Obstacle Settings", EditorStyles.boldLabel);
		Graph.ObstaclePadding = EditorGUILayout.Slider (new GUIContent("Obstacle Padding","The minimal thickness of obstacle casts"),Graph.ObstaclePadding, 0f, 50f);
		Graph.ObstacleLayer = LayerMaskField (new GUIContent("Obstacle Layer","The layer(s) that are used to check for potintional obstacles"),Graph.ObstacleLayer,false);

		EditorGUILayout.Space ();
		EditorGUILayout.LabelField ("Debug Settings", EditorStyles.boldLabel);
		Graph.DebugEnabled = EditorGUILayout.Toggle (new GUIContent("Debug","Determines wheter debug mode is enabled"),Graph.DebugEnabled);
		if (Graph.DebugEnabled) {
			EditorGUILayout.Space ();
			EditorGUI.indentLevel += 1;
			Graph.DrawUnwalkableNodes = EditorGUILayout.Toggle (new GUIContent("Draw Unwalkable Nodes","Determines wheter a unwalkable node should be drawn in debug mode or not"),Graph.DrawUnwalkableNodes);
			Graph.WalkableColor = EditorGUILayout.ColorField (new GUIContent ("Walkable Color", "The color of walkable nodes in the debug grid"), Graph.WalkableColor);
			Graph.CeilingWalkableColor = EditorGUILayout.ColorField (new GUIContent ("Ceiling-Walkable Color", "The color of walkable nodes with a ceiling above it"), Graph.CeilingWalkableColor);
			Graph.NoneWalkableColor = EditorGUILayout.ColorField (new GUIContent ("None-Walkable Color", "The color of none-walkable nodes in the debug grid"), Graph.NoneWalkableColor);
			EditorGUI.indentLevel -= 1;

			EditorGUILayout.Space ();
			if (GUILayout.Button ("Generate Temp-Grid")) {
				Graph.Initialize (); // init
			}
		}

		EditorGUILayout.Space ();
		EditorGUILayout.LabelField ("Debug Values", EditorStyles.boldLabel);
		EditorGUILayout.LabelField ("Total Node Count: "+GetTotalNodeCount(Graph));
		EditorGUILayout.LabelField ("Region Count: "+Graph.RegionCount);

		EditorUtility.SetDirty (Graph); // set graph dirty
		SceneView.RepaintAll (); // repaint scene
	}

	#region Get / set
	public int GetTotalNodeCount(Tracker.Grids.GridGraph Graph){
		var GridNodeCount = new Vector2 ( // calculate node count
			(int)((Graph.GridExtends.x * 2f) / Graph.NodeSize),
			(int)((Graph.GridExtends.z * 2f) / Graph.NodeSize)
		);

		return (int)(GridNodeCount.x * GridNodeCount.y); // calculate total node count
	}
	#endregion

	#region Statics
	public static LayerMask LayerMaskField (string label, LayerMask selected) {
		return LayerMaskField (label,selected,true);
	}
	public static LayerMask LayerMaskField (string label, LayerMask selected, bool showSpecial = true) {
		return LayerMaskField (new GUIContent(label,""),selected,showSpecial);
	}
	public static LayerMask LayerMaskField(GUIContent Label, LayerMask selected, bool showSpecial = true){
		List<GUIContent> layers = new List<GUIContent>();
		List<int> layerNumbers = new List<int>();

		string selectedLayers = "";

		for (int i=0;i<32;i++) {

			string layerName = LayerMask.LayerToName (i);

			if (layerName != "") {
				if (selected == (selected | (1 << i))) {

					if (selectedLayers == "") {
						selectedLayers = layerName;
					} else {
						selectedLayers = "Mixed";
					}
				}
			}
		}

		EventType lastEvent = Event.current.type;

		if (Event.current.type != EventType.MouseDown && Event.current.type != EventType.ExecuteCommand) {
			if (selected.value == 0) {
				layers.Add (new GUIContent("Nothing"));
			} else if (selected.value == -1) {
				layers.Add (new GUIContent("Everything"));
			} else {
				layers.Add (new GUIContent(selectedLayers));
			}
			layerNumbers.Add (-1);
		}

		if (showSpecial) {
			layers.Add (new GUIContent((selected.value == 0 ? "[X] " : "     ") + "Nothing"));
			layerNumbers.Add (-2);

			layers.Add (new GUIContent((selected.value == -1 ? "[X] " : "     ") + "Everything"));
			layerNumbers.Add (-3);
		}

		for (int i=0;i<32;i++) {

			string layerName = LayerMask.LayerToName (i);

			if (layerName != "") {
				if (selected == (selected | (1 << i))) {
					layers.Add (new GUIContent("[X] "+layerName));
				} else {
					layers.Add (new GUIContent("     "+layerName));
				}
				layerNumbers.Add (i);
			}
		}

		bool preChange = GUI.changed;

		GUI.changed = false;

		int newSelected = 0;

		if (Event.current.type == EventType.MouseDown) {
			newSelected = -1;
		}

		newSelected = EditorGUILayout.Popup (Label,newSelected,layers.ToArray(),EditorStyles.layerMaskField);

		if (GUI.changed && newSelected >= 0) {
			if (showSpecial && newSelected == 0) {
				selected = 0;
			} else if (showSpecial && newSelected == 1) {
				selected = -1;
			} else {

				if (selected == (selected | (1 << layerNumbers[newSelected]))) {
					selected &= ~(1 << layerNumbers[newSelected]);
				} else {
					selected = selected | (1 << layerNumbers[newSelected]);
				}
			}
		} else {
			GUI.changed = preChange;
		}

		return selected;
	}
	#endregion
}
#endif
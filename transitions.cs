#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class AnimationAutoEditor : EditorWindow
{
    [MenuItem("Tools/Elly/Transition editor")]
    public static void ShowWindow()
    {
        GetWindow(typeof(AnimationAutoEditor));
    }

    private string layerName = "";

    private void OnGUI()
    {
        GUILayout.Label("List selected animation states", EditorStyles.boldLabel);

        if (GUILayout.Button("List"))
        {
            ListAnimationStates();
        }

        EditorGUILayout.LabelField("Enter layer name:");
        layerName = EditorGUILayout.TextField(layerName);
    }


    private void ListAnimationStates()
    {
        UnityEngine.Object[] selectedObjects = Selection.objects;

        foreach (var selectedObject in selectedObjects)
        {
            AnimatorController controller = selectedObject as AnimatorController;
            if (controller != null)
            {
                List<string> stateNames = new List<string>();
                foreach (var layer in controller.layers)
                {
                    // Check if the layer name matches the user input
                    if (layer.name == layerName)
                    {
                        foreach (var state in layer.stateMachine.states)
                        {
                            stateNames.Add(state.state.name);
                        }
                    }
                }
                Debug.Log(string.Join(", ", stateNames));
            }
        }
    }

}

#endif
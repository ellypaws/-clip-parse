#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Animations;

public class AnimatorStatesLister : EditorWindow
{

    private string[] statuses = { };
    private AnimatorController activeController;
    private int activeLayer = 0;
    [MenuItem("Tools/Elly/Animator States Lister")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorStatesLister>("Animator States");
    }

    void OnGUI()
    {
        if (GUILayout.Button("List States"))
        {
            EditorGUILayout.HelpBox("Loading States", MessageType.Info);
            ListAnimatorStates();
        }

        if (activeController)
        {
            GUILayout.Label("Controller Name");
            GUILayout.TextField(activeController.name);

            var layers = from l in activeController.layers select l.name;

            activeLayer = EditorGUILayout.Popup("Layer", activeLayer, layers.ToArray());

            var layer = activeController.layers[activeLayer];
            foreach (var state in layer.stateMachine.states)
            {
                EditorGUILayout.LabelField("State:", state.state.name);
            }

            if (GUILayout.Button("Figure it out"))
            {
                DoSomething();
            }
        }

        if (statuses.Length > 0)
        {
            EditorGUILayout.Space();
            GUILayout.Label("Logs", EditorStyles.boldLabel);
            foreach (var status in statuses)
            {
                GUILayout.Label(status);
            }
        }
    }

    private void DoSomething()
    {

    }

    private void ListAnimatorStates()
    {
        ArrayUtility.Clear(ref statuses);
        Object selectedObject = Selection.activeObject;

        if (selectedObject == null)
        {
            ArrayUtility.Add(ref statuses, "Error: No asset selected.");
            EditorGUILayout.HelpBox("No asset selected.", MessageType.Info);
            return;
        }

        if (selectedObject is AnimatorController)
        {
            AnimatorController controller = selectedObject as AnimatorController;
            activeController = controller;

            foreach (var layer in controller.layers)
            {
                EditorGUILayout.LabelField("Layer:", layer.name);
                foreach (var state in layer.stateMachine.states)
                {
                    EditorGUILayout.LabelField("State:", state.state.name);
                }
            }
        }
        else
        {
            ArrayUtility.Add(ref statuses, "Error: The selected asset is not an Animator Controller.");
            EditorGUILayout.HelpBox("The selected asset is not an Animator Controller.", MessageType.Warning);
        }

        ArrayUtility.Insert(ref statuses, statuses.Length, "Finished");
    }
}

#endif
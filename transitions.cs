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
    private int activeStateMachine = 0;
    private int activeAction = 0;
    private string[] statesAvailable = { };

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

            var stateMachines = from s in layer.stateMachine.stateMachines select s.stateMachine.name;
            var stateMachinesArray = stateMachines.ToArray();
            ArrayUtility.Insert(ref stateMachinesArray, 0, "Root");
            activeStateMachine = EditorGUILayout.Popup("State Machine", activeStateMachine, stateMachinesArray);

            // Enumerate actions where we grab from A_`actionname`_01_B and list unique action names
            var childAnimatorStates = activeStateMachine == 0
                ? layer.stateMachine.states
                : layer.stateMachine.stateMachines[activeStateMachine - 1].stateMachine.states;
            var actions = from s in childAnimatorStates select s.state.name.Split('_').Skip(1).Take(1);
            var actionNames = actions.SelectMany(x => x).Distinct().ToArray();
            activeAction = EditorGUILayout.Popup("Action", activeAction, actionNames);

            // check if the action is A_`actionname`
            var statesAvailable = from s in childAnimatorStates
                where s.state.name.StartsWith("A_" + actionNames[activeAction])
                select s.state.name;
            foreach (var state in statesAvailable)
            {
                // scrollable list of states
                GUILayout.Label(state);
            }

            if (GUILayout.Button("Figure it out"))
            {
                DoSomething(layer);
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

    private void DoSomething(AnimatorControllerLayer layer)
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
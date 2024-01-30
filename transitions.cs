#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Animations;
using System.Text.RegularExpressions;

public class RegexClip
{
    public string Action { get; set; }
    public string Char { get; set; }
    public string ClipNumber { get; set; }
    public string Alternate { get; set; }
    public string TransitionTo { get; set; }
    public string NextName { get; set; }
    public string NextClip { get; set; }
}

public class AnimationTransition
{
    public string Name { get; set; }
    public List<string> NextAnimations { get; set; } = new List<string>();
    public string[] AlternateAnimations { get; set; }
    public string PreviousAnimation { get; set; }
}

public class AnimatorStatesLister : EditorWindow
{
    private const string action = "action";
    private const string character = "char";
    private const string clipNumber = "clip";
    private const string alternate = "alternate";
    private const string transitionTo = "transitionTo";
    private const string nextName = "nextName";
    private const string nextClip = "nextClip";
    
    private string[] statuses = { };
    private AnimatorController activeController;
    private int activeLayer = 0;
    private int activeStateMachine = 0;
    private int activeAction = 0;
    private int activeClip = 0;

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

            activeClip = EditorGUILayout.Popup("Clip", activeClip, statesAvailable.ToArray());

            // put activeClip in AnimationTransition class and call GetNextAnimation
            var clip = new AnimationTransition
            {
                Name = statesAvailable.ElementAt(activeClip)
            };
            GetNextAnimation(clip, childAnimatorStates);
            GUILayout.Label("Next Animations");
            foreach (var nextAnimation in clip.NextAnimations)
            {
                GUILayout.Label(nextAnimation);
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


    private void GetNextAnimation(AnimationTransition clip, ChildAnimatorState[] allAnimations)
    {
        var pattern =
            @"A_(?<action>[a-z]+)_(?<char>[A-Z]?)_?(?<clip>\d{2})_?(?<alternate>[A-Z]?)?-?(?<transitionTo>(?<nextName>[a-z]+)?_?(?<nextClip>\d{2}))?";
        var match = Regex.Match(clip.Name, pattern);

        if (!match.Success)
        {
            // debug to unity console
            Debug.Log("No match found for " + clip.Name);
            return;
        }

        var result = match.Groups.Cast<Group>().ToDictionary(g => g.Name, g => g.Value);

        if (result[alternate] != "")
        {
            // Alternate clips don't have next animations, but use alternate animations instead
            return;
        }

        // Check for transition animations first
        if (result[transitionTo] != "")
        {
            findTransition(clip, allAnimations, result);
            return;
        }

        var nextClipName = string.Format("A_{0}_{1}", result[action], int.Parse(result[clipNumber]) + 1);
        if (result[character] != "")
        {
            nextClipName = string.Format("A_{0}_{1}_{2}", result[action], result[character],
                int.Parse(result[clipNumber]) + 1);
        }

        var nextClip = FindAnimationByName($"^{clip.Name}-", allAnimations);

        if (nextClip.Equals(null))
        {
            // Try appending "A" or "_A" to the end (e.g., 01 -> 02, 01 -> 02A, 01 -> 02_A)
            nextClip = FindAnimationByName($"^{nextClipName}_?A?$", allAnimations);
        }

        if (!nextClip.Equals(null))
        {
            // append to clip.NextAnimations
            clip.NextAnimations.Add(nextClip.GetValueOrDefault().state.name);
        }
    }

    private ChildAnimatorState? FindAnimationByName(string expression, ChildAnimatorState[] allAnimations)
    {
        return allAnimations.FirstOrDefault(x => Regex.IsMatch(x.state.name, expression));
    }

    // findTransition finds the next animation based on the transitionTo field.
    private void findTransition(AnimationTransition clip, ChildAnimatorState[] allAnimations,
        Dictionary<string, string> result)
    {
        // No nextName means transition (e.g., 01-02 -> 02)
        if (result[nextName] == "")
        {
            // Transition within the same action but different clip number
            var nextClipName = $"A_{result[action]}_{result[transitionTo]}";
            if (result[character] != "")
            {
                nextClipName = $"A_{result[action]}_{result[character]}_{result[transitionTo]}";
            }
            var nextClip = FindAnimationByName($"^{nextClipName}_?A?$", allAnimations);
            if (nextClip.Equals(null))
            {
                // Try appending "A" or "_A" to the end (e.g., 01 -> 02, 01 -> 02A, 01 -> 02_A)
                nextClip = FindAnimationByName($"^{nextClipName}_?A$", allAnimations);
            }
            if (!nextClip.Equals(null))
            {
                // append to clip.NextAnimations
                clip.NextAnimations.Add(nextClip.GetValueOrDefault().state.name);
            }
        }
        else
        {
            // With nextName means transition to another action (e.g., 01 -> 01-relax_01 -> relax_01)
            var nextClipName = $"A_{result[transitionTo]}";
            if (result[character] != "")
            {
                nextClipName = $"A_{result[transitionTo]}_{result[character]}";
            }
            var nextClip = FindAnimationByName($"^{nextClipName}_?A?$", allAnimations);
            if (nextClip.Equals(null))
            {
                // Try appending "A" or "_A" to the end (e.g., 01 -> 02, 01 -> 02A, 01 -> 02_A)
                nextClip = FindAnimationByName($"^{nextClipName}_?A$", allAnimations);
            }
        }

    }

    private void DoSomething(AnimatorControllerLayer layer)
    {
    }

    public RegexClip ParseClipName(string clipName)
    {
        var pattern =
            @"A_(?<action>[a-z]+)_(?<char>[A-Z]?)_?(?<clip>\d{2})_?(?<alternate>[A-Z]?)?-?(?<transitionTo>(?<nextName>[a-z]+)?_?(?<nextClip>\d{2}))?";
        var match = Regex.Match(clipName, pattern);

        if (!match.Success)
        {
            // debug to unity console
            Debug.Log("No match found for " + clipName);
            return null;
        }

        return new RegexClip
        {
            Action = match.Groups[action].Value,
            Char = match.Groups[character].Value,
            ClipNumber = match.Groups[clipNumber].Value,
            Alternate = match.Groups[alternate].Value,
            TransitionTo = match.Groups[transitionTo].Value,
            NextName = match.Groups[nextName].Value,
            NextClip = match.Groups[nextClip].Value
        };
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
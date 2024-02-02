#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Animations;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Object = UnityEngine.Object;

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
    private int activeNextParameter = 0;
    private int activePreviousParameter = 0;

    private bool[] stateToggles = { };

    private bool setNextAnimation = true;
    private bool setPreviousAnimation = true;
    private bool setAlternateAnimations = true;

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

            var layer = LayerSelection();

            var stateMachine = StateMachineSelection(layer);

            // Enumerate actions where we grab from A_`actionname`_01_B and list unique action names
            var statesAvailable = ActionsSlections(layer);

            if (statesAvailable.Length > 0)
            {
                EditorGUILayout.Space();
                GUILayout.Label("States", EditorStyles.boldLabel);
                // check if the action is A_`actionname`
                var clip = ListStates(statesAvailable);

                ShowNextAnimation(statesAvailable);
                ShowPreviousAnimation(statesAvailable);
                ShowAlternateAnimations(statesAvailable);
                ShowBehaviors(clip);

                ShowAvailableConditions();

                EditorGUILayout.Space();
                // checkboxes for setting transitions
                setNextAnimation = EditorGUILayout.Toggle("Set Next Animation", setNextAnimation);
                setPreviousAnimation = EditorGUILayout.Toggle("Set Previous Animation", setPreviousAnimation);
                setAlternateAnimations = EditorGUILayout.Toggle("Set Alternate Animations", setAlternateAnimations);

                var statesActive = (from s in statesAvailable
                    where stateToggles.ElementAt(statesAvailable.ToList().IndexOf(s))
                    select s).ToArray();

                if (GUILayout.Button("Set Transition"))
                {
                    SetTransitions(statesActive);
                }

                if (GUILayout.Button("Clear Transitions"))
                {
                    ClearTransitions(statesActive);
                }

                EditorGUILayout.Space();

                if (GUILayout.Button("Copy behavior from clip"))
                {
                    CopyBehaviorFromClip(clip, statesActive);
                }

                if (GUILayout.Button("Clear behaviors"))
                {
                    ClearBehaviors(statesActive);
                }
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

    private void ClearBehaviors(ChildAnimatorState[] statesAvailable)
    {
        foreach (var state in statesAvailable)
        {
            state.state.behaviours = Array.Empty<StateMachineBehaviour>();
        }
    }

    private void CopyBehaviorFromClip(ChildAnimatorState clip, ChildAnimatorState[] statesAvailable)
    {
        var behaviors = clip.state.behaviours;
        foreach (var state in statesAvailable)
        {
            if (state.state.name.Contains("-"))
            {
                continue;
            }

            state.state.behaviours = behaviors;
        }
    }

    private bool[] behaviorToggles = { };

    private ChildAnimatorState ShowBehaviors(ChildAnimatorState clip)
    {
        EditorGUILayout.Space();
        GUILayout.Label("Behaviors", EditorStyles.boldLabel);
        var behaviors = clip.state.behaviours;
        for (var i = 0; i < behaviors.Length; i++)
        {
            if (behaviorToggles == null || behaviorToggles.Length != behaviors.Length)
            {
                behaviorToggles = Enumerable.Repeat(true, behaviors.Length).ToArray();
            }

            behaviorToggles[i] = GUILayout.Toggle(behaviorToggles.ElementAt(i), behaviors.ElementAt(i).GetType().Name);
        }

        return clip;
    }

    private ChildAnimatorState ListStates(ChildAnimatorState[] statesAvailable)
    {
        // Initialize toggles array if it's not already initialized
        if (stateToggles == null || stateToggles.Length != statesAvailable.Length)
        {
            stateToggles = Enumerable.Repeat(true, statesAvailable.Length).ToArray();
        }

        for (var i = 0; i < statesAvailable.Length; i++)
        {
            // scrollable list of states with checkboxes
            stateToggles[i] = GUILayout.Toggle(stateToggles.ElementAt(i), statesAvailable.ElementAt(i).state.name);
        }

        activeClip = EditorGUILayout.Popup("Clip", activeClip, statesAvailable.Select(x => x.state.name).ToArray());
        return statesAvailable[activeClip];
    }

    private void ShowNextAnimation(ChildAnimatorState[] statesAvailable)
    {
        // put activeClip in AnimationTransition class and call GetNextAnimation
        var clip = new AnimationTransition
        {
            Name = statesAvailable.ElementAt(activeClip).state.name
        };
        nextAnimation(clip, statesAvailable.ToArray());
        GUILayout.Label("Next Animations: " +
                        (clip.NextAnimations.Count > 0 ? string.Join(", ", clip.NextAnimations) : "None"));
    }

    private void ShowPreviousAnimation(ChildAnimatorState[] statesAvailable)
    {
        // put activeClip in AnimationTransition class and call GetNextAnimation
        var clip = new AnimationTransition
        {
            Name = statesAvailable.ElementAt(activeClip).state.name
        };
        previousAnimation(clip, statesAvailable.ToArray());
        GUILayout.Label("Previous Animation: " + (clip.PreviousAnimation != null ? clip.PreviousAnimation : "None"));
    }

    private void ShowAlternateAnimations(ChildAnimatorState[] statesAvailable)
    {
        // put activeClip in AnimationTransition class and call GetNextAnimation
        var clip = new AnimationTransition
        {
            Name = statesAvailable.ElementAt(activeClip).state.name
        };
        alternateAnimations(clip, statesAvailable.ToArray());
        GUILayout.Label("Alternate Animations: " +
                        ((clip.AlternateAnimations != null && clip.AlternateAnimations.Length > 0)
                            ? string.Join(", ", clip.AlternateAnimations)
                            : "None"));
    }

    // ShowAvailableConditions shows the available Parameters and Conditions of the controller as a dropdown
    private void ShowAvailableConditions()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Conditions", EditorStyles.boldLabel);
        var parameters = from p in activeController.parameters select p.name;
        activeNextParameter = EditorGUILayout.Popup("Next Trigger", activeNextParameter, parameters.ToArray());
        activePreviousParameter =
            EditorGUILayout.Popup("Previous Trigger", activePreviousParameter, parameters.ToArray());
    }

    private ChildAnimatorState[] ActionsSlections(AnimatorControllerLayer layer)
    {
        var childAnimatorStates = activeStateMachine == 0
            ? layer.stateMachine.states
            : layer.stateMachine.stateMachines[activeStateMachine - 1].stateMachine.states;

        var allStates = from s in childAnimatorStates
            where s.state.name.StartsWith("A_")
            select s;
        var actionNames = (from s in childAnimatorStates select s.state.name.Split('_').Skip(1).Take(1))
            .SelectMany(x => x)
            .Distinct().ToArray();
        ArrayUtility.Insert(ref actionNames, 0, "All");
        if (actionNames.Length > 0)
        {
            activeAction = EditorGUILayout.Popup("Action", activeAction, actionNames);
        }

        return activeAction == 0
            ? allStates.ToArray()
            : (from s in allStates
                where s.state.name.StartsWith($"A_{actionNames[activeAction]}")
                select s).ToArray();
    }

    private AnimatorStateMachine StateMachineSelection(AnimatorControllerLayer layer)
    {
        var stateMachines = from s in layer.stateMachine.stateMachines select s.stateMachine.name;
        var stateMachinesArray = stateMachines.ToArray();
        ArrayUtility.Insert(ref stateMachinesArray, 0, "Root");
        activeStateMachine = EditorGUILayout.Popup("State Machine", activeStateMachine, stateMachinesArray);
        if (activeStateMachine == 0)
        {
            return layer.stateMachine;
        }

        return layer.stateMachine.stateMachines[activeStateMachine - 1].stateMachine;
    }

    private AnimatorControllerLayer LayerSelection()
    {
        var layers = from l in activeController.layers select l.name;
        activeLayer = EditorGUILayout.Popup("Layer", activeLayer, layers.ToArray());
        return activeController.layers[activeLayer];
    }

    private const string Pattern =
        @"A_(?<action>[a-z]+)_(?<char>[A-Z]?)_?(?<clip>\d{2})_?(?<alternate>[A-Z]?)?-?(?<transitionTo>(?<nextName>[a-z]+)?_?(?<nextClip>\d{2}))?";

    private void nextAnimation(AnimationTransition clip, ChildAnimatorState[] available)
    {
        var match = Regex.Match(clip.Name, Pattern);

        if (!match.Success)
        {
            // debug to unity console
            Debug.Log("No match found for " + clip.Name);
            return;
        }

        var result = match.Groups.Cast<Group>().ToDictionary(g => g.Name, g => g.Value);

        if (result[alternate] != "" && result[alternate] != "A")
        {
            // Alternate clips don't have next animations, but use alternate animations instead unless it's the first clip (A)
            return;
        }

        // Check for transition animations first
        if (result[transitionTo] != "")
        {
            findTransition(clip, available, result);
            return;
        }

        var nextClipName = $"A_{result[action]}_{(int.Parse(result[clipNumber]) + 1):D2}";
        if (result[character] != "")
        {
            nextClipName = $"A_{result[action]}_{result[character]}_{(int.Parse(result[clipNumber]) + 1):D2}";
        }

        var nextClip = FindAnimationByName($"^{clip.Name.TrimEnd('A').TrimEnd('_')}-", available);

        if (nextClip.Equals(null))
        {
            // Try appending "A" or "_A" to the end (e.g., 01 -> 02, 01 -> 02A, 01 -> 02_A)
            nextClip = FindAnimationByName($"^{nextClipName}_?A?$", available);
        }

        if (nextClip.HasValue && nextClip.Value.state != null)
        {
            // append to clip.NextAnimations
            clip.NextAnimations.Add(nextClip.GetValueOrDefault().state.name);
        }
    }

    private void previousAnimation(AnimationTransition clip, ChildAnimatorState[] available)
    {
        var match = Regex.Match(clip.Name, Pattern);

        if (!match.Success)
        {
            // debug to unity console
            Debug.Log("No match found for " + clip.Name);
            return;
        }

        var result = match.Groups.Cast<Group>().ToDictionary(g => g.Name, g => g.Value);

        if (result[alternate] != "" && result[alternate] != "A")
        {
            // Alternate clips don't have previous animations unless it's the first clip (A)
            return;
        }

        // Check for transition animations first
        if (result[transitionTo] != "")
        {
            // Transition animations don't have previous animations
            return;
        }

        var previousClipName = $"A_{result[action]}_{(int.Parse(result[clipNumber]) - 1):D2}";
        if (result[character] != "")
        {
            previousClipName = $"A_{result[action]}_{result[character]}_{(int.Parse(result[clipNumber]) - 1):D2}";
        }

        var previousClip = FindAnimationByName($"^{previousClipName}_?A?$", available);

        // Check if previousClip has a value and its state is not null
        if (previousClip.HasValue && previousClip.Value.state != null)
        {
            clip.PreviousAnimation = previousClip.Value.state.name;
        }
    }

    private ChildAnimatorState? FindAnimationByName(string expression, ChildAnimatorState[] allAnimations)
    {
        return allAnimations.FirstOrDefault(x => Regex.IsMatch(x.state.name, expression));
    }

    [CanBeNull]
    private ChildAnimatorState[] FilterAnimations(string expression, ChildAnimatorState[] allAnimations)
    {
        return allAnimations.Where(x => Regex.IsMatch(x.state.name, expression)).ToArray();
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

    private void alternateAnimations(AnimationTransition clip, ChildAnimatorState[] available)
    {
        var match = Regex.Match(clip.Name, Pattern);

        if (!match.Success)
        {
            // debug to unity console
            Debug.Log("No match found for " + clip.Name);
            return;
        }

        var result = match.Groups.Cast<Group>().ToDictionary(g => g.Name, g => g.Value);

        if (result[transitionTo] != "")
        {
            // Transition animations don't have alternate animations
            return;
        }

        var toFind = $"A_{result[action]}_{result[clipNumber]}";
        if (result[character] != "")
        {
            toFind = $"A_{result[action]}_{result[character]}_{result[clipNumber]}";
        }

        var altClips = FilterAnimations($"^{toFind}_?[A-Z]?$", available);

        if (altClips == null || altClips.Length == 0)
        {
            // No alternate animations found
            return;
        }

        foreach (var alt in altClips)
        {
            if (alt.state.name == clip.Name)
            {
                continue;
            }

            if (clip.AlternateAnimations == null)
            {
                clip.AlternateAnimations = Array.Empty<string>();
            }

            clip.AlternateAnimations = clip.AlternateAnimations.Append(alt.state.name).ToArray();
        }
    }

    private void ClearTransitions(ChildAnimatorState[] statesAvailable)
    {
        for (var i = 0; i < statesAvailable.Length; i++)
        {
            var state = statesAvailable.ElementAt(i).state;
            state.transitions = Array.Empty<AnimatorStateTransition>();
        }
    }

    private void SetTransitions(ChildAnimatorState[] statesAvailable)
    {
        // Get the selected parameter
        var nextParameter = activeController.parameters[activeNextParameter];
        var previousParameter = activeController.parameters[activePreviousParameter];

        foreach (var s in statesAvailable)
        {
            var state = s.state;
            var clip = new AnimationTransition
            {
                Name = state.name
            };

            if (setNextAnimation)
            {
                nextAnimation(clip, statesAvailable);
                foreach (var next in clip.NextAnimations)
                {
                    AddTransition(next, state, nextParameter, statesAvailable);
                }
            }

            if (setPreviousAnimation)
            {
                previousAnimation(clip, statesAvailable);
                if (!String.IsNullOrEmpty(clip.PreviousAnimation) && previousParameter != null)
                {
                    AddTransition(clip.PreviousAnimation, state, previousParameter, statesAvailable);
                }
            }

            if (setAlternateAnimations)
            {
                alternateAnimations(clip, statesAvailable);
                if (clip.AlternateAnimations != null)
                {
                    foreach (var alt in clip.AlternateAnimations)
                    {
                        AddTransition(alt, state, nextParameter, statesAvailable);
                    }
                }
            }
        }
    }

    private void AddTransition(string animationName, AnimatorState state, AnimatorControllerParameter parameter,
        ChildAnimatorState[] statesAvailable)
    {
        ChildAnimatorState? findAnimationByName = FindAnimationByName($"^{animationName}$", statesAvailable);
        if (!findAnimationByName.Equals(null) && findAnimationByName.GetValueOrDefault().state != null)
        {
            // check if state already has a transition to findAnimationByName with parameter
            if (!state.transitions.ToList().Any(x =>
                    x.destinationState == findAnimationByName.Value.state &&
                    x.conditions.Any(y => y.parameter == parameter.name)))
            {
                var transition = state.AddTransition(findAnimationByName.GetValueOrDefault().state);
                transition.AddCondition(AnimatorConditionMode.If, 0, parameter.name);
                transition.hasExitTime = true;
                transition.duration = 0.25f;
            }
        }
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
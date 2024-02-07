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

    private Vector2 scrollPosition;

    [MenuItem("Tools/Elly/Animator States Lister")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorStatesLister>("Animator States");
    }

    private void OnGUI()
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
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

            var availableStates = StateMachineSelection(layer, null);

            // Enumerate actions where we grab from A_`actionname`_01_B and list unique action names
            var statesFromActions = ActionsSelections(availableStates);

            if (statesFromActions.Length > 0)
            {
                EditorGUILayout.Space();
                GUILayout.Label("States", EditorStyles.boldLabel);
                // check if the action is A_`actionname`
                var clip = ListStates(statesFromActions);

                ShowNextAnimation(statesFromActions);
                ShowPreviousAnimation(statesFromActions);
                ShowAlternateAnimations(statesFromActions);
                ShowBehaviors(clip);

                ShowAvailableConditions();

                EditorGUILayout.Space();
                // checkboxes for setting transitions
                setNextAnimation = EditorGUILayout.Toggle("Set Next Animation", setNextAnimation);
                setPreviousAnimation = EditorGUILayout.Toggle("Set Previous Animation", setPreviousAnimation);
                setAlternateAnimations = EditorGUILayout.Toggle("Set Alternate Animations", setAlternateAnimations);

                var statesActive = (from s in statesFromActions
                    where stateToggles.ElementAt(statesFromActions.ToList().IndexOf(s))
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

        GUILayout.EndScrollView();
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

    private Vector2 statesScrollPosition;

    private ChildAnimatorState ListStates(ChildAnimatorState[] statesAvailable)
    {
        // Initialize toggles array if it's not already initialized
        if (stateToggles == null || stateToggles.Length != statesAvailable.Length)
        {
            stateToggles = Enumerable.Repeat(true, statesAvailable.Length).ToArray();
        }

        statesScrollPosition = GUILayout.BeginScrollView(statesScrollPosition, GUILayout.ExpandHeight(true));
        for (var i = 0; i < statesAvailable.Length; i++)
        {
            stateToggles[i] = GUILayout.Toggle(stateToggles.ElementAt(i), statesAvailable.ElementAt(i).state.name);
        }

        GUILayout.EndScrollView();

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

    private ChildAnimatorState[] ActionsSelections(ChildAnimatorState[] availableStates)
    {
        var allStates = from s in availableStates
            where s.state.name.StartsWith("A_")
            select s;
        var actionNames = (from s in availableStates select s.state.name.Split('_').Skip(1).Take(1))
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

    // Recursively display the state machine hierarchy as a foldout.
    // If unfolded, the state machine is filtered and we return the states of the selected state machine and its children.
    // If folded, we return the states of the parent state machine.
    // We start with > Root and then we put each sub state machine under the parent state machine of each sub state machine and so on.
    // Put each sub state machine under the parent state machine of each sub state machine and so on.
    // Each unfolded state machine would be displayed in a dropdown like the following:
    // Root is always present and it's the first item as it's the starting point of the layer. (AnimatorControllerLayer)
    // "Root"
    // "Root > State Machine 1"
    // "Root > State Machine 1 > State Machine 1.1"
    // "Root > State Machine 2"
    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
    private List<string> stateMachinePaths = new List<string>();
    private int activeStateMachineIndex = 0;

    private ChildAnimatorState[] StateMachineSelection(AnimatorControllerLayer selectedLayer,
        AnimatorStateMachine substateMachine = null)
    {
        if (substateMachine == null)
        {
            substateMachine = selectedLayer.stateMachine;
        }

        // Initialize or clear the paths list
        if (stateMachinePaths.Count == 0 || substateMachine == selectedLayer.stateMachine)
        {
            stateMachinePaths.Clear();
            foldoutStates.Clear();
            stateMachinePaths.Add("Root"); // Always add "Root" as the first item
            foldoutStates["Root"] = true; // Default to expanded
            PopulateStateMachinePaths(substateMachine, "Root");
        }

        // Display the dropdown for state machine selection
        activeStateMachineIndex =
            EditorGUILayout.Popup("State Machine:", activeStateMachineIndex, stateMachinePaths.ToArray());

        // Determine the selected state machine based on the active index
        string selectedPath = stateMachinePaths[activeStateMachineIndex];
        AnimatorStateMachine selectedStateMachine = FindStateMachineByPath(selectedLayer.stateMachine, selectedPath);

        // Return the states of the selected state machine
        return selectedStateMachine != null ? selectedStateMachine.states : Array.Empty<ChildAnimatorState>();
    }

    private void PopulateStateMachinePaths(AnimatorStateMachine stateMachine, string pathPrefix)
    {
        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            string path = $"{pathPrefix} > {subStateMachine.stateMachine.name}";
            stateMachinePaths.Add(path);
            foldoutStates[path] = false; // Start as not expanded

            if (foldoutStates[pathPrefix]) // If the parent is expanded, recursively add child state machines
            {
                PopulateStateMachinePaths(subStateMachine.stateMachine, path);
            }
        }
    }

    private AnimatorStateMachine FindStateMachineByPath(AnimatorStateMachine rootStateMachine, string path)
    {
        string[] pathParts = path.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
        AnimatorStateMachine current = rootStateMachine;
        for (int i = 1; i < pathParts.Length; i++) // Start at 1 to skip "Root"
        {
            var found = current.stateMachines.FirstOrDefault(sm => sm.stateMachine.name == pathParts[i]);
            if (found.stateMachine != null)
            {
                current = found.stateMachine;
            }
            else
            {
                return null; // Path does not exist
            }
        }

        return current;
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

        if (!TryFindAnimationByName($"^{clip.Name.TrimEnd('A').TrimEnd('_')}-", available, out var nextClip))
        {
            // Try appending "A" or "_A" to the end (e.g., 01 -> 02, 01 -> 02A, 01 -> 02_A)
            TryFindAnimationByName($"^{nextClipName}_?A?$", available, out nextClip);
        }

        if (nextClip.state != null)
        {
            // append to clip.NextAnimations
            clip.NextAnimations.Add(nextClip.state.name);
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

        if (TryFindAnimationByName($"^{previousClipName}_?A?$", available, out var previousClip))
        {
            clip.PreviousAnimation = previousClip.state.name;
        }
    }

    [Obsolete("Use TryFindAnimationByName instead", false)]
    private ChildAnimatorState FindAnimationByName(string expression, ChildAnimatorState[] allAnimations)
    {
        return allAnimations.FirstOrDefault(x => Regex.IsMatch(x.state.name, expression));
    }


    private bool TryFindAnimationByName(string expression, ChildAnimatorState[] allAnimations,
        out ChildAnimatorState result)
    {
        result = allAnimations.FirstOrDefault(x => Regex.IsMatch(x.state.name, expression));
        return result.state != null;
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
                nextClipName = $"A_{result[action]}_?{result[character]}?_{result[transitionTo]}";
            }

            if (TryFindAnimationByName($"^{nextClipName}_?A?$", allAnimations, out var nextClip) &&
                nextClip.state != null)
            {
                clip.NextAnimations.Add(nextClip.state.name);
            }
        }
        else
        {
            // With nextName means transition to another action (e.g., 01 -> 01-relax_01 -> relax_01)
            var nextClipName = $"A_{result[transitionTo]}";

            if (TryFindAnimationByName($"^{nextClipName}_?A?$", allAnimations, out var nextClip) &&
                nextClip.state != null)
            {
                clip.NextAnimations.Add(nextClip.state.name);
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
        if (TryFindAnimationByName($"^{animationName}$", statesAvailable, out var clip))
        {
            // check if state already has a transition to findAnimationByName with parameter
            if (!state.transitions.ToList().Any(x =>
                    x.destinationState == clip.state &&
                    x.conditions.Any(y => y.parameter == parameter.name)))
            {
                var transition = state.AddTransition(clip.state);
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
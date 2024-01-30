package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"slices"
	"strconv"
	"strings"
)

type Animation struct {
	Name                string
	NextAnimations      []string
	AlternateAnimations []string
	PreviousAnimation   string
}

func main() {
	animations := readFromFolder()

	animations = fetchAnimations(animations)

	bytes, _ := json.Marshal(animations)
	toPrint := string(bytes)
	fmt.Println(toPrint)
}

func readFromFolder() []*Animation {
	var animations []*Animation
	filepath.Walk("animations", func(path string, info os.FileInfo, err error) error {
		if info.IsDir() {
			return nil
		}
		// filename without extension
		filename := strings.TrimSuffix(info.Name(), filepath.Ext(info.Name()))
		animations = append(animations, &Animation{Name: filename})
		return nil
	})
	return animations
}

// re is the regular expression for parsing the animation name.
// The `A` at the beginning is for "Animation".
// name is the name of the animation.
// char is the character name. (optional)
// clip is the clip number.
// alternate is the alternate animation letter. (optional)
// transitionTo is the animation name to transition to. (optional)
// nextName is the next animation name to transition to. (optional)
// nextClip is the next animation clip to transition to. (optional)
var re = regexp.MustCompile(`A_(?P<name>[a-z]+)_(?:(?P<char>[A-Z]?)_?(?P<clip>\d{2}))_?(?P<alternate>[A-Z]?)?-?(?P<transitionTo>(?P<nextName>[a-z]+)?_?(?P<nextClip>\d{2}))?`)

// fetchAnimations returns all the possible next animations.
// The `A` at the beginning is for "Animation".
// An example is `A_intro_01` -> `A_intro_02` -> `A_intro_03`
// Transition animations are when there is another animation name attached to the end.
// An example is `A_intro_01` -> `A_intro_01-02` -> `A_intro_02` (same group)
// This is wrong: `A_intro_01` -> `A_intro_02` when `A_intro_01-02` exists.
// An example is `A_intro_01-relax_01` -> `A_relax_01` (transition to another group)
// Alternate animations are defined when there is a letter after the animation name (A-Z)
// An example is `A_intro_01_A` -> `A_intro_01_B`
// Edge case is sometimes `_A` is not indicated, but `_B` exists, so we need to check for that.
// An example is `A_intro_01` -> `A_intro_01_B`
// Another edge case is the underscore is sometimes not indicated.
// An example is `A_intro_01` -> `A_intro_01B` -> `A_intro_01C`
// There's also a special case such as `A_animation_A_01` and `A_animation_B_01`, which distinguishes from two characters.
// In this case, they are not alternate animations, but two different animations.
func fetchAnimations(animations []*Animation) []*Animation {
	for _, animation := range animations {
		//break
		if animation == nil {
			continue
		}
		animation.getNextAnimation(animations)
		animation.getAlternateAnimation(animations)
	}

	for _, animation := range animations {
		if animation == nil {
			continue
		}
		animation.getPreviousAnimation(animations)
	}

	return animations
}

// getNextAnimation returns the next animation in the sequence.
// If there is no next animation, then it returns nil.
func (clip *Animation) getNextAnimation(allAnimations []*Animation) {
	match := re.FindStringSubmatch(clip.Name)
	if match == nil {
		return
	}

	result := make(map[string]string)
	for i, name := range re.SubexpNames() {
		result[name] = match[i]
	}

	if result["alternate"] != "" {
		// Alternate clips don't have next animations, but use alternate animations instead
		return
	}

	// Check for transition animations first
	if result["transitionTo"] != "" {
		clip.findTransition(allAnimations, result)
		return
	}

	nextClipName := fmt.Sprintf("A_%s_%02d", result["name"], atoi(result["clip"])+1)
	if result["char"] != "" {
		nextClipName = fmt.Sprintf("A_%s_%s_%02d", result["name"], result["char"], atoi(result["clip"])+1)
	}

	// Try searching for clips with transitionTo (e.g., 01 -> 01-02)
	nextClip := findAnimationByName(fmt.Sprintf("^%s-", match[0]), allAnimations)

	if nextClip == nil {
		// Try appending "A" or "_A" to the end (e.g., 01 -> 02, 01 -> 02A, 01 -> 02_A)
		nextClip = findAnimationByName(fmt.Sprintf("^%s_?A?$", nextClipName), allAnimations)
	}

	if nextClip != nil {
		clip.NextAnimations = append(clip.NextAnimations, nextClip.Name)
	}
}

func (clip *Animation) findTransition(allAnimations []*Animation, result map[string]string) {
	// No nextName means transition (e.g., 01-02)
	if result["nextName"] == "" {
		// Transition within the same group but different clip
		nextClipName := fmt.Sprintf("A_%s_%s", result["name"], result["transitionTo"])
		if result["char"] != "" {
			// TODO: Change char regex to also capture the underscore so we can always attempt to concatenate
			nextClipName = fmt.Sprintf("A_%s_%s_%s", result["name"], result["char"], result["transitionTo"])
		}
		nextClip := findAnimationByName(fmt.Sprintf("^%s$", nextClipName), allAnimations)
		if nextClip == nil {
			// Try appending "A" or "_A" to the end
			nextClip = findAnimationByName(fmt.Sprintf("^%s_?A$", nextClipName), allAnimations)
		}
		if nextClip != nil {
			clip.NextAnimations = append(clip.NextAnimations, nextClip.Name)
		}
		return
	}

	// With nextName (e.g., 02-relax_01)
	nextClipName := fmt.Sprintf("A_%s_%s", result["nextName"], result["nextClip"])
	nextClip := findAnimationByName(fmt.Sprintf("^%s$", nextClipName), allAnimations)
	if nextClip == nil {
		// Try appending "A" or "_A" to the end
		nextClip = findAnimationByName(fmt.Sprintf("^%s_?A$", nextClipName), allAnimations)
	}
	if nextClip != nil {
		clip.NextAnimations = append(clip.NextAnimations, nextClip.Name)
	}
	return
}

func findAnimationByName(expression string, allAnimations []*Animation) *Animation {
	reg := regexp.MustCompile(expression)
	for _, anim := range allAnimations {
		if anim == nil {
			continue
		}
		if reg.MatchString(anim.Name) {
			return anim
		}
	}
	return nil
}

func filterAnimations(expression string, allAnimations []*Animation) []*Animation {
	var filtered []*Animation
	reg := regexp.MustCompile(expression)
	for _, anim := range allAnimations {
		if anim == nil {
			continue
		}
		if reg.MatchString(anim.Name) {
			filtered = append(filtered, anim)
		}
	}
	return filtered
}

func atoi(str string) int {
	i, _ := strconv.Atoi(str)
	return i
}

func (clip *Animation) getPreviousAnimation(allAnimations []*Animation) {
	for _, otherClip := range allAnimations {
		if otherClip == nil {
			continue
		}
		if slices.Contains(otherClip.NextAnimations, clip.Name) {
			clip.PreviousAnimation = otherClip.Name
			break
		}
	}
}

func (clip *Animation) getAlternateAnimation(allAnimations []*Animation) {
	match := re.FindStringSubmatch(clip.Name)
	if match == nil {
		return
	}

	result := make(map[string]string)
	for i, name := range re.SubexpNames() {
		result[name] = match[i]
	}

	if result["transitionTo"] != "" {
		// Transition animations don't have alternate animations
		return
	}

	toFind := fmt.Sprintf("A_%s_%s", result["name"], result["clip"])
	if result["char"] != "" {
		toFind = fmt.Sprintf("A_%s_%s_%s", result["name"], result["char"], result["clip"])
	}

	alternates := filterAnimations(fmt.Sprintf("^%s_?[A-Z]?$", toFind), allAnimations)
	for _, alternate := range alternates {
		if alternate == nil {
			continue
		}
		if alternate.Name == clip.Name {
			continue
		}
		clip.AlternateAnimations = append(clip.AlternateAnimations, alternate.Name)
	}
}

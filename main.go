package main

import (
	"log"
	"os"
	"path/filepath"
	"regexp"
	"strings"
)

func main() {
	animations := readFromFolder()
	//for _, animation := range animations {
	//	// log printf with field names
	//	//log.Printf("%+v", animation)
	//	fmt.Println(animation.Name)
	//}

	for _, animation := range animations {

		log.Printf("before: %+v", animation)
		animation.getNextAnimation(animations)
		log.Printf("after: %+v", animation)

		break

	}
}

type Animation struct {
	Name                string
	NextAnimations      []Animation
	AlternateAnimations []Animation
	PreviousAnimation   *Animation
}

func readFromFolder() []Animation {
	var animations []Animation
	filepath.Walk("animations", func(path string, info os.FileInfo, err error) error {
		if info.IsDir() {
			return nil
		}
		// filename without extension
		filename := strings.TrimSuffix(info.Name(), filepath.Ext(info.Name()))
		animations = append(animations, Animation{Name: filename})
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
var re = regexp.MustCompile(`(?m)A_(?P<name>[a-z]+)_(?:(?P<char>[A-Z]?)_?(?P<clip>\d{2}))_?(?P<alternate>[A-Z]?)?-?(?P<transitionTo>(?P<nextName>[a-z]+)?_?(?P<nextClip>\d{2}))?`)

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
func fetchAnimations(animations []Animation) []Animation {
	for i, clip := range animations {
		// Parse the current animation name
		match := re.FindStringSubmatch(clip.Name)
		if match == nil {
			continue
		}
		result := make(map[string]string)
		for i, name := range re.SubexpNames() {
			if i != 0 && name != "" {
				result[name] = match[i]
			}
		}

		// Check for alternate and next animations
		clip.getNextAnimation(animations)
		clip.getAlternateAnimation(animations)

		// Update the map
		animations[i] = clip
	}
	return animations
}

func (clip *Animation) getNextAnimation(allAnimations []Animation) {
	match := re.FindStringSubmatch(clip.Name)
	if match == nil {
		return
	}

	result := make(map[string]string)
	for i, name := range re.SubexpNames() {
		if i != 0 && name != "" {
			result[name] = match[i]
		}
	}

	// Check for next animations
	for _, otherClip := range allAnimations {
		if otherClip.Name == clip.Name {
			continue
		}

		otherMatch := re.FindStringSubmatch(otherClip.Name)
		if otherMatch == nil {
			continue
		}
		otherResult := make(map[string]string)
		for i, name := range re.SubexpNames() {
			if i != 0 && name != "" {
				otherResult[name] = otherMatch[i]
			}
		}

		if otherResult["name"] != result["name"] {
			continue
		}

		if otherResult["char"] != result["char"] {
			continue
		}

		if otherResult["clip"] <= result["clip"] {
			continue
		}

		clip.NextAnimations = append(clip.NextAnimations, otherClip)
	}
}

func (clip *Animation) getAlternateAnimation(allAnimations []Animation) {
	match := re.FindStringSubmatch(clip.Name)
	if match == nil {
		return
	}

	result := make(map[string]string)
	for i, name := range re.SubexpNames() {
		if i != 0 && name != "" {
			result[name] = match[i]
		}
	}

	// Check for alternate animations
	for _, otherClip := range allAnimations {
		if otherClip.Name == clip.Name {
			continue
		}

		otherMatch := re.FindStringSubmatch(otherClip.Name)
		if otherMatch == nil {
			continue
		}
		otherResult := make(map[string]string)
		for i, name := range re.SubexpNames() {
			if i != 0 && name != "" {
				otherResult[name] = otherMatch[i]
			}
		}

		if otherResult["name"] != result["name"] {
			continue
		}

		if otherResult["char"] != result["char"] {
			continue
		}

		if otherResult["clip"] != result["clip"] {
			continue
		}

		if otherResult["alternate"] == "" {
			continue
		}

		if otherResult["alternate"] == result["alternate"] {
			continue
		}

		clip.AlternateAnimations = append(clip.AlternateAnimations, otherClip)
	}
}

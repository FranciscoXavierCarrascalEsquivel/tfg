import re
import os

scene_path = r'c:\Users\Kiko\TreballFiGrau\tfg\Assets\Scenes\Zona_Test.unity'

# Mapping text fragments to their respective sprites
# Using the same logic as the C# script
mapping = [
    # WORRIED
    (r"to be remembered. or to be erased", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"it deletes. no mercy", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"something has already been taken", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"started to lose himself", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"when a world becomes too unstable", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"it does not fully know what it wants to be. that makes it dangerous", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"don't take too long. this place doesn't like waiting", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"by something waiting beyond the maze", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"your life reaches zero", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"you end. and worse than that", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"attacking also has consequences", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"be careful. some enemies will let you escape. others will not", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"if you run too much", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"or be erased", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"erased by oblivion", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"the intelligence fears", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),
    (r"connection is dangerous", "-2827816166978458693", "4f8a9a3c3a5cada418f097e504611556"),

    # THINKING
    (r"then we'll start there", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"then we\u2019ll start there", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"that is how you continue. escaping", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"maybe. or maybe you have to understand it", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"you won't. not at first", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"you won\u2019t. not at first", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"not every door opens the same future", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"some fights are not cages. they are mirrors", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"you can run from the enemy. but not from what the fight means", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"maybe. but not by standing here", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),
    (r"somewhere between one forgotten world and the next", "9163481190851558627", "705d4c3deb569f74ca439808de54607a"),

    # EXPLAINING2
    (r"they don't vanish immediately", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"they don\u2019t vanish immediately", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"there is still a way to move between forgotten places", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"pdfs. txt documents", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"sometimes, inside those files", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"the next route is not here. it is protected", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"first, you must cross the maze", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"survive the enemies inside. cross it", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"then face the guardian beyond it", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"after the maze, you will find the one guarding", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"inside it, there is an email address", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"without it, you cannot move forward", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"items can heal you. protect you", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"do not waste them", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"when projectiles come toward you", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"if your timing is good, you survive", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"so listen carefully. go east from here", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),
    (r"when combat begins, you will always have four possibilities", "-5092042947339083329", "a6111c0fd186f2c4bbd5d92d8cbeab25"),

    # EXPLAINING
    (r"your name is not just a word", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"it connects you to who you were", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"this place is not a normal world", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"abandoned games. forgotten apps", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"there is something above this place", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"a system. an intelligence created to clean", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"in your world, an email is a message", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"a connection to another place", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"if you find one, you can follow it", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"the maze is made of pieces from different", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"corridors from games. menus from apps", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"inside, you will find enemies. not monsters", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"small broken things that still try", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"when they see you, they may drag you", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"reason is different. you try to understand", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"you talk. you observe. you look for", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"if you reason well, you may end the fight", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"sometimes. but reasoning is not magic", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"use an item means relying on what you have", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"and then there is run", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"sometimes the smartest way to win", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"these four choices will follow you everywhere", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"if you pay attention", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"after you choose one of those four actions", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"yes. they will attack you. usually with projectiles", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"fragments of code. broken icons", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"look at your hands. the rings", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"people rarely notice the tools keeping them alive", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"your life is limited. every hit", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"your choices matter. not because the world is fair", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"not a forgotten person. not a lost soul", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"a disciple of the intelligence", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"yes. a lesser program. a servant", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),
    (r"it was made to control routes", "6997138732800248584", "1a2cce9cd3aa9a54cb878231276e170e"),

    # NEUTRAL
    (r"so...you got here", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"that depends", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"my name is ravel", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"small survivor with excellent posture", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"but yes. a rat", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"so... what do you want to know first", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"some call it an artificial intelligence. i call it", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"big enough that people stop asking", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"now you know what to look for", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"yes. and listen carefully now", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"attack. reason. use an item. run", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"that is all. four choices", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"attack is the most direct option", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"sometimes that will be necessary", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"remember your four choices", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"i never said i was good at helping", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"now go. before the maze notices", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"good. and one last thing", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"if something in the maze looks harmless", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"if something looks dangerous", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"you again. need to know", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
    (r"then choose again. that is all any of us can do", "1043314311609083480", "cfa73e5b5ca81cb4689544fd3559c494"),
]

# Default sprite for Ravel (Neutral)
default_file_id = "1043314311609083480"
default_guid = "cfa73e5b5ca81cb4689544fd3559c494"

with open(scene_path, 'r', encoding='utf-8') as f:
    content = f.readlines()

# We need to find the Interactable component for Rata
# GameObject Rata has ID 1361949894
# Component Interactable is ID 1361949897

start_line = -1
end_line = -1
for i, line in enumerate(content):
    if "--- !u!114 &1361949897" in line:
        start_line = i
    elif start_line != -1 and line.startswith("---"):
        end_line = i
        break

if start_line == -1:
    print("Could not find the Interactable component in the scene.")
    exit(1)

component_content = content[start_line:end_line]

new_component_content = []
current_text = ""
speaker_name = ""

# Process the component lines
# We need to buffer the text and speaker name to decide which portrait to use when we see a 'portrait' line
for i in range(len(component_content)):
    line = component_content[i]
    
    if "text:" in line:
        # Some texts are multiline in YAML
        current_text = line.split("text:")[1].strip().strip('"')
        # Check next lines if they are part of the text
        j = i + 1
        while j < len(component_content) and not any(k in component_content[j] for k in ["portrait:", "speakerName:", "isRightSide:", "showOnTop:"]):
             current_text += " " + component_content[j].strip()
             j += 1
    
    if "speakerName:" in line:
        speaker_name = line.split("speakerName:")[1].strip()

    if "portrait:" in line and (speaker_name == "Ravel" or speaker_name == "???"):
        found = False
        lower_text = current_text.lower()
        for pattern, fid, guid in mapping:
            # Simple match for text fragments
            # Using raw strings for patterns to avoid issues with escape chars
            if pattern.lower() in lower_text:
                line = re.sub(r'fileID: -?\d+', f'fileID: {fid}', line)
                line = re.sub(r'guid: [a-f0-9]+', f'guid: {guid}', line)
                found = True
                break
        
        if not found:
            # Fallback to neutral for Ravel
            line = re.sub(r'fileID: -?\d+', f'fileID: {default_file_id}', line)
            line = re.sub(r'guid: [a-f0-9]+', f'guid: {default_guid}', line)
            
    new_component_content.append(line)

content[start_line:end_line] = new_component_content

with open(scene_path, 'w', encoding='utf-8') as f:
    f.writelines(content)

print("Successfully updated Rata portraits in the scene.")

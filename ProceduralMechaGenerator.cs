using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public class ProceduralMechaGenerator : MonoBehaviour {
    public MechaConfiguration config;

    [HideInInspector]
    public List<string> possibleArms = new List<string>();
    [HideInInspector]
    public List<string> possibleLegs = new List<string>();
    [HideInInspector]
    public List<string> possibleAIChips = new List<string>();

    public void GenerateMecha() {
        // Procedural logic here
    }
}

[CustomEditor(typeof(ProceduralMechaGenerator))]
public class ProceduralMechaGeneratorEditor : Editor {
    public override void OnInspectorGUI() {
        ProceduralMechaGenerator generator = (ProceduralMechaGenerator)target;

        generator.config = (MechaConfiguration)EditorGUILayout.ObjectField("Mecha Configuration", generator.config, typeof(MechaConfiguration), false);

        generator.possibleArms = EditorGUILayout.ArrayField("Possible Arms", generator.possibleArms);
        generator.possibleLegs = EditorGUILayout.ArrayField("Possible Legs", generator.possibleLegs);
        generator.possibleAIChips = EditorGUILayout.ArrayField("Possible AI Chips", generator.possibleAIChips);

        if (GUILayout.Button("Generate Mecha")) {
            generator.GenerateMecha();
        }
    }
}
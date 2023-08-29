using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MechaGenerator))]
public class MechaGeneratorEditor : Editor {
    public override void OnInspectorGUI() {
        MechaGenerator generator = (MechaGenerator)target;

        // Display fields to populate MechaConfiguration
        generator.config.armsType = EditorGUILayout.TextField("Arms Type", generator.config.armsType);
        generator.config.numberOfArms = EditorGUILayout.IntField("Number of Arms", generator.config.numberOfArms);
        generator.config.legsType = EditorGUILayout.TextField("Legs Type", generator.config.legsType);
        generator.config.numberOfLegs = EditorGUILayout.IntField("Number of Legs", generator.config.numberOfLegs);
        generator.config.bodyType = EditorGUILayout.TextField("Body Type", generator.config.bodyType);
        generator.config.headpieceType = EditorGUILayout.TextField("Headpiece Type", generator.config.headpieceType);

        // Display list of part sets
        EditorGUILayout.LabelField("Part Sets");
        for (int i = 0; i < generator.config.partSets.Count; i++) {
            generator.config.partSets[i] = EditorGUILayout.TextField(generator.config.partSets[i]);
        }

        if (GUILayout.Button("Generate Mecha")) {
            generator.GenerateMecha();
        }
    }
}
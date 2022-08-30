using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEngine;

[CustomPropertyDrawer(typeof(AxisConstraints))]
public class AxisConstraintsDrawer : PropertyDrawer
{
    //CREDIT: Stack Overflow user Adam Roszyk (https://stackoverflow.com/questions/39081613/align-variables-horizontally-in-unity-inspector)

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw label
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Calculate rects
        var XRect = new Rect(position.x, position.y, 0, position.height);
        var YRect = new Rect(position.x+35, position.y, 0, position.height);
        var ZRect = new Rect(position.x+70, position.y, 0, position.height);

        // Draw fields - passs GUIContent.none to each so they are drawn without labels
        EditorGUIUtility.labelWidth = 15;
        EditorGUI.PropertyField(XRect, property.FindPropertyRelative("X"), new GUIContent("X"));
        EditorGUI.PropertyField(YRect, property.FindPropertyRelative("Y"), new GUIContent("Y"));
        EditorGUI.PropertyField(ZRect, property.FindPropertyRelative("Z"), new GUIContent("Z"));
        EditorGUI.EndProperty();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Window that composites the different functionalities into one
/// </summary>
public class AvatarMeshOptimizerWindow : EditorWindow
{
    VRCAvatarOptimizerWindowPart vrcAvatarOptimizerWindowPart;

    // General
    private Vector2 scrollPos = Vector2.zero;

    [MenuItem("Tools/Search Unused Blendshapes")]
    static void CreateNewWindow()
    {
        EditorWindow.GetWindow<AvatarMeshOptimizerWindow>();
    }

    public AvatarMeshOptimizerWindow() {
        vrcAvatarOptimizerWindowPart = new VRCAvatarOptimizerWindowPart();
    }

    void OnGUI()
    {
        scrollPos = GUILayout.BeginScrollView(scrollPos, GUIStyle.none);

        vrcAvatarOptimizerWindowPart.OnGUI();
        
        GUILayout.EndScrollView();
    }
}

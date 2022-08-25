using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using VRCAvatarDescriptor = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using static BlendshapeAnalyzer;

public class VRCAvatarOptimizerWindowPart
{
    public BlendshapeAnalyzer blendshapeAnalyzer;

    public VRCAvatarOptimizerWindowPart() {
        blendshapeAnalyzer = new BlendshapeAnalyzer();
    }

    private VRCAvatarDescriptor avatarDescriptor;
    private AvatarBlendshapes avatarBlendshapes;
    private string crudeTextLog = "";
    private bool showCrudeTextLog;
    private SkinnedMeshRenderer skinnedMeshRenderer;
    MeshPartSelectionWindowPart meshPartSelection = new MeshPartSelectionWindowPart();

    public void OnGUI()
    {
        avatarDescriptor = ((VRCAvatarDescriptor)EditorGUILayout.ObjectField(avatarDescriptor, typeof(VRCAvatarDescriptor), true));

        if (GUILayout.Button("Analyze Blendshape Usage") && avatarDescriptor != null)
        {
            avatarBlendshapes = BlendshapeAnalyzer.analyzeVRCAvatarBlendshapes(avatarDescriptor, ref crudeTextLog);
        }

        if (avatarBlendshapes?.meshes != null)
        {
            foreach (KeyValuePair<string, MeshBlendshapes> kvp in avatarBlendshapes.meshes)
            {
                kvp.Value.show = EditorGUILayout.Foldout(kvp.Value.show, kvp.Key);
                if (kvp.Value.show)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.ObjectField(kvp.Value.skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);
                    string used = "";
                    string unused = "";
                    foreach (KeyValuePair<string, BlendshapeUsage> kvp2 in kvp.Value.blendshapes)
                    {
                        if (kvp2.Value.inUse)
                        {
                            used += kvp2.Value.name + "\n";
                        }
                        else
                        {
                            unused += kvp2.Value.name + "\n";
                        }
                    }
                    EditorGUILayout.LabelField("Blendshapes in use by Animations or Avatarsettings");
                    EditorGUILayout.TextArea(used.TrimEnd('\r', '\n'));
                    EditorGUILayout.LabelField("Unused:");
                    EditorGUILayout.TextArea(unused.TrimEnd('\r', '\n'));
                    if (GUILayout.Button("Delete unused Blendshapes"))
                    {
                        Undo.RecordObject(kvp.Value.skinnedMeshRenderer, "Optimized Blendshapes");

                        Mesh filteredMesh = blendshapeAnalyzer.filterBlendshapes(kvp.Value.skinnedMeshRenderer.sharedMesh, kvp.Value.toHashSet(inUse: true));
                        
                        System.DateTime foo = System.DateTime.Now;
                        long unixTime = ((System.DateTimeOffset)foo).ToUnixTimeSeconds();
                        if (!AssetDatabase.IsValidFolder("Assets/OptimizedMeshes"))
                        {
                            AssetDatabase.CreateFolder("Assets", "OptimizedMeshes");
                        }

                        AssetDatabase.CreateAsset(filteredMesh, "Assets/OptimizedMeshes/" + kvp.Key + $"{unixTime}.mesh");
                        kvp.Value.skinnedMeshRenderer.sharedMesh = filteredMesh;
                    }
                    displayMeshDetails(kvp.Value?.skinnedMeshRenderer?.sharedMesh);

                    EditorGUI.indentLevel--;
                }
            }
        }

        EditorGUILayout.Space();

        EditorGUILayout.TextArea(crudeTextLog.TrimEnd('\r', '\n'));

        EditorGUILayout.Space();


        showCrudeTextLog = EditorGUILayout.Foldout(showCrudeTextLog, "Text Log:");
        if (showCrudeTextLog)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.TextArea(crudeTextLog.TrimEnd('\r', '\n'));
            EditorGUI.indentLevel--;
        }

        skinnedMeshRenderer = ((SkinnedMeshRenderer)EditorGUILayout.ObjectField(skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true));
        if (skinnedMeshRenderer?.sharedMesh != null)
        {
            EditorGUI.indentLevel++;
            displayMeshDetails(skinnedMeshRenderer.sharedMesh);
            HashSet<string> selection = meshPartSelection.DisplaySelection();

            if (GUILayout.Button("Delete Mesh Parts"))
            {
                Undo.RecordObject(skinnedMeshRenderer, "Filter mesh parts");
                Mesh newMesh = BlendshapeAnalyzer.deleteMeshStreams(skinnedMeshRenderer.sharedMesh, selection);
                skinnedMeshRenderer.sharedMesh = newMesh;
            }
            EditorGUI.indentLevel--;
        }
    }

    public static void displayMeshDetails(Mesh mesh)
    {
        if (mesh == null) return;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("indexFormat");
        EditorGUILayout.TextField(mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16 ? "UInt16" : "UInt32");
        EditorGUILayout.EndHorizontal();
        string attributeText = "";
        foreach (UnityEngine.Rendering.VertexAttributeDescriptor desc in mesh.GetVertexAttributes())
        {
            attributeText += $"{desc.attribute}\t{desc.dimension}\t{desc.format}\t{desc.stream}\n";
        }
        EditorGUILayout.LabelField("VertexAttributes (Attribute, Dimension, Format, Stream#)");
        EditorGUI.indentLevel++;
        EditorGUILayout.TextArea(attributeText.TrimEnd('\r', '\n'));
        EditorGUI.indentLevel--;
    }
}

#if UNITY_EDITOR

// Please go their git and read how to import UnityMeshSimplifer
// requires https://github.com/Whinarn/UnityMeshSimplifier.git

using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections;
using System.Collections.Generic;

using VRCAvatarDescriptor = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using UnityMeshSimplifier;

public class BlendshapeAnalyzer : EditorWindow
{

    private Animator avatarAnimator;
    private AnimatorController avatarAnimatorController;
    private VRCAvatarDescriptor avatarDescriptor;
    
    private Vector2 scrollPos = Vector2.zero;

    // Mesh Name -> Set of Blendshapes
    private Dictionary<string, HashSet<string>> knownBlendshapes;

    private string text = "Hello World";
    private string text2 = "Hello World";

    [MenuItem("Tools/Search Unused Blendshapes")]
    static void CreateNewWindow()
    {
        EditorWindow.GetWindow<BlendshapeAnalyzer>();
    }


    public BlendshapeAnalyzer()
    {
    }


    void OnGUI()
    {
        scrollPos = GUILayout.BeginScrollView(scrollPos, GUIStyle.none);

        avatarAnimatorController = ((AnimatorController)EditorGUILayout.ObjectField(avatarAnimatorController,
            typeof(AnimatorController),
            true));

        avatarDescriptor = ((VRCAvatarDescriptor)EditorGUILayout.ObjectField(avatarDescriptor, typeof(VRCAvatarDescriptor), true));



        if (GUILayout.Button("List Animations"))
        {
            knownBlendshapes = new Dictionary<string, HashSet<string>>();
            text = "";
            text2 = "";

            // Get blendshapes via the AvatarDescriptor 
            if (avatarDescriptor != null)
            {
                // Get Visemes if defined
                if (avatarDescriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                    && avatarDescriptor.VisemeSkinnedMesh != null)
                {
                    if (!knownBlendshapes.ContainsKey(avatarDescriptor.VisemeSkinnedMesh.name))
                    {
                        knownBlendshapes[avatarDescriptor.VisemeSkinnedMesh.name] = new HashSet<string>();
                    }
                    foreach (string s in avatarDescriptor.VisemeBlendShapes)
                    {
                        if (s != null && s != "")
                        {
                            knownBlendshapes[avatarDescriptor.VisemeSkinnedMesh.name].Add(s);
                        }
                    }
                    GUILayout.Label(avatarDescriptor.VisemeSkinnedMesh.name);
                    GUILayout.Label(string.Join("\n", avatarDescriptor.VisemeBlendShapes));
                }

                // Get Eyelids if defined
                if (avatarDescriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && avatarDescriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
                {
                    SkinnedMeshRenderer smr = avatarDescriptor.customEyeLookSettings.eyelidsSkinnedMesh;
                    if (smr.sharedMesh != null)
                    {
                        int blendShapeCount = smr.sharedMesh.blendShapeCount;
                        foreach (int i in avatarDescriptor.customEyeLookSettings.eyelidsBlendshapes)
                        {
                            if (i == -1) break;
                            knownBlendshapes[avatarDescriptor.customEyeLookSettings.eyelidsSkinnedMesh.name]
                                .Add(smr.sharedMesh.GetBlendShapeName(i));
                        }
                    }
                }

                // FX Animator COntroller
                foreach (VRCAvatarDescriptor.CustomAnimLayer cal in avatarDescriptor.baseAnimationLayers)
                {
                    if (cal.animatorController != null)
                        extractRuntimeAnimatorControllerBlendshapes(cal.animatorController);
                }

                foreach (VRCAvatarDescriptor.CustomAnimLayer cal in avatarDescriptor.specialAnimationLayers)
                {
                    if (cal.animatorController != null)
                        extractRuntimeAnimatorControllerBlendshapes(cal.animatorController);
                }

                GUILayout.Label(string.Join("", new List<int>(avatarDescriptor.customEyeLookSettings.eyelidsBlendshapes).ConvertAll(i => i.ToString()).ToArray()));
                GUILayout.Label(avatarDescriptor.baseAnimationLayers[0].animatorController.name);
                foreach (VRCAvatarDescriptor.CustomAnimLayer l in avatarDescriptor.baseAnimationLayers)
                {
                    GUILayout.Label(UnityEditor.AssetDatabase.GetAssetPath(l.animatorController));
                }
            }




            foreach (KeyValuePair<string, HashSet<string>> kvp in knownBlendshapes)
            {
                text2 += "### " + kvp.Key + " ###\n";

                GameObject go = GameObject.Find(kvp.Key);
                HashSet<string> modelBlendShapes = new HashSet<string>();
                SkinnedMeshRenderer smr = null;
                Mesh newMesh = null;

                if (go != null)
                {
                    smr = go.GetComponent<SkinnedMeshRenderer>();
                    newMesh = createMeshCopy(smr.sharedMesh);


                    for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                    {

                        modelBlendShapes.Add(smr.sharedMesh.GetBlendShapeName(i));
                        if (smr.GetBlendShapeWeight(i) >= 0.005)
                        {
                            knownBlendshapes[kvp.Key].Add(smr.sharedMesh.GetBlendShapeName(i));
                        }
                    }
                }
                else
                {
                    text2 += "Object " + kvp.Key + " not found! ???\n";
                }

                foreach (string s in kvp.Value)
                {
                    text2 += s + "\n";
                }

                if (go != null)
                {
                    text2 += "### Unused: ###\n";

                    HashSet<string> unusedBlendshapes = new HashSet<string>(modelBlendShapes);
                    unusedBlendshapes.ExceptWith(kvp.Value);

                    foreach (string s in unusedBlendshapes)
                    {
                        text2 += s + "\n";
                    }

                    if (newMesh != null)
                    {
                        MeshSimplifier ms = new MeshSimplifier(newMesh);
                        BlendShape[] bs = ms.GetAllBlendShapes();
                        ms.ClearBlendShapes();
                        foreach (BlendShape b in bs)
                        {
                            if (!unusedBlendshapes.Contains(b.ShapeName))
                            {
                                ms.AddBlendShape(b);
                            }
                        }
                        Mesh filteredMesh = ms.ToMesh();


                        AssetDatabase.CreateAsset(filteredMesh, AssetDatabase.GetAssetPath(newMesh.GetInstanceID()));
                        if (smr != null)
                        {
                            smr.sharedMesh = filteredMesh;
                        }

                    }


                }
                text2 += "\n\n\n";
            }
        }

        EditorGUILayout.TextArea(text2);
        EditorGUILayout.TextArea(text);

        GUILayout.EndScrollView();
    }

    private void extractAnimatorControllerBlendshapes(AnimatorController avatarAnimatorController)
    {
        if (avatarAnimatorController == null)
        {
            return;
        }
        else
        {
            foreach (AnimatorControllerLayer l in avatarAnimatorController.layers)
            {
                foreach (ChildAnimatorState s in l.stateMachine.states)
                {
                    if (s.state.motion is null) break;

                    text += s.state.motion.name + "\n";

                    extractMotion(s.state.motion);
                }
            }
        }
    }

    private Mesh createMeshCopy(Mesh originalMesh)
    {

        Mesh newMesh = null;
        string path = AssetDatabase.GetAssetPath(originalMesh.GetInstanceID());

        //Create Meshes
        Object[] objects = AssetDatabase.LoadAllAssetsAtPath(path);

        for (int i = 0; i < objects.Length; i++)
        {
            Debug.Log($"Checking {objects[i].name} from asset at {path}");
            if (objects[i] is Mesh && objects[i].name == originalMesh.name)
            {
                Debug.Log($"Found match: {objects[i].name} from asset at {path}");
                Mesh mesh = Object.Instantiate(objects[i]) as Mesh;

                System.DateTime foo = System.DateTime.Now;
                long unixTime = ((System.DateTimeOffset)foo).ToUnixTimeSeconds();

                //TODO: Assure the folder exists beforehand
                AssetDatabase.CreateAsset(mesh, "Assets/OptimizedMeshes/" + objects[i].name + $"{unixTime}.mesh");
                newMesh = mesh;

                Debug.Log($"Path of newMesh: {AssetDatabase.GetAssetPath(mesh.GetInstanceID())}");

            }
        }

        return newMesh;
    }

    private void extractRuntimeAnimatorControllerBlendshapes(RuntimeAnimatorController avatarAnimatorController)
    {
        if (avatarAnimatorController == null)
        {
            return;
        }
        else
        {
            foreach (AnimationClip clip in avatarAnimatorController.animationClips)
            {
                extractAnimationClip(clip);
            }
        }
    }

    private void extractMotion(Motion motion)
    {
        if (motion is AnimationClip clip)
        {
            extractAnimationClip(clip);
        }
        else if (motion is BlendTree blendTree)
        {
            extractBlendTree(blendTree);
        }
    }

    private void extractAnimationClip(AnimationClip clip)
    {
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName.StartsWith("blendShape"))
            {

                if (!knownBlendshapes.ContainsKey(b.path))
                {
                    knownBlendshapes[b.path] = new HashSet<string>();
                }
                knownBlendshapes[b.path].Add(b.propertyName.Substring(11));
            }
        }
    }

    private void extractBlendTree(BlendTree blendTree)
    {
        foreach (ChildMotion cm in blendTree.children)
        {
            if (cm.motion is null) break;
            extractMotion(cm.motion);
        }
    }
}

#endif

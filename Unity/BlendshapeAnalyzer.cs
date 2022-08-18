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
    class AvatarBlendshapes {
        public Dictionary<string, MeshBlendshapes> meshes;

        public AvatarBlendshapes() {
            meshes = new Dictionary<string, MeshBlendshapes>();
        }
    }

    class MeshBlendshapes {
        public bool show;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public string meshName;
        public Dictionary<string, BlendshapeUsage> blendshapes;

        public MeshBlendshapes() {
            blendshapes = new Dictionary<string, BlendshapeUsage>();
            show = false;
            meshName = "";
            skinnedMeshRenderer = null;
        }
    }

    class BlendshapeUsage {
        public string name;
        public bool inUse;
        public BlendshapeUsage(string name, bool inUse) {
            this.name = name;
            this.inUse = inUse;
        }
    }

    class VertexAttributeChange {
        public UnityEngine.Rendering.VertexAttributeDescriptor vertAttrDesc;
        enum ChangeDecision { NO_CHANGE, REMOVE, CONVERT_TO_NORM8, CONVERT_TO_F32 };
        ChangeDecision changeDecision;
        public VertexAttributeChange(UnityEngine.Rendering.VertexAttributeDescriptor vertAttrDesc) {
            this.vertAttrDesc = vertAttrDesc;
        }
    }

    private AvatarBlendshapes avatarBlendshapes;

    private Animator avatarAnimator;
    private AnimatorController avatarAnimatorController;
    private VRCAvatarDescriptor avatarDescriptor;
    
    private Vector2 scrollPos = Vector2.zero;

    // Mesh Name -> Set of Blendshapes
    private Dictionary<string, HashSet<string>> knownBlendshapes;

    private string crudeTextLog = "Hello World";

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



        if (GUILayout.Button("Analyze Blendshape Usage") && avatarDescriptor != null)
        {
            knownBlendshapes = new Dictionary<string, HashSet<string>>();

            // Get blendshapes via the AvatarDescriptor 
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

            //TODO: Scan for Animators in the Hierarchy below the Avatar Component

            //No more searching for new blendshapes
            // For every known set of blendshapes, search for the origin skinned mesh renderer
            // and calcualte the difference set.
            avatarBlendshapes = new AvatarBlendshapes();
            crudeTextLog = "";
            foreach (string key in knownBlendshapes.Keys)
            {
                avatarBlendshapes.meshes.Add(key, new MeshBlendshapes());
                avatarBlendshapes.meshes[key].meshName = key;
                crudeTextLog += "### " + key + " ###\n";

                GameObject go = GameObject.Find(key);
                HashSet<string> modelBlendShapes = new HashSet<string>();
                SkinnedMeshRenderer smr = null;

                if (go != null)
                {
                    smr = go.GetComponent<SkinnedMeshRenderer>();
                    avatarBlendshapes.meshes[key].skinnedMeshRenderer = smr;
                    for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                    {
                        modelBlendShapes.Add(smr.sharedMesh.GetBlendShapeName(i));
                        // Blendshapes that are in use for customization
                        if (smr.GetBlendShapeWeight(i) >= 0.005)
                        {
                            knownBlendshapes[key].Add(smr.sharedMesh.GetBlendShapeName(i));
                        }
                    }
                }
                else
                {
                    crudeTextLog += "Object " + key + " not found! ???\n";
                }

                foreach (string s in knownBlendshapes[key])
                {
                    avatarBlendshapes.meshes[key].blendshapes.Add(s, new BlendshapeUsage(s, true));
                    crudeTextLog += s + "\n";
                }

                if (go != null)
                {
                    crudeTextLog += "### Unused: ###\n";

                    HashSet<string> unusedBlendshapes = new HashSet<string>(modelBlendShapes);
                    unusedBlendshapes.ExceptWith(knownBlendshapes[key]);

                    foreach (string s in unusedBlendshapes)
                    {
                        avatarBlendshapes.meshes[key].blendshapes.Add(s, new BlendshapeUsage(s, false));
                        crudeTextLog += s + "\n";
                    }
                }
                crudeTextLog += "\n\n\n";
            }
        }

        foreach(KeyValuePair<string, MeshBlendshapes> kvp in avatarBlendshapes.meshes) {
            kvp.Value.show = EditorGUILayout.Foldout(kvp.Value.show, kvp.Key);
            if(kvp.Value.show) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space(1);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.ObjectField(kvp.Value.skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);
                string used = "";
                string unused = "";
                foreach(KeyValuePair<string, BlendshapeUsage> kvp2 in kvp.Value.blendshapes) {
                    if(kvp2.Value.inUse) {
                        used += kvp2.Value.name + "\n";
                    } else {
                        unused += kvp2.Value.name + "\n";
                    }
                }
                EditorGUILayout.LabelField("Blendshapes in use by Animations or Avatarsettings");
                EditorGUILayout.TextArea(used);
                EditorGUILayout.LabelField("Unused:");
                EditorGUILayout.TextArea(unused);
                if (GUILayout.Button("Delete unused Blendshapes"))
                {
                    Undo.RecordObject(kvp.Value.skinnedMeshRenderer, "Optimized Blendshapes");
                    MeshSimplifier ms = new MeshSimplifier(kvp.Value.skinnedMeshRenderer.sharedMesh);
                    /*BlendShape[] bs = ms.GetAllBlendShapes();
                    ms.ClearBlendShapes();

                    foreach (BlendShape b in bs)
                    {
                        if (kvp.Value.blendshapes[b.ShapeName].inUse)
                        {
                            ms.AddBlendShape(b);
                        }
                    }*/
                    Mesh filteredMesh = ms.ToMesh();
                    filteredMesh.Optimize();
                    System.DateTime foo = System.DateTime.Now;
                    long unixTime = ((System.DateTimeOffset)foo).ToUnixTimeSeconds();
                    if (!AssetDatabase.IsValidFolder("Assets/OptimizedMeshes"))
                    {
                        AssetDatabase.CreateFolder("Assets", "OptimizedMeshes");
                    }

                    AssetDatabase.CreateAsset(filteredMesh, "Assets/OptimizedMeshes/" + kvp.Key + $"{unixTime}.mesh");
                    kvp.Value.skinnedMeshRenderer.sharedMesh = filteredMesh;
                }

                EditorGUILayout.TextField(kvp.Value.skinnedMeshRenderer.sharedMesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16 ? "UInt16" : "UInt32" );
                Mesh m = kvp.Value.skinnedMeshRenderer.sharedMesh;
                string attributeText = "";
                foreach(UnityEngine.Rendering.VertexAttributeDescriptor desc in m.GetVertexAttributes()) {
                    attributeText += $"{desc.attribute} {desc.dimension} {desc.format} {desc.stream}\n";
                }
                EditorGUILayout.TextArea(attributeText);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space();

        EditorGUILayout.TextArea(crudeTextLog);
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

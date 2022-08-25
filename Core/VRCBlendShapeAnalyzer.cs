#if UNITY_EDITOR

// Please go their git and read how to import UnityMeshSimplifer
// requires https://github.com/Whinarn/UnityMeshSimplifier.git

using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

using VRCAvatarDescriptor = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using UnityMeshSimplifier;

public class BlendshapeAnalyzer : MonoBehaviour
{
    public class AvatarBlendshapes
    {
        public Dictionary<string, MeshBlendshapes> meshes;

        public AvatarBlendshapes()
        {
            meshes = new Dictionary<string, MeshBlendshapes>();
        }
    }

    public class MeshBlendshapes
    {
        public bool show;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public string meshName;
        public Dictionary<string, BlendshapeUsage> blendshapes;

        public MeshBlendshapes()
        {
            blendshapes = new Dictionary<string, BlendshapeUsage>();
            show = false;
            meshName = "";
            skinnedMeshRenderer = null;
        }

        public HashSet<string> toHashSet(bool inUse)
        {
            HashSet<string> hashList = new HashSet<string>();
            foreach (BlendshapeUsage bsu in blendshapes.Values) {
                if (bsu.inUse == inUse) hashList.Add(bsu.name);
            }
            return hashList;
        }
    }

    public class BlendshapeUsage
    {
        public string name;
        public bool inUse;
        public BlendshapeUsage(string name, bool inUse)
        {
            this.name = name;
            this.inUse = inUse;
        }
    }

    class VertexAttributeChange
    {
        public UnityEngine.Rendering.VertexAttributeDescriptor vertAttrDesc;
        enum ChangeDecision { NO_CHANGE, REMOVE, CONVERT_TO_NORM8, CONVERT_TO_F32 };
        ChangeDecision changeDecision;
        public VertexAttributeChange(UnityEngine.Rendering.VertexAttributeDescriptor vertAttrDesc)
        {
            this.vertAttrDesc = vertAttrDesc;
        }
    }


    /*// Blendshapes
    private AvatarBlendshapes avatarBlendshapes;
    private VRCAvatarDescriptor avatarDescriptor;
    // Mesh Name -> Set of Blendshapes
    private Dictionary<string, HashSet<string>> knownBlendshapes; */


    public BlendshapeAnalyzer()
    {
    }

    public static AvatarBlendshapes analyzeVRCAvatarBlendshapes(VRCAvatarDescriptor avatarDescriptor, ref string crudeTextLog) {
        Dictionary<string, HashSet<string>> knownBlendshapes = new Dictionary<string, HashSet<string>>();

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
                    // The eye blendshape settings can be out of range
                    if (i == -1 || i >= smr.sharedMesh.blendShapeCount) break;
                    knownBlendshapes[avatarDescriptor.customEyeLookSettings.eyelidsSkinnedMesh.name]
                        .Add(smr.sharedMesh.GetBlendShapeName(i));
                }
            }
        }

        foreach (VRCAvatarDescriptor.CustomAnimLayer cal in
            avatarDescriptor.baseAnimationLayers.Concat(avatarDescriptor.specialAnimationLayers))
        {
            if (cal.animatorController != null)
                extractRuntimeAnimatorControllerBlendshapes(cal.animatorController, ref knownBlendshapes);
        }

        //TODO: Scan for Animators in the Hierarchy below the Avatar Component

        //No more searching for new blendshapes
        // For every known set of blendshapes, search for the origin skinned mesh renderer
        // and calcualte the difference set.
        AvatarBlendshapes avatarBlendshapes = new AvatarBlendshapes();
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

        return avatarBlendshapes;
    }

    /// <summary>
    /// Return a copy of the mesh with only the given blendshapes
    /// </summary>
    /// <param name="mesh">the mesh</param>
    /// <param name="blendshapeSet">Names of the blendshapes. Anything not in the set will be removed.</param>
    /// <returns>Copy of mesh without the listed blendshapes blendshapes</returns>
    public Mesh filterBlendshapes(Mesh mesh, HashSet<string> blendshapeSet) {
        MeshSimplifier ms = new MeshSimplifier(mesh);
        BlendShape[] bs = ms.GetAllBlendShapes();
        ms.ClearBlendShapes();

        foreach (BlendShape b in bs)
        {
            if (blendshapeSet.Contains(b.ShapeName))
            {
                ms.AddBlendShape(b);
            }
        }
        return ms.ToMesh();
    }

    /// <summary>
    /// Creates a copy of the mesh and remove all parts of the mesh that are mentioned in meshParts
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="meshParts">Names are based on the Mesh property names. uv, uv2 to uv8, colors and colors32</param>
    /// <returns></returns>
    public static Mesh deleteMeshStreams(Mesh mesh, HashSet<string> meshParts) {
        // force a copy here to make sure I don't fuck up my fbx files
        Mesh newMesh = BlendshapeAnalyzer.createMeshCopy(mesh);
        if (meshParts.Contains("uv")) newMesh.uv = null;
        if (meshParts.Contains("uv2")) newMesh.uv2 = null;
        if (meshParts.Contains("uv3")) newMesh.uv3 = null;
        if (meshParts.Contains("uv4")) newMesh.uv4 = null;
        if (meshParts.Contains("uv5")) newMesh.uv5 = null;
        if (meshParts.Contains("uv6")) newMesh.uv6 = null;
        if (meshParts.Contains("uv7")) newMesh.uv7 = null;
        if (meshParts.Contains("uv8")) newMesh.uv8 = null;
        if (meshParts.Contains("colors")) newMesh.colors = null;
        if (meshParts.Contains("colors32")) newMesh.colors32 = null;
        return newMesh;
    } 

    private void extractAnimatorControllerBlendshapes(AnimatorController avatarAnimatorController, ref Dictionary<string, HashSet<string>> knownBlendshapes)
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
                    extractMotion(s.state.motion, ref knownBlendshapes);
                }
            }
        }
    }

    public static Mesh createMeshCopy(Mesh originalMesh)
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

    private static void extractRuntimeAnimatorControllerBlendshapes(RuntimeAnimatorController avatarAnimatorController, ref Dictionary<string, HashSet<string>> knownBlendshapes)
    {
        if (avatarAnimatorController == null)
        {
            return;
        }
        else
        {
            foreach (AnimationClip clip in avatarAnimatorController.animationClips)
            {
                extractAnimationClip(clip, ref knownBlendshapes);
            }
        }
    }

    private static void extractMotion(Motion motion, ref Dictionary<string, HashSet<string>> knownBlendshapes)
    {
        if (motion is AnimationClip clip)
        {
            extractAnimationClip(clip, ref knownBlendshapes);
        }
        else if (motion is BlendTree blendTree)
        {
            extractBlendTree(blendTree, ref knownBlendshapes);
        }
    }

    private static void extractAnimationClip(AnimationClip clip, ref Dictionary<string, HashSet<string>> knownBlendshapes)
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

    private static void extractBlendTree(BlendTree blendTree, ref Dictionary<string, HashSet<string>> knownBlendshapes)
    {
        foreach (ChildMotion cm in blendTree.children)
        {
            if (cm.motion is null) break;
            extractMotion(cm.motion, ref knownBlendshapes);
        }
    }
}

#endif
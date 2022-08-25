#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

public class MeshPartSelectionWindowPart
{
    private class DisplayData {
        public string selectable;
        public GUIContent guiContent;
        public DisplayData(string s, GUIContent gc) {
            selectable = s;
            guiContent = gc;
        }
    }
    HashSet<string> selection;

    public MeshPartSelectionWindowPart() {
        selection = new HashSet<string>();
        displayData = new DisplayData[] {
            new DisplayData("uv", new GUIContent("UV", "Primary Texture Coordinates")),
            new DisplayData("uv2", new GUIContent("UV2", "Texture Coordinates Nr.2")),
            new DisplayData("uv3", new GUIContent("UV3", "Texture Coordinates Nr.3")),
            new DisplayData("uv4", new GUIContent("UV4", "Texture Coordinates Nr.4")),
            new DisplayData("uv5", new GUIContent("UV5", "Texture Coordinates Nr.5")),
            new DisplayData("uv6", new GUIContent("UV6", "Texture Coordinates Nr.6")),
            new DisplayData("uv7", new GUIContent("UV7", "Texture Coordinates Nr.7")),
            new DisplayData("uv8", new GUIContent("UV8", "Texture Coordinates Nr.8")),
            new DisplayData("colors", new GUIContent("Colors", "4xFloat RGBA Vertex Colors")),
            new DisplayData("colors32", new GUIContent("Colors32", "4xByte RGBA Vertex Colors"))
        };
    }

    private DisplayData[] displayData;

    public HashSet<string> DisplaySelection() {
        foreach(DisplayData dd in displayData) {

            bool res = EditorGUILayout.Toggle(dd.guiContent, selection.Contains(dd.selectable));
            if (res) selection.Add(dd.selectable);
            else selection.Remove(dd.selectable);
        }

        return selection;
    }
}

#endif

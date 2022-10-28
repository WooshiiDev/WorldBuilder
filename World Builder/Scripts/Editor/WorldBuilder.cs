using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.VersionControl;

[InitializeOnLoad()]
public static class WorldBuilder
{
    public const string ID = "World Builder";
    public static bool IsSnapping = false;
}

public class WorldBuilderWindow : EditorWindow
{
    /// <summary>
    /// Holds cache related to loaded prefabs and assets in WorldBuilder.
    /// </summary>
    public static class WorldBuilderCache
    {
        // Fields

        /// <summary>
        /// The current collection of prefabs loaded.
        /// </summary>
        public static GameObject[] Prefabs { get; private set; } = new GameObject[0];

        /// <summary>
        /// Contains GUIContent instances representing each prefab in the cache.
        /// </summary>
        public static GUIContent[] Content { get; private set; } = new GUIContent[0];

        // Methods

        /// <summary>
        /// Does a GameObject exist in the cache?
        /// </summary>
        /// <param name="instance">The instance to look for.</param>
        /// <returns>Returns a boolean based on whether the instance exists in cache or not.</returns>
        public static bool Exists(GameObject instance)
        {
            return Prefabs.Contains(instance);
        }

        /// <summary>
        /// Get an instance and its related GUIContent.
        /// </summary>
        /// <param name="index">The index of the instance.</param>
        /// <returns>Returns an ObjectContent object that contains both the instance and the GUIContent.</returns>
        public static ObjectContent Get(int index)
        {
            if (index < 0 || index >= Prefabs.Length)
            {
                return ObjectContent.Empty;
            }

            return new ObjectContent(Prefabs[index], Content[index]);
        }

        /// <summary>
        /// Set the prefab cache.
        /// </summary>
        /// <param name="prefabs">The prefabs to cache.</param>
        public static void SetCache(GameObject[] prefabs)
        {
            if (prefabs == null)
            {
                Debug.LogError("Prefabs ");
                return;
            }

            Prefabs = new GameObject[prefabs.Length];
            Content = new GUIContent[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                GameObject prefab = prefabs[i];

                if (prefab == null)
                {
                    Debug.LogError("Cannot cache null prefab. Are you manually assigning cache?");
                    return;
                }

                string name = prefab.name;
                GUIContent content = new GUIContent(name, AssetPreview.GetAssetPreview(prefab), name);


                Prefabs[i] = prefab;
                Content[i] = content;
            }

            AssetPreview.SetPreviewTextureCacheSize(Content.Length * 2);
        }
    }

    [System.Serializable]
    public struct ObjectContent
    {
        public readonly static ObjectContent Empty = new ObjectContent(null, null);

        public readonly GameObject instance;
        public readonly GUIContent content;

        public ObjectContent(GameObject instance, GUIContent content)
        {
            this.instance = instance;
            this.content = content;
        }
    }

    [SerializeField] private bool isSelectingPath = false;

    [SerializeField] private string assetPath;
    [SerializeField] private int selectedIndex;
    [SerializeField] private Material mat;

    [SerializeField] private Vector2 scrollPos;

    private static Event Event
    {
        get
        {
            return Event.current;
        }
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;

        selectedIndex = -1;
    }

    // Editor GUI

    private void OnGUI()
    {
        DrawSearchSettings();

        if (GUILayout.Button("Load Prefabs"))
        {
            SearchAndCacheAssets();
        }

        if (!AssetPreview.IsLoadingAssetPreviews())
        {
            GUIStyle style = new GUIStyle(EditorStyles.objectField)
            {
                imagePosition = ImagePosition.ImageOnly,

                fixedHeight = 96,
                fixedWidth = 96,

                padding = new RectOffset(4, 4, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
            };

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            selectedIndex = GUILayout.SelectionGrid(selectedIndex, WorldBuilderCache.Content, 4, style);
            EditorGUILayout.EndScrollView();
        }

        Repaint();
    }

    private void DrawSearchSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            WorldBuilder.IsSnapping = EditorGUILayout.Toggle("Snap", WorldBuilder.IsSnapping);
            mat = (Material)EditorGUILayout.ObjectField(mat, typeof(Material), false);

            if (GUILayout.Button(assetPath))
            {
                EditorGUIUtility.ShowObjectPicker<DefaultAsset>(null, false, "", 100);
                isSelectingPath = true;
            }
        }
        EditorGUILayout.EndVertical();

        if (isSelectingPath)
        {
            OnSearchObjectPicker();
        }
    }

    private void OnSearchObjectPicker()
    {

        if (EditorGUIUtility.GetObjectPickerControlID() != 100)
        {
            return;
        }

        switch (Event.commandName)
        {
            case "ObjectSelectorClosed":
                isSelectingPath = false;
                break;

            case "ObjectSelectorUpdated":

                Object picked = EditorGUIUtility.GetObjectPickerObject();

                if (picked != null)
                {
                    string path = AssetDatabase.GetAssetPath(picked);

                    if (AssetDatabase.IsValidFolder(path))
                    {
                        assetPath = path;
                    }
                    else
                    {
                        int index = path.LastIndexOf('/');
                        assetPath = path[..(index + 1)];
                    }
                }
                break;
        }
    }

    private void SearchAndCacheAssets()
    {
        // Find prefabs and get paths

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { assetPath });
        IEnumerable<string> paths = guids.Select(AssetDatabase.GUIDToAssetPath);

        // Find GameObjects 
        GameObject[] foundAssets = paths
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(o => o.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
            .ToArray();

        WorldBuilderCache.SetCache(foundAssets);
        selectedIndex = -1; 
    }

    // Scene GUI
    private void OnSceneGUI(SceneView scene)
    {
        if (selectedIndex == -1)
        {
            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(Event.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            Handles.DrawWireDisc(hit.point, hit.normal, 1f);
            DrawSceneMesh(scene.camera, hit.point, hit.normal);
        }

        scene.Repaint();
    }

    private void DrawSceneMesh(Camera camera, Vector3 position, Vector3 normal)
    {
        GameObject instance = WorldBuilderCache.Prefabs[selectedIndex];
        MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            Transform t = filter.transform;

            int submeshCount = filter.sharedMesh.subMeshCount;

            for (int j = 0; j < submeshCount; j++)
            {
                Graphics.DrawMesh(
                    filter.sharedMesh,
                    position + t.localPosition,
                    Quaternion.LookRotation(t.forward, normal),
                    mat,
                    filter.gameObject.layer,
                    camera,
                    j);
            }
        }
    }

    [MenuItem("World Builder/Show Window")]
    public static void ShowWindow()
    {
        GetWindow<WorldBuilderWindow>("World Builder", false, typeof(SceneView));
    }
}
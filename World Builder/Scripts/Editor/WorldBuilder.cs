using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace WB
{
    using Geometry;

    [InitializeOnLoad()]
    public static class WorldBuilder
    {
        public const string ID = "World Builder";
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

        private const float HANDLES_OFFSET = 5e-3f;

        [SerializeField] private bool isSelectingPath = false;

        [SerializeField] private string assetPath;
        [SerializeField] private int selectedIndex;
        [SerializeField] private Bounds selectedBounds;

        [SerializeField] private bool useDefaultMaterials = true;
        [SerializeField] private Material previewMaterial;

        [SerializeField] private Vector2 scrollPos;

        // Hit mesh in scene 

        private bool isPressingSnap = false;
        private Transform hitTransform;
        private MeshData hitMeshData;

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

                EditorGUI.BeginChangeCheck();
                {
                    selectedIndex = GUILayout.SelectionGrid(selectedIndex, WorldBuilderCache.Content, 4, style);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (selectedIndex != -1)
                    {
                        GameObject selectedInstance = WorldBuilderCache.Prefabs[selectedIndex];

                        MeshRenderer[] renderers = selectedInstance.GetComponentsInChildren<MeshRenderer>();
                        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

                        for (int i = 0; i < renderers.Length; i++)
                        {
                            bounds.Encapsulate(renderers[i].bounds);
                        }

                        selectedBounds = bounds;
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSearchSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

                useDefaultMaterials = EditorGUILayout.Toggle("Use default materials", useDefaultMaterials);

                EditorGUI.BeginDisabledGroup(useDefaultMaterials);
                previewMaterial = (Material)EditorGUILayout.ObjectField(previewMaterial, typeof(Material), false);
                EditorGUI.EndDisabledGroup();

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
                Vector3 point = hit.point;
                Vector3 normal = hit.normal;

                Transform transform = hit.transform;

                if (hitTransform != transform)
                {
                    MeshFilter filter = transform.GetComponent<MeshFilter>();

                    hitTransform = transform;
                    hitMeshData = new MeshData(filter.sharedMesh, transform);
                }

                switch (Event.keyCode)
                {
                    case KeyCode.LeftShift when !isPressingSnap && Event.type == EventType.KeyDown:
                        isPressingSnap = true;
                        break;

                    case KeyCode.LeftShift when isPressingSnap && Event.type == EventType.KeyUp:
                        isPressingSnap = false;
                        break;
                }

                if (isPressingSnap)
                {
                    Triangle tri = GetHitTriangle(ray, hit.triangleIndex, hit, out RaycastHit triangleHit);
                    Vector3[] snapPoints = { tri.A, tri.B, tri.C, tri.Centroid };

                    float dst = Mathf.Infinity;
                    Vector3 nearestPoint = Vector3.zero;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 snapPoint = snapPoints[i];
                        float lenSqr = (point - snapPoint).sqrMagnitude;

                        if (lenSqr < dst)
                        {
                            nearestPoint = snapPoint;
                            dst = lenSqr;
                        }
                    }

                    point = nearestPoint;
                    normal = triangleHit.normal;

                    DrawTriangle(tri, normal);
                }

                DrawSceneMesh(scene.camera, point, normal);
            }

            scene.Repaint();
        }

        private Triangle GetHitTriangle(Ray ray, int triangleIndex, RaycastHit previousHit, out RaycastHit triangleHit)
        {
            triangleHit = previousHit;

            float closest = Mathf.Infinity;

            Triangle triangle = Triangle.Zero;
            if (triangleIndex == -1)
            {
                for (int i = 0; i < hitMeshData.Triangles.Length; i++)
                {
                    Triangle tri = hitMeshData.Triangles[i];

                    // Reuse the original raycast parameter

                    if (GeometryPhysics.IntersectRayTriangle(ray, tri.A, tri.B, tri.C, out previousHit, true))
                    {
                        Vector3 avg = ray.origin - ((tri.A + tri.B + tri.C) / 3);
                        float lenSqr = avg.sqrMagnitude;

                        // The closest triangle hit is the one required

                        if (lenSqr < closest)
                        {
                            triangle = tri;
                            closest = lenSqr;
                            triangleHit = previousHit;
                        }
                    }
                }
            }
            else
            {
                // If we already have the index, there's no reason to loop over the mesh

                triangle = hitMeshData.Triangles[triangleIndex];
            }

            return triangle;
        }

        private void DrawSceneMesh(Camera camera, Vector3 position, Vector3 normal)
        {
            GameObject prefab = WorldBuilderCache.Prefabs[selectedIndex];
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);

            Quaternion rotation = Quaternion.LookRotation(normal) * Quaternion.Euler(90f, 0f, 0f);
            Material instanceMaterial = previewMaterial;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                Transform t = filter.transform;

                int submeshCount = filter.sharedMesh.subMeshCount;

                for (int j = 0; j < submeshCount; j++)
                {
                    if (useDefaultMaterials)
                    {
                        if (filter.TryGetComponent(out MeshRenderer renderer))
                        {
                            instanceMaterial = renderer.sharedMaterials[j];
                        }
                    }

                    Graphics.DrawMesh(
                        filter.sharedMesh,
                        position + (rotation * t.localPosition),
                        rotation,
                        instanceMaterial,
                        filter.gameObject.layer,
                        camera,
                        j);
                }
            }

            int button = Event.button;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            switch (Event.GetTypeForControl(controlID))
            {
                case EventType.MouseDown when button == 0:
                    GUIUtility.hotControl = controlID;
                    Event.Use();
                    break;

                case EventType.MouseUp when button == 0:

                    GUIUtility.hotControl = 0;

                    Undo.IncrementCurrentGroup();
                    {
                        GameObject instance = Instantiate(prefab, position, rotation);
                        Undo.RegisterCreatedObjectUndo(instance, "WorldBuilder Instanciation.");
                    }
                    Undo.SetCurrentGroupName("[WorldBuilder] Instanciated a prefab.");
                    Event.Use();
                    break;
            }

        }

        // GUI

        private void DrawTriangle(Triangle triangle, Vector3 normal)
        {
            Vector3 offset = Quaternion.LookRotation(normal) * new Vector3(0, 0, HANDLES_OFFSET);

            triangle.Move(offset);
            Vector3 centroid = triangle.Centroid;
            float handleSize = HandleUtility.GetHandleSize(centroid);
            float sphereSize = Mathf.Min(Mathf.Max(handleSize * 0.1f, 0.025f), 0.5f);

            CompareFunction zTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            {
                // Draw the snap points
                Handles.color = Color.red;
                Handles.SphereHandleCap(0, centroid, Quaternion.identity, sphereSize, EventType.Repaint);

                for (int i = 0; i < 3; i++)
                {
                    Vector3 corner = triangle[i];

                    Handles.color = Color.black;
                    Handles.DrawDottedLine(corner, centroid, 4f);

                    Handles.color = Color.red;
                    Handles.SphereHandleCap(0, corner, Quaternion.identity, sphereSize, EventType.Repaint);
                }

                // Draw the outline of the triangle 

                Handles.color = Color.black;
                {
                    Vector3 a = triangle.A;
                    Vector3 b = triangle.B;
                    Vector3 c = triangle.C;

                    Handles.DrawPolyLine(a, b, c, a);
                }
                Handles.color = Color.white;
            }
            Handles.zTest = zTest;
        }

        [MenuItem("World Builder/Show Window")]
        public static void ShowWindow()
        {
            GetWindow<WorldBuilderWindow>("World Builder", false, typeof(SceneView));
        }
    }
}
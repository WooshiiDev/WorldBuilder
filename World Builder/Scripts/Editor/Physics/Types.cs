using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

namespace WB.Physics
{
    /// <summary>
    /// Struct representing a triangle.
    /// </summary>
    [Serializable]
    public struct Triangle
    {
        [SerializeField] private Vector3 a;
        [SerializeField] private Vector3 b;
        [SerializeField] private Vector3 c;

        /// <summary>
        /// First corner of the represented triangle.
        /// </summary>
        public Vector3 A
        {
            get { return a; }
        }

        /// <summary>
        /// Second corner of the represented triangle.
        /// </summary>
        public Vector3 B
        {
            get { return b; }
        }

        /// <summary>
        /// Third corner of the represented triangle.
        /// </summary>
        public Vector3 C
        {
            get { return c; }
        }

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public void Transform(Transform transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException("transform", "Transform is null, cannot apply to triangle.");
            }

            a = transform.TransformPoint(a);
            b = transform.TransformPoint(b);
            c = transform.TransformPoint(c);
        }

        public override string ToString()
        {
            return $"Triangle: ({a},{b},{c})";
        }
    }

    /// <summary>
    /// Represents raw data of a mesh.
    /// </summary>
    [Serializable]
    public struct MeshData
    {
        [SerializeField] private int[] indices;
        [SerializeField] private Vector3[] vertices;
        [SerializeField] private Triangle[] triangles;

        /// <summary>
        /// Mesh indices for triangles.
        /// </summary>
        public int[] Indices
        {
            get
            {
                return indices;
            }
        }

        /// <summary>
        /// The mesh vertices.
        /// </summary>
        public Vector3[] Vertices
        {
            get
            {
                return vertices;
            }
        }
        
        /// <summary>
        /// The mesh triangles.
        /// </summary>
        public Triangle[] Triangles
        {
            get
            {
                return triangles;
            }
        }

        /// <summary>
        /// Populate the MeshData.
        /// </summary>
        /// <param name="mesh">The mesh the data is for.</param>
        /// <param name="transform">The transform this mesh is for. If given a transform, the data will be relative to the transform.</param>
        /// <exception cref="ArgumentNullException">Will throw a null exception if <paramref name="mesh"/> is null.</exception>
        public MeshData(Mesh mesh, Transform transform = null)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException("mesh", "Null mesh passed to MeshData.");
            }

            bool hasTransform = transform != null;

            vertices = mesh.vertices;
            indices = mesh.triangles;

            // Vertices need to be relative to transforms

            if (hasTransform)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = transform.TransformPoint(vertices[i]);
                }
            }

            // Create triangles

            int triangleCount = indices.Length / 3;
            Triangle[] tris = new Triangle[triangleCount];

            for (int i = 0; i < triangleCount; i ++)
            {
                int index = i * 3;

                int indiceA = indices[index];
                int indiceB = indices[index + 1];
                int indiceC = indices[index + 2];

                tris[i] = new Triangle(vertices[indiceA], vertices[indiceB], vertices[indiceC]);
            }

            triangles = tris;
        }
    }
}

//-----------------------------------------------------------------------
// <copyright file="ScreenSpaceDepthMeshGenerator.cs" company="Google LLC">
//
// Copyright 2020 Google LLC. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using GoogleARCore;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a dynamically updating triangle Mesh object from the depth texture.
/// </summary>
public class ScreenSpaceDepthMeshGenerator : MonoBehaviour
{
    // Specifies the maximum distance between vertices of a single triangle to be rendered.
    private const float k_TriangleConnectivityCutOff = 0.5f;

    private static readonly Vector3 k_DefaultMeshOffset = new Vector3(-100, -100, -100);

    private static readonly string k_VertexModelTransformPropertyName = "_VertexModelTransform";

    // Holds the vertex and index data of the depth template mesh.
    private Mesh m_Mesh;

    private bool m_FreezeMesh = false;

    private bool m_Initialized = false;

    // Holds a copy of the depth frame at the time the frame was frozen.
    private Texture2D m_StaticDepthTexture = null;

    /// <summary>
    /// Takes a snapshot of the current depth array and sets a static depth texture.
    /// </summary>
    public void FreezeDepthFrame()
    {
        m_FreezeMesh = true;
        m_StaticDepthTexture = DepthSource.GetDepthTextureSnapshot();

        Material material = GetComponent<Renderer>().material;
        material.SetTexture("_CurrentDepthTexture", m_StaticDepthTexture);
    }

    /// <summary>
    /// Resumes to update the depth texture on every frame.
    /// </summary>
    public void UnfreezeDepthFrame()
    {
        m_FreezeMesh = false;
        Material material = GetComponent<Renderer>().material;
        material.SetTexture("_CurrentDepthTexture", DepthSource.DepthTexture);
        Destroy(m_StaticDepthTexture);
        m_StaticDepthTexture = null;

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static int[] GenerateTriangles(int width, int height)
    {
        int[] indices = new int[(height - 1) * (width - 1) * 6];
        int idx = 0;
        for (int y = 0; y < (height - 1); y++)
        {
            for (int x = 0; x < (width - 1); x++)
            {
                //// Unity has a clockwise triangle winding order.
                //// Upper quad triangle
                //// Top left
                int idx0 = (y * width) + x;
                //// Top right
                int idx1 = idx0 + 1;
                //// Bottom left
                int idx2 = idx0 + width;

                //// Lower quad triangle
                //// Top right
                int idx3 = idx1;
                //// Bottom right
                int idx4 = idx2 + 1;
                //// Bottom left
                int idx5 = idx2;

                indices[idx++] = idx0;
                indices[idx++] = idx1;
                indices[idx++] = idx2;
                indices[idx++] = idx3;
                indices[idx++] = idx4;
                indices[idx++] = idx5;
            }
        }

        return indices;
    }

    private void InitializeMesh()
    {
        // Create template vertices.
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();

        // Create template vertices for the mesh object.
        for (int y = 0; y < DepthSource.DepthHeight; y++)
        {
            for (int x = 0; x < DepthSource.DepthWidth; x++)
            {
                Vector3 v = new Vector3(x * 0.01f, -y * 0.01f, 0) + k_DefaultMeshOffset;
                vertices.Add(v);
                normals.Add(Vector3.back);
            }
        }

        // Create template triangle list.
        int[] triangles = GenerateTriangles(DepthSource.DepthWidth, DepthSource.DepthHeight);

        // Create the mesh object and set all template data.
        m_Mesh = new Mesh();
        m_Mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m_Mesh.SetVertices(vertices);
        m_Mesh.SetNormals(normals);
        m_Mesh.SetTriangles(triangles, 0);
        m_Mesh.bounds = new Bounds(Vector3.zero, new Vector3(50, 50, 50));
        m_Mesh.UploadMeshData(true);

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = m_Mesh;

        // Sets camera intrinsics for depth reprojection.
        Material material = GetComponent<Renderer>().material;
        material.SetTexture("_CurrentDepthTexture", DepthSource.DepthTexture);
        material.SetFloat("_FocalLengthX", DepthSource.FocalLength.x);
        material.SetFloat("_FocalLengthY", DepthSource.FocalLength.y);
        material.SetFloat("_PrincipalPointX", DepthSource.PrincipalPoint.x);
        material.SetFloat("_PrincipalPointY", DepthSource.PrincipalPoint.y);
        material.SetInt("_ImageDimensionsX", DepthSource.ImageDimensions.x);
        material.SetInt("_ImageDimensionsY", DepthSource.ImageDimensions.y);
        material.SetFloat("_TriangleConnectivityCutOff", k_TriangleConnectivityCutOff);

        m_Initialized = true;
    }

    private void Update()
    {
        if (!m_FreezeMesh)
        {
            Material material = GetComponent<Renderer>().material;
            material.SetMatrix(k_VertexModelTransformPropertyName, DepthSource.LocalToWorldMatrix);
        }

        if (!m_Initialized && DepthSource.Initialized)
        {
            InitializeMesh();
        }
    }
}

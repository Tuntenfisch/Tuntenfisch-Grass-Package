using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tuntenfisch.Grass.Painter
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GrassPainter : MonoBehaviour, ISerializationCallbackReceiver
    {
        #region Public Properties
        public int GrassClusterCapacity => m_grassClusterCapacity;
        public int GrassClusterCount => m_grassClusterCount;
        public GrassBrushProperties BrushProperties => m_brushProperties;
        public GrassBladeProperties BladeProperties => m_bladeProperties;
        public Material SharedMaterial => MeshRenderer.sharedMaterial;
        #endregion

        #region Private Properties
        private Mesh Mesh
        {
            get
            {
                if (m_mesh == null)
                {
                    NativeArray<int> indices = new NativeArray<int>(m_grassClusterCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    InitializeIndicesJob initializeIndicesJob = new InitializeIndicesJob { Indices = indices };
                    initializeIndicesJob.Run(indices.Length);

                    m_mesh = new Mesh { name = "Grass Painter Mesh" };
                    m_mesh.SetVertexBufferParams(m_grassClusterCapacity, GrassVertex.Attributes);
                    m_mesh.SetVertexBufferData(Vertices.AsArray(), 0, 0, Vertices.Length);
                    m_mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
                    m_mesh.SetIndexBufferData(indices, 0, 0, indices.Length);

                    indices.Dispose();

                    if (MeshFilter.sharedMesh != null)
                    {
                        SecureDestroyOrDestroyImmediate(MeshFilter.sharedMesh);
                    }
                    MeshFilter.sharedMesh = MeshFilter.mesh = m_mesh;
                }
                return m_mesh;
            }

            set => m_mesh = value;
        }

        private MeshFilter MeshFilter
        {
            get
            {
                if (m_meshFilter == null)
                {
                    m_meshFilter = GetComponent<MeshFilter>();
                }
                return m_meshFilter;
            }
        }

        private MeshRenderer MeshRenderer
        {
            get
            {
                if (m_meshRenderer == null)
                {
                    m_meshRenderer = GetComponent<MeshRenderer>();
                }
                return m_meshRenderer;
            }
        }

        private NativeList<GrassVertex> Vertices
        {
            get
            {
                if (!m_vertices.IsCreated)
                {
                    m_vertices = new NativeList<GrassVertex>(m_grassClusterCapacity, Allocator.Persistent);
                    m_vertices.Resize(m_serializedMesh.Length, NativeArrayOptions.UninitializedMemory);
                    NativeArray<GrassVertex>.Copy(m_serializedMesh, m_vertices.AsArray());
                }
                return m_vertices;
            }
        }
        #endregion

        #region Inspector Fields
        [HideInInspector]
        [SerializeField]
        private int m_grassClusterCapacity = 2 << 14;
        [HideInInspector]
        [SerializeField]
        private int m_grassClusterCount;

        [Header("Brush Properties")]
        [Commons.Attributes.InlineField]
        [SerializeField]
        private GrassBrushProperties m_brushProperties = new GrassBrushProperties(1, 1.0f, 2.5f, 0.25f, GrassPainterMode.None);

        [SerializeField]
        private GrassBladeProperties m_bladeProperties = new GrassBladeProperties(new Color32(0, 100, 0, 255), new Color32(64, 168, 64, 255), 0.0f, 0, 0.075f, 0.75f, 7.5f, 0.125f, 2.0f, 0.375f);
        [HideInInspector]
        [SerializeField]
        private GrassPainterPresets m_presets;
        [HideInInspector]
        [SerializeField]
        // Unity cannot serialize NativeList instances, so we use a simple array to serialize our mesh.
        private GrassVertex[] m_serializedMesh;
        #endregion

        #region Private Fields
        private const int c_innterLoopBatchCount = 32;

        private Mesh m_mesh;
        private MeshFilter m_meshFilter;
        private MeshRenderer m_meshRenderer;
        private NativeList<GrassVertex> m_vertices;
        #endregion

        #region Unity Callbacks
        private void OnDisable()
        {
            if (m_vertices.IsCreated)
            {
                m_vertices.Dispose();
            }
        }

        public void OnBeforeSerialize()
        {
            if (m_vertices.IsCreated)
            {
                m_serializedMesh = m_vertices.ToArray();
                m_vertices.Dispose();
            }
        }

        public void OnAfterDeserialize()
        {
            if (m_vertices.IsCreated)
            {
                m_serializedMesh = m_vertices.ToArray();
                m_vertices.Dispose();
            }
        }
        #endregion

        #region Public Methods
        public void SetMaxGrassClusterCount(int maxGrassClusterCount, bool force = false)
        {
            if (maxGrassClusterCount < m_grassClusterCount)
            {
                if (!force)
                {
                    throw new ArgumentException($"Max grass cluster count is smaller than current grass cluster count. Parts of the mesh would be lost.", nameof(maxGrassClusterCount));
                }
                m_serializedMesh = Vertices.AsArray().Slice(0, maxGrassClusterCount).ToArray();
                Clear(ClearGrassFlags.RuntimeGrass);
                UpdateGrassMeshExplicitly();
            }
            m_grassClusterCapacity = maxGrassClusterCount;
        }

        public void SetBrushProperties(LayerMask? raycastLayer = null, float? radius = null, float? density = null, float? smoothing = null, GrassPainterMode? mode = null)
        {
            m_brushProperties = new GrassBrushProperties
            (
                raycastLayer.GetValueOrDefault(m_brushProperties.RaycastLayer),
                radius.GetValueOrDefault(m_brushProperties.Radius),
                density.GetValueOrDefault(m_brushProperties.Density),
                smoothing.GetValueOrDefault(m_brushProperties.Smoothing),
                mode.GetValueOrDefault(m_brushProperties.Mode)
            );
        }

        public void SetBladeProperties(GrassBladeProperties bladeProperties)
        {
            m_bladeProperties = bladeProperties;
        }

        public void Paint(float3 brushPosition, float3 brushNormal)
        {
            switch (BrushProperties.Mode)
            {
                case GrassPainterMode.Add:
                    AddGrass(brushPosition, brushNormal);
                    break;

                case GrassPainterMode.Remove:
                    RemoveGrass(brushPosition);
                    break;

                case GrassPainterMode.Modify:
                    ModifyGrass(brushPosition);
                    break;

                default:
                    throw new InvalidOperationException($"Cannot paint when mode \"{GrassPainterMode.None}\" is enabled.");
            }
        }

        public void Clear(ClearGrassFlags flags = ClearGrassFlags.Everything)
        {
            if ((flags & ClearGrassFlags.RuntimeGrass) != 0)
            {
                if (MeshFilter.sharedMesh != null)
                {
                    SecureDestroyOrDestroyImmediate(MeshFilter.sharedMesh);
                }

                if (m_mesh != null)
                {
                    SecureDestroyOrDestroyImmediate(m_mesh);
                }

                if (m_vertices.IsCreated)
                {
                    m_vertices.Dispose();
                }
                MeshFilter.sharedMesh = MeshFilter.mesh = Mesh = null;
                m_grassClusterCount = 0;
            }

            if ((flags & ClearGrassFlags.SerializedGrass) != 0)
            {
                m_serializedMesh = new GrassVertex[0];
            }
        }

        public void UpdateGrassMeshExplicitly()
        {
            Mesh.SetVertexBufferData(Vertices.AsArray(), 0, 0, Vertices.Length);
            Mesh.SetSubMesh(0, new SubMeshDescriptor(0, Vertices.Length, MeshTopology.Points));
            // We cannot use Mesh.RecalculateBounds() because it will compute the bounds based on
            // all Mesh.vertexCount vertices, of which some could be unitialized because they haven't
            // been painted yet. These uninitialized vertices would mess up the bounds calculation.
            //
            // Luckily setting a new sub-mesh with Mesh.SetSubMesh() will also calculate the bounds of 
            // the sub-mesh. We simply need to assign those sub-mesh bounds to Mesh.bounds.
            Mesh.bounds = Mesh.GetSubMesh(0).bounds;
            m_grassClusterCount = Vertices.Length;
        }
        #endregion

        #region Private Methods
        private void AddGrass(float3 brushPosition, float3 brushNormal)
        {
            if (GrassClusterCount == GrassClusterCapacity)
            {
                Debug.LogWarning("Unable to paint more grass. Grass cluster capacity has been reached.", this);
                return;
            }
            // For adding grass we essentially want to send out a bunch of raycasts distributed evenly
            // around the position, within the brush's radius, and in the opposite direction of the
            // passed in normal.
            //
            // Unlike the other grass painter modes, adding grass requires running a few different jobs
            // in succession, whereas each job depends on the last job:
            //
            // We first schedule our jobs and built up the dependencies.
            CreateRaycastCommandsJob.RaycastGrid Grid = new CreateRaycastCommandsJob.RaycastGrid
            {
                Dimensions = (int2)math.ceil(2.0f * BrushProperties.Radius * BrushProperties.Density),
                CellSize = 1.0f / BrushProperties.Density,
                // We don't actually want to create the raycasts directly on the plane spanned by the
                // position and normal, but slightly above it. Thats why we offset the plane's origin
                // by adding the normal to it. The CreateRaycastCommandsJob has to take this offset into
                // account when defining the max raycast distance.
                LocalToWorldTransform = new float4x4(Quaternion.LookRotation(-brushNormal), brushPosition + brushNormal)
            };
            // declare the uper limit of raycasts created. The actual amount of raycasts will most likely always be
            // smaller because, as mentioned above, we want to raycast within a circle around the brush.
            int maxRaycasts = Grid.Dimensions.x * Grid.Dimensions.y;

            if (GrassClusterCount + maxRaycasts > GrassClusterCapacity)
            {
                Debug.LogWarning("Unable to paint more grass. Adding more grass could potentially surpass the grass cluster capacity.", this);
                return;
            }
            // Alright, ideally we would declare raycastCommands to be of type NativeList and then populate that list
            // with the raycast commands and pass it into RaycastCommand.ScheduleBatch() with .AsDeferredJobArray().
            // But according to
            // https://forum.unity.com/threads/request-raycastcommand-schedulebatch-with-asdeferredjobarray-support.643261/
            // RaycastCommand.ScheduleBatch() doesn't support this as of now.
            NativeArray<RaycastCommand> raycastCommands = new NativeArray<RaycastCommand>(maxRaycasts, Allocator.TempJob);

            // Schedule the job for creating the raycasts.
            JobHandle jobHandle = new CreateRaycastCommandsJob
            {
                Grid = Grid,
                BrushProperties = BrushProperties,
                RaycastCommands = raycastCommands
            }.Schedule(maxRaycasts, c_innterLoopBatchCount);

            NativeArray<RaycastHit> hits = new NativeArray<RaycastHit>(maxRaycasts, Allocator.TempJob);
            // Schedule the job for doing the actual raycasting.
            jobHandle = RaycastCommand.ScheduleBatch(raycastCommands, hits, c_innterLoopBatchCount, jobHandle);

            // Schedule the job for creating the new grass vertices.
            jobHandle = new AddGrassJob
            {
                BrushPositionOS = transform.InverseTransformPoint(brushPosition),
                BrushProperties = BrushProperties,
                BladeProperties = BladeProperties,
                WorldToLocalMatrix = transform.worldToLocalMatrix,
                Hits = hits,
                Vertices = Vertices.AsParallelWriter()
            }.Schedule(maxRaycasts, c_innterLoopBatchCount, jobHandle);

            // After the jobs have been declared we complete the final job. Since the final job depends
            // on all jobs declared before, all of them will be completed.
            jobHandle.Complete();

            // In the end we dispose of the native arrays we created and update the grass mesh.
            raycastCommands.Dispose();
            hits.Dispose();
            UpdateGrassMeshExplicitly();
        }

        private void RemoveGrass(float3 brushPosition)
        {
            NativeList<GrassVertex> OutputVertices = new NativeList<GrassVertex>(Vertices.Length, Allocator.TempJob);

            JobHandle jobHandle = new RemoveGrassJob
            {
                BrushPositionOS = transform.InverseTransformPoint(brushPosition),
                BrushProperties = BrushProperties,
                InputVertices = Vertices,
                OutputVertices = OutputVertices.AsParallelWriter()
            }.Schedule(Vertices.Length, c_innterLoopBatchCount);

            jobHandle.Complete();
            Vertices.Resize(OutputVertices.Length, NativeArrayOptions.UninitializedMemory);
            NativeArray<GrassVertex>.Copy(OutputVertices, Vertices, OutputVertices.Length);
            OutputVertices.Dispose();
            UpdateGrassMeshExplicitly();
        }

        private void ModifyGrass(float3 brushPosition)
        {
            JobHandle jobHandle = new ModifyGrassJob
            {
                BrushPositionOS = transform.InverseTransformPoint(brushPosition),
                BrushProperties = BrushProperties,
                BladeProperties = BladeProperties,
                Vertices = Vertices
            }.Schedule(Vertices.Length, c_innterLoopBatchCount);

            jobHandle.Complete();
            UpdateGrassMeshExplicitly();
        }

        private void SecureDestroyOrDestroyImmediate(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
#else
            Destroy(obj);
#endif
        }
        #endregion

        #region Public Structs, Classes and Enums
        public enum GrassPainterMode
        {
            None,
            Add,
            Remove,
            Modify
        }

        [Flags]
        public enum ClearGrassFlags
        {
            RuntimeGrass = 2 << 0,
            SerializedGrass = 2 << 1,
            Everything = int.MaxValue
        }

        [Serializable]
        public struct GrassBrushProperties
        {
            public LayerMask RaycastLayer => m_raycastLayer;
            public float Radius => m_radius;
            public float RadiusSquared => m_radius * m_radius;
            public float SmoothingRadius => (1.0f - m_smoothing) * m_radius;
            public float SmoothingRadiusSquared => SmoothingRadius * SmoothingRadius;
            public float Density => m_density;
            public float Smoothing => m_smoothing;
            public GrassPainterMode Mode => m_mode;

            #region Private Fields
            [SerializeField]
            private LayerMask m_raycastLayer;
            [Range(0.1f, 10.0f)]
            [SerializeField]
            private float m_radius;
            [Range(1.0f, 10.0f)]
            [SerializeField]
            private float m_density;
            [Range(0.0f, 1.0f)]
            [SerializeField]
            private float m_smoothing;
            #endregion

            #region Private Fields
            private GrassPainterMode m_mode;
            #endregion

            #region Public Methods
            public GrassBrushProperties(LayerMask paintLayer, float radius, float density, float smoothing, GrassPainterMode mode)
            {
                m_raycastLayer = paintLayer;
                m_radius = radius;
                m_density = density;
                m_smoothing = smoothing;
                m_mode = mode;
            }

            public float GetInverseSmoothingFactor(float3 positionOS, float3 brushPositionOS)
            {
                return math.smoothstep(SmoothingRadius, Radius, math.length(positionOS - brushPositionOS));
            }

            public float GetSmoothingFactor(float3 positionOS, float3 brushPositionOS)
            {
                return 1.0f - GetInverseSmoothingFactor(positionOS, brushPositionOS);
            }
            #endregion
        }
        #endregion
    }
}
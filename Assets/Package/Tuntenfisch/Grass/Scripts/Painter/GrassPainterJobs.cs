using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Tuntenfisch.Grass.Painter
{
    [BurstCompile]
    public struct InitializeIndicesJob : IJobParallelFor
    {
        #region Public Fields
        [WriteOnly]
        public NativeArray<int> Indices;
        #endregion

        #region Public Methods
        public void Execute(int index)
        {
            Indices[index] = index;
        }
        #endregion
    }

    [BurstCompile]
    public struct CreateRaycastCommandsJob : IJobParallelFor
    {
        #region Public Fields
        public RaycastGrid Grid;
        public GrassPainter.GrassBrushProperties BrushProperties;

        [WriteOnly]
        public NativeArray<RaycastCommand> RaycastCommands;
        #endregion

        #region Public Methods
        public void Execute(int index)
        {
            // Calculate grid space position.
            float3 positionGS = new float3(index % Grid.Dimensions.x, index / Grid.Dimensions.y, 0.0f);
            positionGS -= 0.5f * new float3(Grid.Dimensions, 0.0f);
            positionGS *= Grid.CellSize;

            // Only populate those raycast commands that actual lie within the circle we want to raycast in.
            // All other raycast commands, i.e. the raycast commands at the corners of grid, will be 0
            // and later on be "ignored" by RaycastCommand.ScheduleBatch().
            //
            // Obviously they will cause a small overhead for RaycastCommand.ScheduleBatch(), but it
            // should be fine.
            if (math.lengthsq(positionGS) <= BrushProperties.RadiusSquared)
            {
                float3 position = math.mul(Grid.LocalToWorldTransform, new float4(positionGS, 1.0f)).xyz;
                // For the direction we use the forward axis of the local to world transform.
                float3 direction = Grid.LocalToWorldTransform.c2.xyz;
                // The raycast distance has to be slightly longer than the offset mentioned in GrassPainter.cs.
                RaycastCommands[index] = new RaycastCommand(position, direction, 1.1f, BrushProperties.RaycastLayer);
            }
        }
        #endregion

        public struct RaycastGrid
        {
            #region Public Fields
            public int2 Dimensions;
            public float CellSize;
            public float4x4 LocalToWorldTransform;
            #endregion
        }
    }

    [BurstCompile]
    public struct AddGrassJob : IJobParallelFor
    {
        #region Public Fields
        public float3 BrushPositionOS;
        public GrassPainter.GrassBrushProperties BrushProperties;
        public GrassBladeProperties BladeProperties;
        public float4x4 WorldToLocalMatrix;

        [ReadOnly]
        public NativeArray<RaycastHit> Hits;
        [WriteOnly]
        public NativeList<GrassVertex>.ParallelWriter Vertices;
        #endregion

        #region Public Methods
        public void Execute(int index)
        {
            RaycastHit hit = Hits[index];

            if (hit.colliderInstanceID == 0)
            {
                return;
            }
            float3 positionOS = math.mul(WorldToLocalMatrix, new float4(hit.point, 1.0f)).xyz;
            float3 normalOS = math.mul(WorldToLocalMatrix, new float4(hit.normal, 0.0f)).xyz;
            float smoothingFactor = BrushProperties.GetSmoothingFactor(positionOS, BrushPositionOS);
            GrassBladeProperties bladeProperties = new GrassBladeProperties(BladeProperties);
            bladeProperties.Height *= smoothingFactor;
            Vertices.AddNoResize(new GrassVertex(positionOS, normalOS, GetRandomTangentOS(normalOS), bladeProperties));
        }
        #endregion

        #region Private Methods
        private float4 GetRandomTangentOS(float3 normalOS)
        {
            float3 tangentOS = math.cross(normalOS, new float3(0.0f, 1.0f, 0.0f));

            if (math.lengthsq(tangentOS) < 8.0f * float.Epsilon)
            {
                tangentOS = math.cross(normalOS, new float3(1.0f, 0.0f, 0.0f));
            }
            math.normalize(tangentOS);

            return new float4(tangentOS, -1.0f);
        }
        #endregion
    }

    [BurstCompile]
    public struct RemoveGrassJob : IJobParallelFor
    {
        #region Public Fields
        public float3 BrushPositionOS;
        public GrassPainter.GrassBrushProperties BrushProperties;

        [ReadOnly]
        public NativeList<GrassVertex> InputVertices;
        [WriteOnly]
        public NativeList<GrassVertex>.ParallelWriter OutputVertices;
        #endregion

        #region Public Methods
        public void Execute(int index)
        {
            GrassVertex vertex = InputVertices[index];
            float inverseSmoothingFactor = BrushProperties.GetInverseSmoothingFactor(vertex.PositionOS, BrushPositionOS);

            if (inverseSmoothingFactor > 0.0f)
            {
                GrassBladeProperties bladeProperties = new GrassBladeProperties(vertex.BladeProperties);
                bladeProperties.Height *= inverseSmoothingFactor;
                OutputVertices.AddNoResize(new GrassVertex(vertex.PositionOS, vertex.NormalOS, vertex.TangentOS, bladeProperties));
            }
        }
        #endregion
    }

    [BurstCompile]
    public struct ModifyGrassJob : IJobParallelFor
    {
        #region Public Fields
        public float3 BrushPositionOS;
        public GrassPainter.GrassBrushProperties BrushProperties;
        public GrassBladeProperties BladeProperties;

        public NativeArray<GrassVertex> Vertices;
        #endregion

        #region Public Methods
        public void Execute(int index)
        {
            GrassVertex vertex = Vertices[index];
            float smoothingFactor = BrushProperties.GetSmoothingFactor(vertex.PositionOS, BrushPositionOS);

            if (smoothingFactor <= 0.0f)
            {
                return;
            }
            GrassBladeProperties bladeProperties = GrassBladeProperties.Lerp(vertex.BladeProperties, BladeProperties, smoothingFactor);
            Vertices[index] = new GrassVertex(vertex.PositionOS, vertex.NormalOS, vertex.TangentOS, bladeProperties);
        }
        #endregion
    }
}
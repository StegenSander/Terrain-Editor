using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class ChunkCompute : Chunk
{
    struct Triangle
    {
        public Vector3 point1;
        public Vector3 point2;
        public Vector3 point3;
    }

    private ComputeShader _MarchingCubeShader;
    private ComputeBuffer _InputBuffer;
    private ComputeBuffer _OutputBuffer;
    private ComputeBuffer _CountBuffer;
    private int _MainHandle;
    private bool _Updating;
    private bool _IsFinished;

    NativeArray<Triangle> _TrianglesOutput;
    public ChunkCompute(World world, Vector3 position, ComputeShader shader)
            : base(world,position)
    {
        _MarchingCubeShader = shader;
        if (!_MarchingCubeShader.HasKernel("MarchCubes")) Debug.Log("Kernel not found");
        _MainHandle = _MarchingCubeShader.FindKernel("MarchCubes");

        int amountOfVoxels
            = (world.TerrainInfo.ChunkWidth * world.TerrainInfo.PixelsPerUnit + 1)
            * (world.TerrainInfo.ChunkWidth * world.TerrainInfo.PixelsPerUnit + 1)
            * (world.TerrainInfo.ChunkHeight * world.TerrainInfo.PixelsPerUnit + 1);

        // A float for each voxel
        _InputBuffer = new ComputeBuffer(amountOfVoxels * sizeof(float), sizeof(float),ComputeBufferType.Structured);

        // a triangle (3 * Vector3) for each voxel
        _OutputBuffer = new ComputeBuffer(amountOfVoxels * 3 * 3 * sizeof(float), 3 * sizeof(float), ComputeBufferType.Append);

        _CountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }

    ~ChunkCompute()
    {
        _InputBuffer.Dispose();
        _InputBuffer.Release();
        _OutputBuffer.Dispose();
        _OutputBuffer.Release();
        _CountBuffer.Dispose();
        _CountBuffer.Release();
    }

    override public void CreateMesh()
    {
        if (!_Updating)
        {
            Profiler.BeginSample("Marching cubes");
            _Updating = true;
            //Set input
            _InputBuffer.SetData(Terrain._TerrainData);

            _OutputBuffer.SetCounterValue(0);
            _MarchingCubeShader.SetInt("terrainWidth", _ParentWorld.TerrainInfo.ChunkWidth);
            _MarchingCubeShader.SetInt("terrainHeight", _ParentWorld.TerrainInfo.ChunkHeight);
            _MarchingCubeShader.SetInt("pixelsPerUnit",  _ParentWorld.TerrainInfo.PixelsPerUnit);
            _MarchingCubeShader.SetBuffer(_MainHandle, Shader.PropertyToID("terrainData"), _InputBuffer);
            _MarchingCubeShader.SetBuffer(_MainHandle, Shader.PropertyToID("triangles"), _OutputBuffer);

            _MarchingCubeShader.Dispatch(_MainHandle
                , _ParentWorld.TerrainInfo.ChunkWidth * _ParentWorld.TerrainInfo.PixelsPerUnit / 8
                , _ParentWorld.TerrainInfo.ChunkHeight * _ParentWorld.TerrainInfo.PixelsPerUnit / 8
                , _ParentWorld.TerrainInfo.ChunkWidth * _ParentWorld.TerrainInfo.PixelsPerUnit / 8);

            Action<AsyncGPUReadbackRequest> GPUCallback = gpuRequest => OutputBufferCompleted(gpuRequest);
            AsyncGPUReadback.Request(_OutputBuffer, GPUCallback);
            Profiler.EndSample();
        }
        if (_IsFinished)
        {

            UpdateMesh();

            _Updating = false;
            NeedsUpdate = false;
            _IsFinished = false;
        }
    }

    private void OutputBufferCompleted(AsyncGPUReadbackRequest GPUReadback)
    {
        _TrianglesOutput = GPUReadback.GetData<Triangle>();

        //Get the count from the append buffer
        ComputeBuffer.CopyCount(_OutputBuffer,_CountBuffer, 0);
        int[] counter = new int[1] { 0 };
        _CountBuffer.GetData(counter);

       
        List<Triangle> triangleList = new List<Triangle>(_TrianglesOutput.GetSubArray(0, counter[0]).ToArray());

        _VertexBuffer.Clear();
        _IndexBuffer.Clear();
        foreach (Triangle triangle in triangleList)
        {
            _VertexBuffer.Add(triangle.point1);
            _VertexBuffer.Add(triangle.point2);
            _VertexBuffer.Add(triangle.point3);

            _IndexBuffer.Add(_VertexBuffer.Count - 3);
            _IndexBuffer.Add(_VertexBuffer.Count - 2);
            _IndexBuffer.Add(_VertexBuffer.Count - 1);
        }

        _IsFinished = true;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class Chunk
{
    #region Variables
    protected World _ParentWorld;
    public World GetWorld { get { return _ParentWorld; } }
    protected World.TerrainInformation _TerrainInfo;

    protected GameObject _ChunkObject;
    public GameObject ChunkObject
    {
        get { return _ChunkObject; }
    }
    protected MeshFilter _MeshFilter;
    protected MeshCollider _MeshCollider;

    //Vertex Buffer
    protected List<Vector3> _VertexBuffer = new List<Vector3>();
    public List<Vector3> VertexBuffer
    {
        get { return _VertexBuffer; }
    }

    //IndexBuffer
    protected List<int> _IndexBuffer = new List<int>();
    public List<int> IndexBuffer
    {
        get { return _IndexBuffer; }
    }

    public bool NeedsUpdate { get; set; } = true;

    TerrainMap _TerrainMap;
    public TerrainMap Terrain
    {
        get { return _TerrainMap; }
    }
    #endregion

    public Chunk(World world, Vector3 position)
    {
        _ParentWorld = world;
        _TerrainInfo = _ParentWorld.TerrainInfo;
        _TerrainMap = new TerrainMap(this,position);


        _ChunkObject = new GameObject();
        _ChunkObject.transform.parent = world.transform;
        _ChunkObject.transform.position = position;
        _MeshFilter = _ChunkObject.AddComponent<MeshFilter>();
        _ChunkObject.AddComponent<MeshRenderer>().material = _TerrainInfo.Mat;
        _MeshCollider =  _ChunkObject.AddComponent<MeshCollider>();
    }

    virtual public void CreateMesh()
    {
        Profiler.BeginSample("Clearing Data");
        NeedsUpdate = false;
        //Clear previously used data
        _VertexBuffer.Clear();
        _IndexBuffer.Clear();
        Profiler.EndSample();

        Profiler.BeginSample("Marching cubes");
        for (int x = 0; x < _TerrainInfo.ChunkWidth * _TerrainInfo.PixelsPerUnit; x++)
            for (int y = 0; y < _TerrainInfo.ChunkHeight * _TerrainInfo.PixelsPerUnit; y++)
                for (int z = 0; z < _TerrainInfo.ChunkWidth * _TerrainInfo.PixelsPerUnit; z++)
                {
                    MarchCube( x, y, z);
                }
        Profiler.EndSample();

        Profiler.BeginSample("Updating mesh");
        UpdateMesh();
        Profiler.EndSample();
    }

    private void MarchCube(int x,int y, int z)
    {
        MarchCube(new Vector3Int(x, y, z));
    }
    private void MarchCube(Vector3Int pos)
    {
        //Get Cube index in the triangle Table
        //loop over all corners of the cube
        int triangleIdx = 0;
        for (int i = 0; i < 8; i++)
        {
            Vector3Int sampPos = pos + MarchingCubeData.CornerTable[i];
            if (_TerrainMap.SampleTerrain(sampPos) > 0/*Surface level*/)
                triangleIdx |= 1 << i; //Set the correct bit flag to 1, these bit value match the Triangle Table in Marching Cube Data
        }


        //Debug.Log($"Position:{pos} triangleIndex:{triangleIdx}");
        int idx = 0;
        for (int t = 0; t < 5; t++) //max 5 triangles per cube
            for (int p = 0; p < 3; p++) //3 points per triangle
            {
                //Get the edge out of the triangle table
                int edgeIdx = MarchingCubeData.TriangleTable[triangleIdx, idx];
                if (edgeIdx == -1) //-1 -> end of this triangeTable triangle
                    return;
                //Get the 2 vertices of the edge
                Vector3 vert1 = (pos + MarchingCubeData.CornerTable[MarchingCubeData.EdgeTable[edgeIdx, 0]]);
                Vector3 vert2 = (pos + MarchingCubeData.CornerTable[MarchingCubeData.EdgeTable[edgeIdx, 1]]);

                Vector3 vertPos = ((vert1 + vert2) / 2f) / _TerrainInfo.PixelsPerUnit;

                //_IndexBuffer.Add(AddToVertexBuffer(vertPos));
                _VertexBuffer.Add(vertPos);
                _IndexBuffer.Add(_VertexBuffer.Count - 1);

                idx++;
            }
    }

    //Add to vertex buffer returns the index;
    private int AddToVertexBuffer(Vector3 v)
    {
        int index = _VertexBuffer.IndexOf(v);
        if (index != -1)
            return index;

        _VertexBuffer.Add(v); //add vertex to list
        return _VertexBuffer.Count - 1; //return new idx
    }

    protected void UpdateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = _VertexBuffer.ToArray();
        mesh.triangles = _IndexBuffer.ToArray();
        mesh.RecalculateNormals();

        _MeshFilter.mesh = mesh;
        _MeshCollider.sharedMesh = mesh;
    }
}

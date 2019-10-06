using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Prutils.SpriteAnimation
{


    public struct MeshData : ISharedComponentData
    {
        
        public float Height;
        public float Width;
        public int _MaterialIndex;
        public int _MeshIndex;


        public MeshData(float height, float width, int materialIndex) {
            //TODO material index to be used with some material holder gameobject that just stored references to materials by index
            Height = height;
            Width = width;
            _MaterialIndex = materialIndex;
            _MeshIndex = -1;
            _MeshIndex = MakeMesh();

        }

        public Material GetMaterial() {
            return MaterialStore.GetInstance().GetMaterialById(_MaterialIndex);
        }

        public int GetMeshID() {
            return _MeshIndex;
        }
        private int MakeMesh() {

                Mesh m = new Mesh();
                
                Vector3[] vertices = new Vector3[4];
                Vector2[] uv = new Vector2[4];
                int[] triangles = new int[6];
            
                vertices[0] = new Vector3(0, 0);
                vertices[1] = new Vector3(0, Height);
                vertices[2] = new Vector3(Width, Height);
                vertices[3] = new Vector3(Width, 0);
                
                uv[0] = new Vector2(0,0);
                uv[1] = new Vector2(0,1);
                uv[2] = new Vector2(1,1);
                uv[3] = new Vector2(1,0);
                

                triangles[0] = 0;
                triangles[1] = 1;
                triangles[2] = 2;
                
                triangles[3] = 0;
                triangles[4] = 2;
                triangles[5] = 3;
                
                m.vertices = vertices;
                m.uv = uv;
                m.triangles = triangles;

                
                return MaterialStore.GetInstance().AddMesh(m);

        }
    }

}
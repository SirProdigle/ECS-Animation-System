using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Prutils.SpriteAnimation
{
    public class MaterialStore : MonoBehaviour
    {
        private static MaterialStore instance;
        public List<Material> Materials;
        public List<Mesh> Meshes;

        public static MaterialStore GetInstance() {
            if (instance == null) {
                instance = GameObject.FindObjectOfType<MaterialStore>();
            }
            return instance;
        }

        public void Awake() {
            instance = this;
        }

        public Material GetMaterialById(int id) {
            return Materials[id];
        }
         public Mesh GetMeshById(int id) {
             return Meshes[id];
         }

         public int AddMesh(Mesh m) {
             int current = Meshes.Count;
             Meshes.Add(m);
             return current;
         }
    }

}